using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace LocalizationKit.Tooling;

/// <summary>
/// Maintains the <c>PRIResource</c> designer entries Visual Studio keeps in a project's
/// <c>.csproj.user</c> file.
/// </summary>
/// <remarks>
/// <para>
/// The Windows App SDK targets already glob <c>**/*.resw</c> into <c>PRIResource</c>, so entries use
/// <c>Update</c> and never <c>Include</c>: an <c>Include</c> would add a second item for the same
/// file. Paths are written relative to the project directory with backslash separators, which is what
/// Visual Studio writes, so the file does not churn when both sides touch it.
/// </para>
/// <para>
/// The file is edited, never regenerated. It also holds <c>Page</c> entries and launch settings that
/// belong to Visual Studio, and an existing entry whose <c>SubType</c> differs is left alone, because
/// a developer may have set it deliberately.
/// </para>
/// </remarks>
public static class CsprojUserFile
{
    private const string MSBuildNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";
    private const string DesignerSubType = "Designer";

    /// <summary>Plans the entries needed for a set of resource files.</summary>
    /// <param name="csprojUserPath">The <c>.csproj.user</c> file, which need not exist yet.</param>
    /// <param name="relativeReswPaths">
    /// The resource files, relative to the project directory. Separators are normalized to backslashes.
    /// </param>
    /// <returns>The change to make, or <see langword="null"/> when every entry is already present.</returns>
    /// <exception cref="XmlException">The existing file is not well-formed XML.</exception>
    public static FileChange? Plan(string csprojUserPath, IEnumerable<string> relativeReswPaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csprojUserPath);
        ArgumentNullException.ThrowIfNull(relativeReswPaths);

        string[] wanted = [.. relativeReswPaths.Select(NormalizeSeparators).Distinct(StringComparer.OrdinalIgnoreCase)];
        if (wanted.Length == 0)
        {
            return null;
        }

        if (!File.Exists(csprojUserPath))
        {
            return new FileChange(
                csprojUserPath,
                $"create with {wanted.Length} PRIResource designer {Plural(wanted.Length, "entry", "entries")}",
                CreateDocument(wanted),
                FileChangeKind.Create,
                WriteByteOrderMark: true);
        }

        string original = File.ReadAllText(csprojUserPath);
        XDocument document = XDocument.Parse(original, LoadOptions.PreserveWhitespace);
        XElement root = document.Root
            ?? throw new XmlException($"'{csprojUserPath}' has no root element.");

        XNamespace ns = root.GetDefaultNamespace();

        HashSet<string> existing = new(StringComparer.OrdinalIgnoreCase);
        foreach (XElement item in root.Descendants(ns + "PRIResource"))
        {
            string? update = item.Attribute("Update")?.Value;
            if (!string.IsNullOrEmpty(update))
            {
                existing.Add(NormalizeSeparators(update));
            }
        }

        string[] missing = [.. wanted.Where(path => !existing.Contains(path))];
        if (missing.Length == 0)
        {
            return null;
        }

        // Lift the whitespace that sits before </Project> so the new groups land inside it.
        while (root.LastNode is XText trailing && string.IsNullOrWhiteSpace(trailing.Value))
        {
            trailing.Remove();
        }

        foreach (string path in missing)
        {
            // Visual Studio writes one ItemGroup per item; matching that keeps diffs small.
            root.Add(new XText("\r\n  "));
            root.Add(CreateItemGroup(path, ns));
        }

        root.Add(new XText("\r\n"));

        return new FileChange(
            csprojUserPath,
            $"add {missing.Length} PRIResource designer {Plural(missing.Length, "entry", "entries")}: {string.Join(", ", missing)}",
            Serialize(document),
            FileChangeKind.Update,
            WriteByteOrderMark: true);
    }

    /// <summary>Normalizes a project-relative path the way Visual Studio writes it.</summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The path with backslash separators and no leading separator.</returns>
    public static string NormalizeSeparators(string path)
        => path.Replace('/', '\\').TrimStart('\\');

    private static string Plural(int count, string one, string many) => count == 1 ? one : many;

    /// <summary>
    /// Builds the item group with its indentation as explicit text nodes. It is constructed rather
    /// than parsed from a string so that it inherits the document's default namespace instead of
    /// carrying a redundant <c>xmlns</c> attribute of its own.
    /// </summary>
    private static XElement CreateItemGroup(string relativePath, XNamespace ns) => new(
        ns + "ItemGroup",
        new XText("\r\n    "),
        new XElement(
            ns + "PRIResource",
            new XAttribute("Update", relativePath),
            new XText("\r\n      "),
            new XElement(ns + "SubType", DesignerSubType),
            new XText("\r\n    ")),
        new XText("\r\n  "));

    private static string CreateDocument(IEnumerable<string> relativePaths)
    {
        StringBuilder builder = new();
        builder.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n");
        builder.Append($"<Project ToolsVersion=\"Current\" xmlns=\"{MSBuildNamespace}\">\r\n");
        builder.Append("  <PropertyGroup />\r\n");

        foreach (string path in relativePaths)
        {
            builder.Append("  <ItemGroup>\r\n");
            builder.Append($"    <PRIResource Update=\"{Escape(path)}\">\r\n");
            builder.Append($"      <SubType>{DesignerSubType}</SubType>\r\n");
            builder.Append("    </PRIResource>\r\n");
            builder.Append("  </ItemGroup>\r\n");
        }

        builder.Append("</Project>");
        return builder.ToString();
    }

    private static string Escape(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);

    private static string Serialize(XDocument document)
    {
        XmlWriterSettings settings = new()
        {
            OmitXmlDeclaration = false,
            Indent = false,

            // An XML parser normalizes CRLF to LF while reading, so the preserved whitespace comes
            // back as LF and has to be written out as CRLF again to match what Visual Studio saves.
            NewLineHandling = NewLineHandling.Replace,
            NewLineChars = "\r\n",
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };

        using Utf8StringWriter writer = new();
        using (XmlWriter xml = XmlWriter.Create(writer, settings))
        {
            document.Save(xml);
        }

        return writer.ToString();
    }

    /// <summary>A string writer that reports UTF-8, so the XML declaration says <c>utf-8</c>.</summary>
    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }
}
