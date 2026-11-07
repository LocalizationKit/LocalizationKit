using System.Globalization;
using System.Xml;

namespace LocalizationKit.Tooling;

/// <summary>How much a validation finding matters.</summary>
public enum ValidationSeverity
{
    /// <summary>Worth knowing.</summary>
    Info = 0,

    /// <summary>Something a release should not ship with.</summary>
    Warning = 1,

    /// <summary>Something that will not work at runtime.</summary>
    Error = 2,
}

/// <summary>One validation finding.</summary>
/// <param name="Severity">How much it matters.</param>
/// <param name="Code">A stable code, such as <c>LK006</c>, so a build can filter on it.</param>
/// <param name="Text">The human-readable finding.</param>
/// <param name="File">The file it concerns, when it concerns one.</param>
/// <param name="Line">
/// The 1-based line in <paramref name="File"/> the finding sits on, when a single entry is to blame.
/// A finding that summarizes many keys has no line.
/// </param>
public sealed record ValidationMessage(
    ValidationSeverity Severity,
    string Code,
    string Text,
    string? File = null,
    int? Line = null);

/// <summary>
/// Checks a project's resources the way a build should: before a malformed message or a missing
/// translation reaches a user.
/// </summary>
public static class ResourceValidator
{
    private const int ExamplesToList = 5;

    /// <summary>Validates every culture of a project.</summary>
    /// <param name="project">The project to check.</param>
    /// <returns>The findings, most severe first.</returns>
    public static IReadOnlyList<ValidationMessage> Validate(ProjectContext project)
    {
        ArgumentNullException.ThrowIfNull(project);

        List<ValidationMessage> messages = [];
        IReadOnlyList<string> cultures = project.Cultures;

        if (cultures.Count == 0)
        {
            messages.Add(new ValidationMessage(
                ValidationSeverity.Error,
                "LK001",
                $"No cultures found. Set {ProjectContext.CulturesProperty} or add a folder under '{project.ResourcesPath}'."));
            return messages;
        }

        CheckCultureDeclarations(project, messages);

        string defaultCulture = project.DefaultLanguage ?? cultures[0];
        Dictionary<string, IReadOnlyList<ReswEntry>> loaded = new(StringComparer.OrdinalIgnoreCase);

        foreach (string culture in cultures)
        {
            string path = project.ResourceFileFor(culture);
            if (!File.Exists(path))
            {
                messages.Add(new ValidationMessage(
                    ValidationSeverity.Warning,
                    "LK004",
                    $"Culture '{culture}' has no resource file.",
                    path));
                continue;
            }

            try
            {
                loaded[culture] = ReswFile.Read(path);
            }
            catch (XmlException ex)
            {
                messages.Add(new ValidationMessage(
                    ValidationSeverity.Error,
                    "LK005",
                    $"'{culture}' could not be parsed: {ex.Message}",
                    path));
            }
        }

        foreach ((string culture, IReadOnlyList<ReswEntry> entries) in loaded)
        {
            CheckEntries(project, culture, entries, messages);
        }

        CheckKeyCoverage(project, defaultCulture, loaded, messages);

        return [.. messages.OrderByDescending(m => m.Severity).ThenBy(m => m.Code, StringComparer.Ordinal)];
    }

    private static void CheckCultureDeclarations(ProjectContext project, List<ValidationMessage> messages)
    {
        if (project.DefaultLanguage is null)
        {
            messages.Add(new ValidationMessage(
                ValidationSeverity.Warning,
                "LK002",
                $"The project has no {ProjectContext.DefaultLanguageProperty} property; the first culture is being "
                + "treated as the default.",
                project.ProjectFile));
        }
        else if (!project.Cultures.Contains(project.DefaultLanguage, StringComparer.OrdinalIgnoreCase))
        {
            messages.Add(new ValidationMessage(
                ValidationSeverity.Error,
                "LK002",
                $"{ProjectContext.DefaultLanguageProperty} is '{project.DefaultLanguage}', which is not one of the "
                + $"cultures ({string.Join(", ", project.Cultures)}). Every fallback chain ends at the default "
                + "culture, so it must exist.",
                project.ProjectFile));
        }

        if (project.DeclaredCultures.Count == 0)
        {
            return;
        }

        foreach (string folder in project.CultureFolders)
        {
            if (!project.DeclaredCultures.Contains(folder, StringComparer.OrdinalIgnoreCase))
            {
                messages.Add(new ValidationMessage(
                    ValidationSeverity.Warning,
                    "LK003",
                    $"Culture folder '{folder}' is not listed in {ProjectContext.CulturesProperty}.",
                    project.ResourceFileFor(folder)));
            }
        }
    }

