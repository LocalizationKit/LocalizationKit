using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace LocalizationKit;

/// <summary>Reads and writes WinRT <c>.resw</c> files.</summary>
/// <remarks>
/// <para>
/// Only string entries are read. An entry carrying a <c>type</c> or <c>mimetype</c> attribute is a
/// serialized object or a binary blob rather than a translation, so it is skipped instead of being
/// turned into a bogus string.
/// </para>
/// <para>
/// Files are written as UTF-8 with a BOM, CRLF line endings and no trailing newline, which is what
/// Visual Studio produces. A file this class writes and then reads back is unchanged on disk.
/// </para>
/// </remarks>
public static class ReswFile
{
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    /// <summary>Reads the string entries of a <c>.resw</c> file.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The entries, in document order.</returns>
    /// <exception cref="XmlException">The file is not well-formed XML.</exception>
    public static IReadOnlyList<ReswEntry> Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using FileStream stream = File.OpenRead(path);
        return Read(stream);
    }

    /// <summary>Reads the string entries of a <c>.resw</c> document.</summary>
    /// <param name="stream">The stream to read.</param>
    /// <returns>The entries, in document order.</returns>
    /// <exception cref="XmlException">The document is not well-formed XML.</exception>
    public static IReadOnlyList<ReswEntry> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // SetLineInfo costs a little memory and buys a clickable build error: a validation finding
        // can name the line the key is on, not just the file it is in.
        XDocument document = XDocument.Load(stream, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        if (document.Root is null)
        {
            return [];
        }

        List<ReswEntry> entries = [];
        foreach (XElement data in document.Root.Elements("data"))
        {
            string? name = data.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (data.Attribute("type") is not null || data.Attribute("mimetype") is not null)
            {
                continue;
            }

            string value = data.Element("value")?.Value ?? string.Empty;
            string? comment = data.Element("comment")?.Value;
            int line = ((IXmlLineInfo)data).HasLineInfo() ? ((IXmlLineInfo)data).LineNumber : 0;
            entries.Add(new ReswEntry(
                name,
                value,
                string.IsNullOrEmpty(comment) ? null : comment,
                line > 0 ? line : null));
        }

        return entries;
    }

    /// <summary>Reads a <c>.resw</c> file as a lookup table.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The entries keyed by name, compared case-insensitively as Windows resource names are.</returns>
    public static IReadOnlyDictionary<string, string> ReadValues(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return ReadValues(stream);
    }

    /// <summary>Reads a <c>.resw</c> document as a lookup table.</summary>
    /// <param name="stream">The stream to read.</param>
    /// <returns>The entries keyed by name, compared case-insensitively as Windows resource names are.</returns>
    public static IReadOnlyDictionary<string, string> ReadValues(Stream stream)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (ReswEntry entry in Read(stream))
        {
            values[entry.Name] = entry.Value;
        }

        return values;
    }

    /// <summary>Renders a complete <c>.resw</c> document.</summary>
    /// <param name="entries">The entries to write. They are emitted in the order given.</param>
    /// <returns>The document text, without a byte order mark.</returns>
    public static string ToXml(IEnumerable<ReswEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        StringBuilder builder = new(ReswTemplate.Header);
        foreach (ReswEntry entry in entries)
        {
            ArgumentException.ThrowIfNullOrEmpty(entry.Name);

            builder.Append("    <data name=\"").Append(EscapeAttribute(entry.Name)).Append("\" xml:space=\"preserve\">\r\n");
            builder.Append("        <value>").Append(EscapeText(entry.Value)).Append("</value>\r\n");

            if (!string.IsNullOrEmpty(entry.Comment))
            {
                builder.Append("        <comment>").Append(EscapeText(entry.Comment)).Append("</comment>\r\n");
            }

            builder.Append("    </data>\r\n");
        }

        return builder.Append(ReswTemplate.Footer).ToString();
    }

    /// <summary>Writes a complete <c>.resw</c> file, creating its directory if needed.</summary>
    /// <param name="path">The file to write.</param>
    /// <param name="entries">The entries to write.</param>
    public static void Write(string path, IEnumerable<ReswEntry> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, ToXml(entries), Utf8WithBom);
    }

    /// <summary>Writes an empty but valid <c>.resw</c> file, the way the WinUI template ships one.</summary>
    /// <param name="path">The file to write.</param>
    public static void WriteEmpty(string path) => Write(path, []);

    private static string EscapeText(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);

    private static string EscapeAttribute(string value) => EscapeText(value)
        .Replace("\"", "&quot;", StringComparison.Ordinal);
}
