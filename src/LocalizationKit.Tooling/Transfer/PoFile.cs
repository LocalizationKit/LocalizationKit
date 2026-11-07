using System.Globalization;
using System.Text;

namespace LocalizationKit.Tooling;

/// <summary>Reads and writes GNU gettext PO catalogues.</summary>
/// <remarks>
/// <para>
/// A key-based catalogue goes into PO the way gettext intends for one: the key is the
/// <c>msgctxt</c>, the source text is the <c>msgid</c>, and the translation is the <c>msgstr</c>.
/// Keeping the source text as the msgid rather than putting the key there is what lets a translator
/// see what they are translating, and the msgctxt is what keeps two identical English strings apart
/// when their keys differ.
/// </para>
/// <para>
/// Plurals stay whole. PO has its own plural model — <c>msgid_plural</c> with a <c>msgstr[n]</c> per
/// form, chosen by the catalogue's <c>Plural-Forms</c> header — and an ICU message already carries
/// every form inside one string. Splitting one into the other means deciding which of ICU's
/// categories maps to which of PO's indices, for every language, and reassembling it afterwards
/// without losing the parts PO cannot hold (<c>=0</c> exact matches, nested arguments, a plural
/// inside a branch). So an ICU message travels as the string it is, and the translator sees the
/// syntax. That is a real cost, and it is the reason the editor exists.
/// </para>
/// </remarks>
public static class PoFile
{
    /// <summary>Renders a catalogue.</summary>
    /// <param name="units">The strings to hand over.</param>
    /// <param name="sourceCulture">The culture the keys were authored in.</param>
    /// <param name="targetCulture">The culture being translated into.</param>
    /// <returns>The catalogue text.</returns>
    public static string Write(
        IEnumerable<TransferUnit> units,
        string sourceCulture,
        string targetCulture)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCulture);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetCulture);

        StringBuilder builder = new();

        // The header entry, which every PO file starts with: an empty msgid whose msgstr holds the
        // metadata.
        _ = builder.AppendLine("msgid \"\"")
            .AppendLine("msgstr \"\"")
            .AppendLine("\"Project-Id-Version: \\n\"")
            .AppendLine(Invariant($"\"POT-Creation-Date: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mmzzz}\\n\""))
            .AppendLine(Invariant($"\"Language: {targetCulture}\\n\""))
            .AppendLine(Invariant($"\"X-Source-Language: {sourceCulture}\\n\""))
            .AppendLine("\"MIME-Version: 1.0\\n\"")
            .AppendLine("\"Content-Type: text/plain; charset=UTF-8\\n\"")
            .AppendLine("\"Content-Transfer-Encoding: 8bit\\n\"")
            .AppendLine("\"X-Generator: LocalizationKit\\n\"");

        foreach (TransferUnit unit in units)
        {
            _ = builder.AppendLine();

            if (!string.IsNullOrEmpty(unit.Comment))
            {
                foreach (string line in unit.Comment.ReplaceLineEndings("\n").Split('\n'))
                {
                    _ = builder.Append("#. ").AppendLine(line);
                }
            }

            // A reference is where a translator's tool sends them to see the string in context; the
            // key is the nearest thing to a location a resource file has.
            _ = builder.Append("#: ").AppendLine(unit.Key);

            if (string.IsNullOrEmpty(unit.Target))
            {
                _ = builder.AppendLine("#, fuzzy");
            }

            _ = builder.Append("msgctxt ").AppendLine(Quote(unit.Key));
            _ = builder.Append("msgid ").AppendLine(Quote(unit.Source));
            _ = builder.Append("msgstr ").AppendLine(Quote(unit.Target));
        }

        return builder.ToString();
    }

    /// <summary>Reads a catalogue.</summary>
    /// <param name="text">The catalogue text.</param>
    /// <returns>The strings it holds, header entry excluded.</returns>
    /// <exception cref="FormatException">An entry is malformed.</exception>
    public static IReadOnlyList<TransferUnit> Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        List<TransferUnit> units = [];
        string[] lines = text.ReplaceLineEndings("\n").Split('\n');

        string? context = null;
        string? id = null;
        string? str = null;
        List<string> comments = [];
        StringBuilder? continued = null;
        string? field = null;

        void Flush()
        {
            Close();

            // The header is the entry with an empty msgid, and carries no string of its own.
            if (id is { Length: > 0 } || context is { Length: > 0 })
            {
                units.Add(new TransferUnit(
                    context ?? id ?? string.Empty,
                    id ?? string.Empty,
                    str ?? string.Empty,
                    comments.Count == 0 ? null : string.Join("\n", comments)));
            }

            context = null;
            id = null;
            str = null;
            comments = [];
        }

        void Close()
        {
            if (continued is null || field is null)
            {
                return;
            }

            string value = continued.ToString();
            switch (field)
            {
                case "msgctxt": context = value; break;
                case "msgid": id = value; break;
                case "msgstr": str = value; break;
            }

            continued = null;
            field = null;
        }

        foreach (string raw in lines)
        {
            string line = raw.Trim();

            if (line.Length == 0)
            {
                Flush();
                continue;
            }

            if (line.StartsWith("#.", StringComparison.Ordinal))
            {
                Close();
                comments.Add(line[2..].Trim());
                continue;
            }

            if (line.StartsWith('#'))
            {
                // A reference, a flag, or a commented-out entry: none of it is the translation.
                Close();
                continue;
            }

            if (line.StartsWith('"'))
            {
                // A continuation of whatever field is open, which is how PO wraps long strings.
                if (continued is null)
                {
                    throw new FormatException($"A quoted line stands on its own: {line}");
                }

                _ = continued.Append(Unquote(line));
                continue;
            }

            int space = line.IndexOf(' ');
            if (space < 0)
            {
                throw new FormatException($"'{line}' is not a PO field.");
            }

            string name = line[..space];
            if (name is not ("msgctxt" or "msgid" or "msgstr") && !name.StartsWith("msgstr[", StringComparison.Ordinal))
            {
                throw new FormatException($"'{name}' is not a PO field this reads.");
            }

            Close();
            field = name.StartsWith("msgstr[", StringComparison.Ordinal) ? "msgstr" : name;
            continued = new StringBuilder(Unquote(line[(space + 1)..].Trim()));
        }

        Flush();
        return units;
    }

    private static string Quote(string value)
    {
        StringBuilder quoted = new("\"");

        foreach (char character in value)
        {
            switch (character)
            {
                case '"': _ = quoted.Append("\\\""); break;
                case '\\': _ = quoted.Append("\\\\"); break;
                case '\n': _ = quoted.Append("\\n"); break;
                case '\r': _ = quoted.Append("\\r"); break;
                case '\t': _ = quoted.Append("\\t"); break;
                default: _ = quoted.Append(character); break;
            }
        }

        return quoted.Append('"').ToString();
    }

    private static string Unquote(string value)
    {
        string trimmed = value.Trim();

        if (trimmed.Length < 2 || !trimmed.StartsWith('"') || !trimmed.EndsWith('"'))
        {
            throw new FormatException($"A PO string has to be quoted: {value}");
        }

        StringBuilder text = new();
        string inner = trimmed[1..^1];

        for (int index = 0; index < inner.Length; index++)
        {
            if (inner[index] != '\\' || index + 1 >= inner.Length)
            {
                _ = text.Append(inner[index]);
                continue;
            }

            index++;
            _ = text.Append(inner[index] switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                '"' => '"',
                '\\' => '\\',
                char other => other,
            });
        }

        return text.ToString();
    }

    private static string Invariant(string text) => string.Create(CultureInfo.InvariantCulture, $"{text}");
}
