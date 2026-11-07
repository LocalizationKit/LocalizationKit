using System.Text;
using System.Xml;

namespace LocalizationKit.Resources.Resw.Tests;

public sealed class ReswFileTests
{
    [Fact]
    public void An_empty_file_is_valid_xml_and_has_no_entries()
    {
        using TempDirectory temp = new();
        string file = temp.Combine("Strings", "en-US", "Resources.resw");

        ReswFile.WriteEmpty(file);

        Assert.Empty(ReswFile.Read(file));
        using XmlReader reader = XmlReader.Create(file);
        while (reader.Read())
        {
            // Reading to the end is the assertion: malformed XML throws.
        }
    }

    [Fact]
    public void Files_are_written_the_way_visual_studio_writes_them()
    {
        using TempDirectory temp = new();
        string file = temp.Combine("Strings", "en-US", "Resources.resw");

        ReswFile.Write(file, [new ReswEntry("Common.Save", "Save")]);
        byte[] bytes = File.ReadAllBytes(file);
        string text = Encoding.UTF8.GetString(bytes);

        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.DoesNotContain('\n', text.Replace("\r\n", string.Empty, StringComparison.Ordinal));
        Assert.EndsWith("</root>", text, StringComparison.Ordinal);
        Assert.Contains("<resheader name=\"resmimetype\">", text, StringComparison.Ordinal);
        Assert.Contains("xml:space=\"preserve\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Entries_round_trip()
    {
        using TempDirectory temp = new();
        ReswEntry[] entries =
        [
            new ReswEntry("Common.Save", "Salva"),
            new ReswEntry("Welcome.Description", "Bentornato, {name}!", "{name} is the display name."),
            new ReswEntry("Cart.Items", "{count, plural, one {# articolo} other {# articoli}}"),
        ];

        string file = temp.WriteResw("it-IT", entries);

        Assert.Equal(entries, ReswFile.Read(file));
    }

    [Fact]
    public void Significant_whitespace_survives()
    {
        using TempDirectory temp = new();
        string file = temp.WriteResw("en-US", new ReswEntry("Separator", " of "));

        Assert.Equal(" of ", ReswFile.Read(file)[0].Value);
    }

    [Fact]
    public void Markup_in_values_is_escaped_and_read_back_intact()
    {
        using TempDirectory temp = new();
        string value = "Fish & <chips> \"quoted\"";

        string file = temp.WriteResw("en-US", new ReswEntry("Odd", value));

        Assert.Contains("&amp;", File.ReadAllText(file), StringComparison.Ordinal);
        Assert.Equal(value, ReswFile.Read(file)[0].Value);
    }

    [Fact]
    public void Non_string_resources_are_skipped()
    {
        using TempDirectory temp = new();
        string file = temp.Combine("Strings", "en-US", "Resources.resw");
        ReswFile.Write(file, [new ReswEntry("Text", "Hello")]);

        string text = File.ReadAllText(file).Replace(
            "</root>",
            """
                <data name="Logo" type="System.Drawing.Bitmap, System.Drawing">
                    <value>not a translation</value>
                </data>
            </root>
            """,
            StringComparison.Ordinal);

        File.WriteAllText(file, text, new UTF8Encoding(true));

        Assert.Equal(["Text"], ReswFile.Read(file).Select(e => e.Name));
    }

    [Fact]
    public void Values_are_keyed_case_insensitively_like_windows_resources()
    {
        using TempDirectory temp = new();
        string file = temp.WriteResw("en-US", new ReswEntry("Common.Save", "Save"));

        IReadOnlyDictionary<string, string> values = ReswFile.ReadValues(file);

        Assert.Equal("Save", values["common.save"]);
    }

    [Fact]
    public void An_entry_remembers_the_line_it_was_read_from()
    {
        using TempDirectory directory = new();
        string path = Path.Combine(directory.Path, "Resources.resw");
        ReswFile.Write(path, [new ReswEntry("Common.Save", "Save"), new ReswEntry("Common.Cancel", "Cancel")]);

        IReadOnlyList<ReswEntry> entries = ReswFile.Read(path);
        string[] lines = File.ReadAllLines(path);

        foreach (ReswEntry entry in entries)
        {
            Assert.NotNull(entry.Line);
            Assert.Contains(entry.Name, lines[entry.Line!.Value - 1], StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Two_entries_that_say_the_same_thing_are_equal_wherever_they_came_from()
    {
        // The line is provenance, not identity: the same key in two culture files is the same entry,
        // and a round trip through this class has to compare equal to what went into it.
        Assert.Equal(new ReswEntry("Common.Save", "Save"), new ReswEntry("Common.Save", "Save", null, 42));
        Assert.Equal(
            new ReswEntry("Common.Save", "Save", null, 7).GetHashCode(),
            new ReswEntry("Common.Save", "Save", null, 99).GetHashCode());
        Assert.NotEqual(new ReswEntry("Common.Save", "Save"), new ReswEntry("Common.Save", "Salva"));
    }
}
