using System.Globalization;

namespace LocalizationKit.Tooling;

/// <summary>Everything adding one language touches.</summary>
/// <param name="Culture">The culture that was added.</param>
/// <param name="Changes">The files to write.</param>
/// <param name="Notes">Lines worth printing, including anything the tool deliberately did not do.</param>
public sealed record AddCultureResult(string Culture, IReadOnlyList<FileChange> Changes, IReadOnlyList<string> Notes)
{
    /// <summary>Gets a value indicating whether anything needs writing.</summary>
    public bool HasChanges => Changes.Count > 0;

    /// <summary>Writes every planned change.</summary>
    public void Apply()
    {
        foreach (FileChange change in Changes)
        {
            change.Apply();
        }
    }
}

/// <summary>
/// Adds a language: the <c>.resw</c> file, the culture list in the project, and the local Visual
/// Studio state, so that nothing is left for anyone to remember.
/// </summary>
public static class CultureScaffolder
{
    /// <summary>Plans everything needed to add one culture.</summary>
    /// <param name="project">The project to add the culture to.</param>
    /// <param name="culture">An IETF language tag, such as <c>it-IT</c>.</param>
    /// <param name="seedFromDefault">
    /// Whether to copy the default culture's keys and comments into the new file with empty values,
    /// which gives a translator the full list to work through.
    /// </param>
    /// <param name="prune">Whether to drop Resource Explorer columns for cultures that are gone.</param>
    /// <returns>The planned changes.</returns>
    /// <exception cref="CultureNotFoundException"><paramref name="culture"/> is not a culture.</exception>
    public static AddCultureResult Plan(
        ProjectContext project,
        string culture,
        bool seedFromDefault = true,
        bool prune = false)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);

        // Throws for anything that is not a real culture, before a single file is touched. Without
        // predefinedOnly, .NET happily synthesizes a culture for a typo like "en-USA".
        string cultureName = CultureInfo.GetCultureInfo(culture, predefinedOnly: true).Name;

        List<FileChange> changes = [];
        List<string> notes = [];

        string target = project.ResourceFileFor(cultureName);
        if (File.Exists(target))
        {
            notes.Add($"'{Path.GetFileName(target)}' already exists for {cultureName}; leaving it alone.");
        }
        else
        {
            IReadOnlyList<ReswEntry> entries = seedFromDefault ? SeedEntries(project, cultureName, notes) : [];
            changes.Add(new FileChange(
                target,
                entries.Count == 0
                    ? $"create an empty {cultureName} resource file"
                    : $"create the {cultureName} resource file with {entries.Count} untranslated keys",
                ReswFile.ToXml(entries),
                FileChangeKind.Create,
                WriteByteOrderMark: true));
        }

        FileChange? projectChange = PlanCultureListUpdate(project, cultureName, notes);
        if (projectChange is not null)
        {
            changes.Add(projectChange);
        }

        // Plan the IDE files against the culture list as it will be, not as it is.
        List<string> cultures = [.. project.Cultures];
        if (!cultures.Contains(cultureName, StringComparer.OrdinalIgnoreCase))
        {
            cultures.Add(cultureName);
        }

        IdeSyncResult ide = IdeSync.Plan(project, cultures, prune, [target]);
        changes.AddRange(ide.Changes.Where(c => !changes.Any(existing =>
            string.Equals(existing.Path, c.Path, StringComparison.OrdinalIgnoreCase))));
        notes.AddRange(ide.Notes);

        return new AddCultureResult(cultureName, changes, notes);
    }

    private static IReadOnlyList<ReswEntry> SeedEntries(ProjectContext project, string culture, List<string> notes)
    {
        string? defaultCulture = project.DefaultLanguage
            ?? project.DeclaredCultures.FirstOrDefault()
            ?? project.CultureFolders.FirstOrDefault();

        if (defaultCulture is null
            || string.Equals(defaultCulture, culture, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        string source = project.ResourceFileFor(defaultCulture);
        if (!File.Exists(source))
        {
            notes.Add($"No '{defaultCulture}' resource file to copy keys from; creating an empty file.");
            return [];
        }

        // Keys and translator comments carry over; values do not, so an untranslated string is
        // visibly empty rather than silently English.
        return [.. ReswFile.Read(source).Select(entry => entry with { Value = string.Empty })];
    }

    /// <summary>
    /// Adds the culture to an existing <c>LocalizationKitCultures</c> property. The property is not
    /// created when it is absent: the project file is in source control, and adding a build property
    /// to it is the developer's decision, not the tool's.
    /// </summary>
    private static FileChange? PlanCultureListUpdate(ProjectContext project, string culture, List<string> notes)
    {
        if (project.DeclaredCultures.Count == 0)
        {
            notes.Add(
                $"The project has no {ProjectContext.CulturesProperty} property. Add "
                + $"<{ProjectContext.CulturesProperty}>{string.Join(";", project.Cultures.Append(culture).Distinct(StringComparer.OrdinalIgnoreCase))}</{ProjectContext.CulturesProperty}> "
                + "to make the culture list explicit; until then the tooling uses the folders on disk.");
            return null;
        }

        if (project.DeclaredCultures.Contains(culture, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        string text = File.ReadAllText(project.ProjectFile);
        string open = $"<{ProjectContext.CulturesProperty}>";
        string close = $"</{ProjectContext.CulturesProperty}>";

        int start = text.IndexOf(open, StringComparison.Ordinal);
        int end = start < 0 ? -1 : text.IndexOf(close, start, StringComparison.Ordinal);
        if (start < 0 || end < 0)
        {
            notes.Add($"Could not find the {ProjectContext.CulturesProperty} property to update; add '{culture}' by hand.");
            return null;
        }

        int valueStart = start + open.Length;
        string current = text[valueStart..end].Trim();
        string updated = current.Length == 0 ? culture : $"{current};{culture}";

        return new FileChange(
            project.ProjectFile,
            $"add {culture} to {ProjectContext.CulturesProperty}",
            string.Concat(text.AsSpan(0, valueStart), updated, text.AsSpan(end)),
            FileChangeKind.Update,
            WriteByteOrderMark: text.Length > 0 && File.ReadAllBytes(project.ProjectFile) is [0xEF, 0xBB, 0xBF, ..]);
    }
}