    private static void CheckEntries(
        ProjectContext project,
        string culture,
        IReadOnlyList<ReswEntry> entries,
        List<ValidationMessage> messages)
    {
        string path = project.ResourceFileFor(culture);
        SimpleMessageFormatter formatter = new();

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int?> lines = new(StringComparer.OrdinalIgnoreCase);
        List<string> duplicates = [];
        List<string> empty = [];

        foreach (ReswEntry entry in entries)
        {
            if (!seen.Add(entry.Name))
            {
                duplicates.Add(entry.Name);
            }

            lines.TryAdd(entry.Name, entry.Line);

            if (string.IsNullOrWhiteSpace(entry.Value))
            {
                empty.Add(entry.Name);
                continue;
            }

            if (!formatter.TryValidate(entry.Value, out string? error))
            {
                messages.Add(new ValidationMessage(
                    ValidationSeverity.Error,
                    "LK009",
                    $"[{culture}] '{entry.Name}' is not a valid message: {error}",
                    path,
                    entry.Line));
            }

            if (!LocalizedMarkup.TryParse(entry.Value, out _, out string? markupError))
            {
                messages.Add(new ValidationMessage(
                    ValidationSeverity.Error,
                    "LK013",
                    $"[{culture}] '{entry.Name}' has malformed link markup: {markupError}",
                    path,
                    entry.Line));
            }
        }

        CheckPriScopeCollisions(culture, seen, lines, path, messages);

        if (duplicates.Count > 0)
        {
            messages.Add(new ValidationMessage(
                ValidationSeverity.Error,
                "LK010",
                $"[{culture}] duplicate keys: {Summarize(duplicates)}",
                path));
        }

        if (empty.Count > 0)
        {
            messages.Add(new ValidationMessage(
                ValidationSeverity.Warning,
                "LK008",
                $"[{culture}] {empty.Count} untranslated {(empty.Count == 1 ? "key" : "keys")}: {Summarize(empty)}",
                path));
        }
    }

    /// <summary>
    /// Finds keys that cannot coexist in a compiled PRI.
    /// </summary>
    /// <remarks>
    /// MakePri reads a dot in a resource name as a hierarchy separator, so a key that is also the
    /// prefix of another key would have to be a string and a container at the same time, and the
    /// build fails with <c>PRI278</c>. It is worth catching here: the error arrives from a packaging
    /// tool with no obvious connection to the two keys that caused it, and it happens even when the
    /// application resolves its strings without PRI.
    /// </remarks>
    private static void CheckPriScopeCollisions(
        string culture,
        HashSet<string> keys,
        IReadOnlyDictionary<string, int?> lines,
        string path,
        List<ValidationMessage> messages)
    {
        foreach (string key in keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            for (int dot = key.IndexOf('.'); dot >= 0; dot = key.IndexOf('.', dot + 1))
            {
                string prefix = key[..dot];
                if (keys.Contains(prefix))
                {
                    messages.Add(new ValidationMessage(
                        ValidationSeverity.Error,
                        "LK012",
                        $"[{culture}] '{prefix}' is a key and also the prefix of '{key}'. MakePri treats a dot as a "
                        + $"scope separator, so this fails the build with PRI278. Rename one of them, for example to "
                        + $"'{prefix}{key[(dot + 1)..].Replace(".", string.Empty, StringComparison.Ordinal)}'.",
                        path,
                        lines.TryGetValue(key, out int? line) ? line : null));
                }
            }
        }
    }

