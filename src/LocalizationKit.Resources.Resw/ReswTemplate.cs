namespace LocalizationKit;

/// <summary>
/// The boilerplate every <c>.resw</c> file carries: the XML declaration and the embedded XSD that
/// Visual Studio and the resource designer expect to find.
/// </summary>
/// <remarks>
/// Taken verbatim from the file the WinUI project template produces, down to the CRLF line endings and
/// the trailing spaces after <c>&lt;root&gt;</c>, so a file this library writes and a file Visual
/// Studio writes do not differ.
/// </remarks>
internal static class ReswTemplate
{
    /// <summary>The lines preceding the first <c>&lt;data&gt;</c> element.</summary>
    private static readonly string[] HeaderLines =
    [
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
        "<root>  ",
        "    <xsd:schema id=\"root\" xmlns=\"\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:msdata=\"urn:schemas-microsoft-com:xml-msdata\">",
        "        <xsd:element name=\"root\" msdata:IsDataSet=\"true\">",
        "            <xsd:complexType>",
        "                <xsd:choice maxOccurs=\"unbounded\">",
        "                    <xsd:element name=\"data\">",
        "                        <xsd:complexType>",
        "                            <xsd:sequence>",
        "                                <xsd:element name=\"value\" type=\"xsd:string\" minOccurs=\"0\" msdata:Ordinal=\"1\" />",
        "                                <xsd:element name=\"comment\" type=\"xsd:string\" minOccurs=\"0\" msdata:Ordinal=\"2\" />",
        "                            </xsd:sequence>",
        "                            <xsd:attribute name=\"name\" type=\"xsd:string\" msdata:Ordinal=\"1\" />",
        "                            <xsd:attribute name=\"type\" type=\"xsd:string\" msdata:Ordinal=\"3\" />",
        "                            <xsd:attribute name=\"mimetype\" type=\"xsd:string\" msdata:Ordinal=\"4\" />",
        "                        </xsd:complexType>",
        "                    </xsd:element>",
        "                    <xsd:element name=\"resheader\">",
        "                        <xsd:complexType>",
        "                            <xsd:sequence>",
        "                                <xsd:element name=\"value\" type=\"xsd:string\" minOccurs=\"0\" msdata:Ordinal=\"1\" />",
        "                            </xsd:sequence>",
        "                            <xsd:attribute name=\"name\" type=\"xsd:string\" use=\"required\" />",
        "                        </xsd:complexType>",
        "                    </xsd:element>",
        "                </xsd:choice>",
        "            </xsd:complexType>",
        "        </xsd:element>",
        "    </xsd:schema>",
        "    <resheader name=\"resmimetype\">",
        "        <value>text/microsoft-resx</value>",
        "    </resheader>",
        "    <resheader name=\"version\">",
        "        <value>1.3</value>",
        "    </resheader>",
        "    <resheader name=\"reader\">",
        "        <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=2.0.3500.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>",
        "    </resheader>",
        "    <resheader name=\"writer\">",
        "        <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=2.0.3500.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>",
        "    </resheader>",
        ""
    ];

    private static readonly string HeaderText = string.Join("\r\n", HeaderLines);

    /// <summary>Gets the preamble, up to and including the newline before the first entry.</summary>
    public static string Header => HeaderText;

    /// <summary>The closing element, with no trailing newline, matching how Visual Studio saves the file.</summary>
    public const string Footer = "</root>";

    /// <summary>Gets an empty but valid <c>.resw</c> document.</summary>
    /// <returns>The document text.</returns>
    public static string Empty() => Header + Footer;
}
