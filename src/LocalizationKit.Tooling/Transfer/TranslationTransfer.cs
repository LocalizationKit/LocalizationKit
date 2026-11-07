using System.Globalization;
using System.Xml;

namespace LocalizationKit.Tooling;

/// <summary>What was imported, and what it changed.</summary>
/// <param name="Changes">The file writes to apply, which a caller may preview first.</param>
/// <param name="Notes">What is worth saying about the import.</param>
/// <param name="Updated">How many translations changed.</param>
/// <param name="Added">How many keys the file had that the culture did not.</param>
/// <param name="Skipped">How many units named keys the default culture does not have.</param>
public sealed record ImportResult(
    IReadOnlyList<FileChange> Changes,
    IReadOnlyList<string> Notes,
    int Updated,
    int Added,
    int Skipped);

/// <summary>Hands a culture over to a translator, and takes it back.</summary>
/// <remarks>
/// PO and XLIFF are interchange formats, which is why neither is a resource provider: a translator
/// receives one and returns one, and converting belongs here rather than at runtime. Both directions
/// go through the keys of the default culture, because that file is the list of strings the
/// application has — a unit naming anything else is a key that was renamed or removed since the
/// hand-off, and writing it back would resurrect it.
/// </remarks>
public static class TranslationTransfer
{
    /// <summary>Renders a culture for a translator.</summary>
    /// <param name="project">The project to read.</param>
    /// <param name="culture">The culture to translate into.</param>
    /// <param name="format">The format to write.</param>
    /// <returns>The document text.</returns>
    /// <exception cref="InvalidOperationException">The project has no default culture to translate from.</exception>
    public static string Export(ProjectContext project, string culture, TransferFormat format)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);

        string source = project.DefaultLanguage
            ?? (project.Cultures.Count > 0 ? project.Cultures[0] : null)
            ?? throw new InvalidOperationException(
                "The project has no cultures, so there is nothing to export. Set "
                + ProjectContext.CulturesProperty + " or add a culture folder.");

        string sourcePath = project.ResourceFileFor(source);
        if (!File.Exists(sourcePath))
        {
            throw new InvalidOperationException($"'{source}' has no resource file at {sourcePath}.");
        }

        IReadOnlyList<ReswEntry> entries = ReswFile.Read(sourcePath);
        IReadOnlyDictionary<string, string> translations = ReadTranslations(project, culture);

        List<TransferUnit> units =
        [
            .. entries.Select(entry => new TransferUnit(
                entry.Name,
                entry.Value,
                translations.TryGetValue(entry.Name, out string? existing) ? existing : string.Empty,
                entry.Comment)),
        ];

        return format switch
        {
            TransferFormat.Po => PoFile.Write(units, source, culture),
            TransferFormat.Xliff => XliffFile.Write(units, source, culture, project.ResourceFileName),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }

    /// <summary>Takes a translated document back into a culture's resource file.</summary>
    /// <param name="project">The project to write to.</param>
    /// <param name="culture">The culture the document translates into.</param>
    /// <param name="text">The document.</param>
    /// <param name="format">The format to read.</param>
    /// <returns>What it would change.</returns>
    /// <exception cref="FormatException">The document is malformed.</exception>
    public static ImportResult Import(
        ProjectContext project,
        string culture,
        string text,
        TransferFormat format)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        ArgumentNullException.ThrowIfNull(text);

        IReadOnlyList<TransferUnit> units = format switch
        {
            TransferFormat.Po => PoFile.Read(text),
            TransferFormat.Xliff => XliffFile.Read(text),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

        string source = project.DefaultLanguage ?? (project.Cultures.Count > 0 ? project.Cultures[0] : culture);
        HashSet<string> known = new(StringComparer.OrdinalIgnoreCase);
        string sourcePath = project.ResourceFileFor(source);

        if (File.Exists(sourcePath))
        {
            foreach (ReswEntry entry in ReswFile.Read(sourcePath))
            {
                _ = known.Add(entry.Name);
            }
        }

        string path = project.ResourceFileFor(culture);
        List<string> notes = [];
        int updated = 0;
        int added = 0;
        int skipped = 0;

        // A culture with no file yet gets one, written the way the scaffolder writes them. An existing
        // file is changed in place, which is the whole reason ReswDocument exists: rewriting it would
        // turn a handful of new translations into a diff of every line.
        bool existed = File.Exists(path);
        ReswDocument? document = null;
        List<ReswEntry> fresh = [];

        if (existed)
        {
            try
            {
                document = ReswDocument.Load(path);
            }
            catch (FormatException exception)
            {
                throw new FormatException($"{path} could not be read: {exception.Message}", exception);
            }
            catch (XmlException exception)
            {
                throw new FormatException($"{path} is not well-formed XML: {exception.Message}", exception);
            }
        }

        IReadOnlyDictionary<string, string> before = existed
            ? document!.Entries.ToDictionary(entry => entry.Name, entry => entry.Value, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (TransferUnit unit in units)
        {
            if (unit.Key.Length == 0 || string.IsNullOrEmpty(unit.Target))
            {
                continue;
            }

            if (known.Count > 0 && !known.Contains(unit.Key))
            {
                // Renamed or removed since the hand-off: writing it back would bring it back.
                skipped++;
                continue;
            }

            bool isNew = !before.ContainsKey(unit.Key);

            if (existed)
            {
                // The note travels outwards only. It is the source culture's note to the translator,
                // and the target file's note is its own: writing one over the other replaces what
                // somebody wrote about this translation with what somebody wrote about the English.
                // A key that is new to this culture has no note to protect, so it keeps the one it
                // arrived with.
                if (document!.Set(unit.Key, unit.Target, isNew ? unit.Comment : null))
                {
                    if (isNew)
                    {
                        added++;
                    }
                    else
                    {
                        updated++;
                    }
                }
            }
            else
            {
                fresh.Add(new ReswEntry(unit.Key, unit.Target, unit.Comment));
                added++;
            }
        }

        if (skipped > 0)
        {
            notes.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{skipped} unit(s) name keys '{source}' does not have, and were left out."));
        }

        if (updated == 0 && added == 0)
        {
            notes.Add("Every translation in the document is already in the file.");
            return new ImportResult([], notes, 0, 0, skipped);
        }

        string description = string.Create(
            CultureInfo.InvariantCulture,
            $"{updated} translation(s) changed, {added} added");

        FileChange change = existed
            ? new FileChange(path, description, document!.ToText(), FileChangeKind.Update, document.HasByteOrderMark)
            : new FileChange(path, description, ReswFile.ToXml(fresh), FileChangeKind.Create, WriteByteOrderMark: true);

        return new ImportResult([change], notes, updated, added, skipped);
    }

    private static IReadOnlyDictionary<string, string> ReadTranslations(ProjectContext project, string culture)
    {
        string path = project.ResourceFileFor(culture);

        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        Dictionary<string, string> translations = new(StringComparer.OrdinalIgnoreCase);

        foreach (ReswEntry entry in ReswFile.Read(path))
        {
            translations[entry.Name] = entry.Value;
        }

        return translations;
    }
}
