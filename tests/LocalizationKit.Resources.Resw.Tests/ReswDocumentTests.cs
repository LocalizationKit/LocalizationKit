using System.Text;

namespace LocalizationKit.Resources.Resw.Tests;

/// <summary>
/// The property that makes this class worth having: a file it reads and writes back unchanged is the
/// same bytes it was. These files sit in source control beside a project Visual Studio also writes,
/// so a tool that reformats them turns a one-word change into a diff of every line.
/// </summary>
public sealed class ReswDocumentTests
{
    /// <summary>
    /// The shape Visual Studio produces, including the part that catches anything reading these files
    /// with a pattern: the documentation comment contains example &lt;data&gt; elements.
    /// </summary>
    private const string File2Space =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
        "<root>\r\n" +
        "  <!-- \r\n" +
        "    Microsoft ResX Schema, Version 2.0\r\n" +
        "    Example:\r\n" +
        "    <data name=\"Name1\"><value>this is my long string</value><comment>this is a comment</comment></data>\r\n" +
        "    <data name=\"Color1\" type=\"System.Drawing.Color, System.Drawing\">Blue</data>\r\n" +
        "  -->\r\n" +
        "  <resheader name=\"version\">\r\n" +
        "    <value>2.0</value>\r\n" +
        "  </resheader>\r\n" +
        "  <data name=\"Common.Save\" xml:space=\"preserve\">\r\n" +
        "    <value>Save</value>\r\n" +
        "  </data>\r\n" +
        "  <data name=\"Login.Hint\" xml:space=\"preserve\">\r\n" +
        "    <value>Read the &lt;a id=\"help\"&gt;guide&lt;/a&gt; or \"ask\".</value>\r\n" +
        "    <comment>Link markup, and quotation marks that stay as they are</comment>\r\n" +
        "  </data>\r\n" +
        "  <data name=\"Logo\" type=\"System.Drawing.Bitmap, System.Drawing\">\r\n" +
        "    <value>logo.png</value>\r\n" +
        "  </data>\r\n" +
        "</root>";

    [Fact]
    public void A_document_read_and_written_back_is_unchanged()
    {
        Assert.Equal(File2Space, ReswDocument.Parse(File2Space).ToText());
    }

    [Fact]
    public void The_comments_example_entries_are_not_mistaken_for_entries()
    {
        // Name1 and Color1 live inside the documentation comment. Reading them as entries would both
        // invent keys and cut the header short.
        IReadOnlyList<ReswEntry> entries = ReswDocument.Parse(File2Space).Entries;

        Assert.Equal(["Common.Save", "Login.Hint"], entries.Select(entry => entry.Name));
    }

    [Fact]
    public void An_entry_that_is_not_a_string_is_kept_as_it_was()
    {
        // The bitmap is not an entry this library understands, and the file still has to come back
        // with it in place.
        ReswDocument document = ReswDocument.Parse(File2Space);
        document.Set("Common.Save", "Salva");

        Assert.Contains("System.Drawing.Bitmap", document.ToText(), StringComparison.Ordinal);
        Assert.Contains("<value>logo.png</value>", document.ToText(), StringComparison.Ordinal);
    }

    [Fact]
    public void Changing_one_value_changes_one_value()
    {
        ReswDocument document = ReswDocument.Parse(File2Space);

        Assert.True(document.Set("Common.Save", "Salva"));
        Assert.Equal(File2Space.Replace("<value>Save</value>", "<value>Salva</value>", StringComparison.Ordinal), document.ToText());
    }

    [Fact]
    public void Setting_the_same_value_changes_nothing()
    {
        ReswDocument document = ReswDocument.Parse(File2Space);

        Assert.False(document.Set("Common.Save", "Save"));
    }

    [Fact]
    public void A_new_entry_keeps_the_files_own_indentation()
    {
        ReswDocument document = ReswDocument.Parse(File2Space);
        document.Set("Common.Cancel", "Annulla", "A note");

        string text = document.ToText();

        Assert.Contains("  <data name=\"Common.Cancel\" xml:space=\"preserve\">\r\n", text, StringComparison.Ordinal);
        Assert.Contains("    <value>Annulla</value>\r\n", text, StringComparison.Ordinal);
        Assert.Contains("    <comment>A note</comment>\r\n", text, StringComparison.Ordinal);

        // Before the closing tag, not after it.
        Assert.EndsWith("  </data>\r\n</root>", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_quotation_mark_in_text_is_left_alone_and_one_in_a_name_is_escaped()
    {
        ReswDocument document = ReswDocument.Parse(File2Space);
        document.Set("Common.Save", "He said \"no\"");

        string text = document.ToText();

        Assert.Contains("<value>He said \"no\"</value>", text, StringComparison.Ordinal);
        Assert.Contains("&lt;a id=\"help\"&gt;", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_four_space_files_this_library_writes_also_round_trip()
    {
        // ReswFile.Write produces the Visual Studio template's own four-space shape; the document has
        // to keep whichever it is given.
        string written = ReswFile.ToXml([new ReswEntry("Common.Save", "Save"), new ReswEntry("B", "b", "note")]);

        Assert.Equal(written, ReswDocument.Parse(written).ToText());
    }

    [Fact]
    public void A_file_on_disk_round_trips_with_its_byte_order_mark()
    {
        using TempDirectory directory = new();
        string path = Path.Combine(directory.Path, "Resources.resw");
        System.IO.File.WriteAllText(path, File2Space, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        byte[] before = System.IO.File.ReadAllBytes(path);

        ReswDocument document = ReswDocument.Load(path);
        document.Save(path);

        Assert.Equal(before, System.IO.File.ReadAllBytes(path));
    }

    [Fact]
    public void A_document_with_no_entries_is_refused()
    {
        Assert.Throws<FormatException>(() => ReswDocument.Parse("<?xml version=\"1.0\"?>\r\n<root>\r\n</root>"));
    }
}
