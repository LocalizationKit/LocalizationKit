using System.Xml;

namespace LocalizationKit.Tooling;

/// <summary>Finds resource keys that nothing in the project appears to use.</summary>
/// <remarks>
/// <para>
/// Keys accumulate. A screen is rewritten, its strings stay, and a translator is paid to translate
/// text no user will ever see. Nothing else here can tell: every other rule reads the resources, and
/// whether a key is used is a question about the code.
/// </para>
/// <para>
/// So this reads the source instead, and it is honest about what that can prove. A key named in one
/// piece is found whether it was written as a string or reached through the generated constants,
/// because <c>LocalizationKeys.Settings.Theme.Header</c> contains <c>Settings.Theme.Header</c>. A key
/// assembled at runtime — <c>"Login.Error." + failure</c> — cannot be found at all, and a key that is
/// genuinely unused looks exactly the same. That is why a finding here is a note rather than a
/// warning: it is a list worth reading, not a verdict.
/// </para>
/// </remarks>
public static class UnusedKeyScanner
{
    private static readonly string[] Extensions = [".cs", ".xaml", ".axaml", ".razor", ".cshtml", ".vb"];

    private static readonly string[] SkippedDirectories = ["bin", "obj", ".vs", ".git", "node_modules"];

    /// <summary>Looks for keys the project never mentions.</summary>
    /// <param name="project">The project to read.</param>
    /// <returns>One finding listing the unused keys, or nothing when they are all used.</returns>
    public static IReadOnlyList<ValidationMessage> Scan(ProjectContext project)
    {
        ArgumentNullException.ThrowIfNull(project);

        string culture = project.DefaultLanguage ?? (project.Cultures.Count > 0 ? project.Cultures[0] : string.Empty);
        if (culture.Length == 0)
        {
            return [];
        }

        string path = project.ResourceFileFor(culture);
        if (!File.Exists(path))
        {
            return [];
        }

        IReadOnlyList<ReswEntry> entries;
        try
        {
            entries = ReswFile.Read(path);
        }
        catch (XmlException)
        {
            // Already reported as LK005.
            return [];
        }

        if (entries.Count == 0)
        {
            return [];
        }

        string sources = ReadSources(project.ProjectDirectory);

        List<string> unused =
        [
            .. entries
                .Select(entry => entry.Name)
                .Where(name => !IsMentioned(name, sources))
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        if (unused.Count == 0)
        {
            return [];
        }

        return
        [
            new ValidationMessage(
                ValidationSeverity.Info,
                "LK016",
                $"{unused.Count} {(unused.Count == 1 ? "key is" : "keys are")} never mentioned in the "
                + $"project's source: {Summarize(unused)}. A key built at runtime cannot be seen from "
                + "here, so check before deleting.",
                path),
        ];
    }

    private static bool IsMentioned(string name, string sources)
    {
        if (sources.Contains(name, StringComparison.Ordinal))
        {
            return true;
        }

        // The generated constants sanitize a segment that C# would reject, so a key named
        // "Dialog.Delete forever" is reached as Dialog.Delete_forever and would otherwise look unused.
        string identifier = Sanitize(name);
        return !string.Equals(identifier, name, StringComparison.Ordinal)
            && sources.Contains(identifier, StringComparison.Ordinal);
    }

    private static string Sanitize(string name)
    {
        char[] characters = name.ToCharArray();

        for (int index = 0; index < characters.Length; index++)
        {
            char character = characters[index];
            bool allowed = character == '.' || character == '_' || char.IsLetterOrDigit(character);
            if (!allowed)
            {
                characters[index] = '_';
            }
        }

        return new string(characters);
    }

    /// <summary>
    /// Every source file, concatenated. A single string rather than a file-by-file search because the
    /// question is only whether a key appears anywhere, and one pass over a few hundred kilobytes is
    /// cheaper than reopening the files once per key.
    /// </summary>
    /// <param name="directory">The project directory.</param>
    /// <returns>The text of every source file.</returns>
    private static string ReadSources(string directory)
    {
        System.Text.StringBuilder sources = new();

        foreach (string file in Enumerate(directory))
        {
            try
            {
                _ = sources.Append(File.ReadAllText(file)).Append('\n');
            }
            catch (IOException)
            {
                // A file being written by the IDE: skip it rather than fail the scan.
            }
            catch (UnauthorizedAccessException)
            {
                // Likewise.
            }
        }

        return sources.ToString();
    }

    private static IEnumerable<string> Enumerate(string directory)
    {
        foreach (string file in Directory.EnumerateFiles(directory))
        {
            if (Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }

        foreach (string child in Directory.EnumerateDirectories(directory))
        {
            string name = Path.GetFileName(child);
            if (SkippedDirectories.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (string file in Enumerate(child))
            {
                yield return file;
            }
        }
    }

    private static string Summarize(IReadOnlyList<string> keys)
    {
        const int Examples = 8;

        return keys.Count <= Examples
            ? string.Join(", ", keys)
            : string.Join(", ", keys.Take(Examples)) + $", and {keys.Count - Examples} more";
    }
}