    private static void CheckKeyCoverage(
        ProjectContext project,
        string defaultCulture,
        Dictionary<string, IReadOnlyList<ReswEntry>> loaded,
        List<ValidationMessage> messages)
    {
        if (!loaded.TryGetValue(defaultCulture, out IReadOnlyList<ReswEntry>? reference))
        {
            return;
        }

        if (reference.Count == 0)
        {
            messages.Add(new ValidationMessage(
                ValidationSeverity.Warning,
                "LK011",
                $"The default culture '{defaultCulture}' has no entries, so every lookup will fall through to the "
                + "configured missing-key behaviour.",
                project.ResourceFileFor(defaultCulture)));
        }

        HashSet<string> expected = new(reference.Select(e => e.Name), StringComparer.OrdinalIgnoreCase);

        foreach ((string culture, IReadOnlyList<ReswEntry> entries) in loaded)
        {
            if (string.Equals(culture, defaultCulture, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            HashSet<string> actual = new(entries.Select(e => e.Name), StringComparer.OrdinalIgnoreCase);

            List<string> missing = [.. expected.Except(actual, StringComparer.OrdinalIgnoreCase)
                .OrderBy(k => k, StringComparer.Ordinal)];

            List<string> extra = [.. actual.Except(expected, StringComparer.OrdinalIgnoreCase)
                .OrderBy(k => k, StringComparer.Ordinal)];

            if (missing.Count > 0)
            {
                messages.Add(new ValidationMessage(
                    ValidationSeverity.Warning,
                    "LK006",
                    $"[{culture}] {missing.Count} {(missing.Count == 1 ? "key is" : "keys are")} missing: {Summarize(missing)}",
                    project.ResourceFileFor(culture)));
            }

            if (extra.Count > 0)
            {
                messages.Add(new ValidationMessage(
                    ValidationSeverity.Warning,
                    "LK007",
                    $"[{culture}] {extra.Count} {(extra.Count == 1 ? "key is" : "keys are")} not in '{defaultCulture}': {Summarize(extra)}",
                    project.ResourceFileFor(culture)));
            }

            CheckLinkIds(project, culture, reference, entries, messages);
            CheckArguments(project, culture, reference, entries, messages);
        }
    }

    /// <summary>
    /// Checks that a translation carries the same link ids as the culture the keys were authored in.
    /// </summary>
    /// <remarks>
    /// A link is matched to its action by id, which is what lets a translator move links around. That
    /// only holds if the ids survive translation: an id that was dropped, renamed or duplicated leaves
    /// a link that does nothing, in one language only, which is close to undiscoverable by hand.
    /// </remarks>
    private static void CheckLinkIds(
        ProjectContext project,
        string culture,
        IReadOnlyList<ReswEntry> reference,
        IReadOnlyList<ReswEntry> entries,
        List<ValidationMessage> messages)
    {
        Dictionary<string, ReswEntry> translated = new(StringComparer.OrdinalIgnoreCase);
        foreach (ReswEntry entry in entries)
        {
            translated[entry.Name] = entry;
        }

        foreach (ReswEntry source in reference)
        {
            if (!translated.TryGetValue(source.Name, out ReswEntry? entry))
            {
                continue;
            }

            if (!TryGetLinkIds(source.Value, out IReadOnlyList<string> expected)
                || !TryGetLinkIds(entry.Value, out IReadOnlyList<string> actual))
            {
                // Malformed markup is already reported as LK013.
                continue;
            }

            if (expected.Order(StringComparer.Ordinal).SequenceEqual(actual.Order(StringComparer.Ordinal)))
            {
                continue;
            }

            messages.Add(new ValidationMessage(
                ValidationSeverity.Error,
                "LK014",
                $"[{culture}] '{source.Name}' has link ids [{string.Join(", ", actual)}] but '{project.DefaultLanguage ?? "the default culture"}' "
                + $"has [{string.Join(", ", expected)}]. A link whose id is missing from a translation does nothing in that language.",
                project.ResourceFileFor(culture),
                entry.Line));
        }
    }

    /// <summary>
    /// Checks that a translation refers to the same message arguments as the culture the keys were
    /// authored in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A translator who drops <c>{count}</c>, or types <c>{conteggio}</c> because the surrounding
    /// sentence is in Italian, leaves a message that is still valid ICU and still passes every other
    /// rule here. What happens next depends on the engine: the ICU one throws on an argument it was
    /// not given, so the string fails at runtime, in one language, on whatever screen happens to show
    /// it. An argument that is merely unused is milder — the number quietly never appears — but it is
    /// just as certainly a mistake.
    /// </para>
    /// <para>
    /// This is the rule that makes the plural work in section 4 safe to rely on: everything else
    /// checks that a message is well formed, and this checks that it is the same message.
    /// </para>
    /// </remarks>
    private static void CheckArguments(
        ProjectContext project,
        string culture,
        IReadOnlyList<ReswEntry> reference,
        IReadOnlyList<ReswEntry> entries,
        List<ValidationMessage> messages)
    {
        Dictionary<string, ReswEntry> translated = new(StringComparer.OrdinalIgnoreCase);
        foreach (ReswEntry entry in entries)
        {
            translated[entry.Name] = entry;
        }

        foreach (ReswEntry source in reference)
        {
            if (!translated.TryGetValue(source.Name, out ReswEntry? entry))
            {
                continue;
            }

            if (!TryGetArguments(source.Value, out IReadOnlyList<string> expected)
                || !TryGetArguments(entry.Value, out IReadOnlyList<string> actual))
            {
                // A malformed message is already reported as LK009.
                continue;
            }

            IReadOnlyList<string> missing = [.. expected.Except(actual, StringComparer.OrdinalIgnoreCase)];
            IReadOnlyList<string> extra = [.. actual.Except(expected, StringComparer.OrdinalIgnoreCase)];

            if (missing.Count == 0 && extra.Count == 0)
            {
                continue;
            }

            string detail = missing.Count > 0 && extra.Count > 0
                ? $"does not use {Summarize(missing)} and uses {Summarize(extra)} instead"
                : missing.Count > 0
                    ? $"does not use {Summarize(missing)}"
                    : $"uses {Summarize(extra)}, which the message has no value for";

            messages.Add(new ValidationMessage(
                ValidationSeverity.Error,
                "LK015",
                $"[{culture}] '{source.Name}' {detail}. The arguments of a translation have to match the "
                + "ones its message is given, or the value is dropped at best and the string fails at "
                + "worst.",
                project.ResourceFileFor(culture),
                entry.Line));
        }
    }

    private static bool TryGetArguments(string value, out IReadOnlyList<string> arguments)
    {
        try
        {
            arguments = IcuMessageSyntax.GetArgumentNames(value);
            return true;
        }
        catch (FormatException)
        {
            arguments = [];
            return false;
        }
    }

    private static bool TryGetLinkIds(string value, out IReadOnlyList<string> ids)
    {
        if (LocalizedMarkup.TryParse(value, out IReadOnlyList<LocalizedMarkupSegment> segments, out _))
        {
            ids = [.. segments
                .Where(segment => segment.Kind == LocalizedMarkupKind.Link && segment.Id is { Length: > 0 })
                .Select(segment => segment.Id!)];

            return true;
        }

        ids = [];
        return false;
    }

    private static string Summarize(IReadOnlyList<string> keys)
    {
        IEnumerable<string> shown = keys.Take(ExamplesToList);
        string text = string.Join(", ", shown);
        return keys.Count > ExamplesToList
            ? string.Create(CultureInfo.InvariantCulture, $"{text} (+{keys.Count - ExamplesToList} more)")
            : text;
    }
}
