using System.Xml;
using System.Xml.Linq;

namespace LocalizationKit.Tooling;

/// <summary>Reads and writes XLIFF 1.2 documents.</summary>
/// <remarks>
/// Version 1.2 rather than 2.0 on purpose: it is the one every translation tool in use reads, and the
/// newer version buys nothing for a catalogue this shape. The key is the <c>trans-unit</c> id, the
/// source text and the translation are <c>source</c> and <c>target</c>, and the translator note is a
/// <c>note</c>. A target with nothing in it is marked <c>needs-translation</c>, which is what a tool
/// filters its work queue by.
/// </remarks>
public static class XliffFile
{
    private static readonly XNamespace Ns = "urn:oasis:names:tc:xliff:document:1.2";

    /// <summary>Renders a document.</summary>
    /// <param name="units">The strings to hand over.</param>
    /// <param name="sourceCulture">The culture the keys were authored in.</param>
    /// <param name="targetCulture">The culture being translated into.</param>
    /// <param name="original">What the strings came from, for the <c>original</c> attribute.</param>
    /// <returns>The document text.</returns>
    public static string Write(
        IEnumerable<TransferUnit> units,
        string sourceCulture,
        string targetCulture,
        string original = "Resources.resw")
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCulture);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetCulture);

        XElement body = new(Ns + "body");

        foreach (TransferUnit unit in units)
        {
            XElement element = new(
                Ns + "trans-unit",
                new XAttribute("id", unit.Key),
                new XAttribute("resname", unit.Key),
                new XElement(Ns + "source", unit.Source),
                new XElement(
                    Ns + "target",
                    new XAttribute("state", string.IsNullOrEmpty(unit.Target) ? "needs-translation" : "translated"),
                    unit.Target));

            if (!string.IsNullOrEmpty(unit.Comment))
            {
                element.Add(new XElement(Ns + "note", new XAttribute("from", "developer"), unit.Comment));
            }

            body.Add(element);
        }

        XDocument document = new(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(
                Ns + "xliff",
                new XAttribute("version", "1.2"),
                new XElement(
                    Ns + "file",
                    new XAttribute("original", original),
                    new XAttribute("source-language", sourceCulture),
                    new XAttribute("target-language", targetCulture),
                    new XAttribute("datatype", "plaintext"),
                    body)));

        using StringWriter text = new();
        using XmlWriter writer = XmlWriter.Create(
            text,
            new XmlWriterSettings { Indent = true, IndentChars = "  ", Encoding = System.Text.Encoding.UTF8 });

        document.Save(writer);
        writer.Flush();

        return text.ToString();
    }

    /// <summary>Reads a document.</summary>
    /// <param name="text">The document text.</param>
    /// <returns>The strings it holds.</returns>
    /// <exception cref="FormatException">The document is not XLIFF this can read.</exception>
    public static IReadOnlyList<TransferUnit> Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        XDocument document;
        try
        {
            document = XDocument.Parse(text);
        }
        catch (XmlException exception)
        {
            throw new FormatException("The document is not well-formed XML: " + exception.Message, exception);
        }

        XElement? root = document.Root;
        if (root is null || root.Name.LocalName != "xliff")
        {
            throw new FormatException("The document's root is not <xliff>.");
        }

        // Namespace-agnostic, because a file that has been through a translation tool may come back
        // without one, or with 2.0's.
        List<TransferUnit> units = [];

        foreach (XElement element in root.Descendants().Where(e => e.Name.LocalName == "trans-unit"))
        {
            string key = (string?)element.Attribute("resname") ?? (string?)element.Attribute("id") ?? string.Empty;
            if (key.Length == 0)
            {
                continue;
            }

            XElement? source = element.Elements().FirstOrDefault(e => e.Name.LocalName == "source");
            XElement? target = element.Elements().FirstOrDefault(e => e.Name.LocalName == "target");
            XElement? note = element.Elements().FirstOrDefault(e => e.Name.LocalName == "note");

            units.Add(new TransferUnit(
                key,
                source?.Value ?? string.Empty,
                target?.Value ?? string.Empty,
                string.IsNullOrEmpty(note?.Value) ? null : note!.Value));
        }

        return units;
    }
}
