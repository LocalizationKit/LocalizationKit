namespace LocalizationKit.Tests;

public sealed class LocalizedMarkupTests
{
    [Fact]
    public void A_plain_string_is_one_piece_of_text()
    {
        IReadOnlyList<LocalizedMarkupSegment> segments = LocalizedMarkup.Parse("Salva");

        LocalizedMarkupSegment only = Assert.Single(segments);
        Assert.Equal(LocalizedMarkupKind.Text, only.Kind);
        Assert.Equal("Salva", only.Text);
    }

    [Fact]
    public void A_sentence_with_a_link_keeps_the_text_around_it()
    {
        IReadOnlyList<LocalizedMarkupSegment> segments =
            LocalizedMarkup.Parse("Don't have an account? <a id=\"signup\">Sign up</a>.");

        Assert.Equal(3, segments.Count);
        Assert.Equal(new LocalizedMarkupSegment(LocalizedMarkupKind.Text, "Don't have an account? "), segments[0]);
        Assert.Equal(new LocalizedMarkupSegment(LocalizedMarkupKind.Link, "Sign up", "signup"), segments[1]);
        Assert.Equal(new LocalizedMarkupSegment(LocalizedMarkupKind.Text, "."), segments[2]);
    }

    [Fact]
    public void A_link_can_carry_a_url_instead_of_an_id()
    {
        LocalizedMarkupSegment link = LocalizedMarkup
            .Parse("Read the <a href=\"https://example.com/help\">help</a>.")[1];

        Assert.Equal("https://example.com/help", link.Href);
        Assert.Null(link.Id);
        Assert.Equal("help", link.Text);
    }

    [Fact]
    public void A_link_can_carry_both_an_id_and_a_url()
    {
        LocalizedMarkupSegment link = LocalizedMarkup
            .Parse("<a id='terms' href=\"https://example.com/terms\">Terms</a>")[0];

        Assert.Equal("terms", link.Id);
        Assert.Equal("https://example.com/terms", link.Href);
    }

    [Fact]
    public void A_bare_link_tag_needs_no_attributes()
    {
        LocalizedMarkupSegment link = LocalizedMarkup.Parse("or <a>log in manually</a>")[1];

        Assert.Equal("log in manually", link.Text);
        Assert.Null(link.Id);
        Assert.Null(link.Href);
    }

    [Fact]
    public void Several_links_keep_their_own_identity()
    {
        IReadOnlyList<LocalizedMarkupSegment> segments = LocalizedMarkup.Parse(
            "Agree to the <a id=\"terms\">Terms</a> and the <a id=\"privacy\">Privacy Policy</a>.");

        Assert.Equal(
            ["terms", "privacy"],
            segments.Where(s => s.Kind == LocalizedMarkupKind.Link).Select(s => s.Id));
    }

    [Fact]
    public void Reordering_links_in_a_translation_does_not_change_which_is_which()
    {
        // The reason ids exist: Italian puts the privacy link first, and both links still work.
        IReadOnlyList<string> english = LocalizedMarkup.GetLinkIds(
            "Agree to the <a id=\"terms\">Terms</a> and the <a id=\"privacy\">Privacy</a>.");

        IReadOnlyList<string> italian = LocalizedMarkup.GetLinkIds(
            "Accetti l'<a id=\"privacy\">Informativa</a> e i <a id=\"terms\">Termini</a>.");

        Assert.Equal(["terms", "privacy"], english);
        Assert.Equal(["privacy", "terms"], italian);
        Assert.Equal(english.Order(), italian.Order());
    }

    [Theory]
    [InlineData("First\\nSecond")]
    [InlineData("First\nSecond")]
    [InlineData("First\r\nSecond")]
    public void A_line_break_is_its_own_piece(string value)
    {
        IReadOnlyList<LocalizedMarkupSegment> segments = LocalizedMarkup.Parse(value);

        Assert.Equal(
            [LocalizedMarkupKind.Text, LocalizedMarkupKind.LineBreak, LocalizedMarkupKind.Text],
            segments.Select(s => s.Kind));

        Assert.Equal("First", segments[0].Text);
        Assert.Equal("Second", segments[2].Text);
    }

    [Fact]
    public void An_angle_bracket_that_is_not_a_link_is_text()
    {
        LocalizedMarkupSegment only = Assert.Single(LocalizedMarkup.Parse("a < b and <abbr> too"));

        Assert.Equal(LocalizedMarkupKind.Text, only.Kind);
        Assert.Equal("a < b and <abbr> too", only.Text);
    }

    [Theory]
    [InlineData("<a>never closed")]
    [InlineData("closed without opening</a>")]
    [InlineData("<a>outer <a>inner</a></a>")]
    [InlineData("<a klass=\"x\">unknown attribute</a>")]
    [InlineData("<a id=unquoted>bad</a>")]
    [InlineData("<a id=\"unterminated>bad</a>")]
    public void Malformed_markup_is_an_error(string value)
    {
        Assert.Throws<FormatException>(() => LocalizedMarkup.Parse(value));
    }

    [Fact]
    public void TryParse_reports_the_problem_instead_of_throwing()
    {
        bool parsed = LocalizedMarkup.TryParse("<a>never closed", out IReadOnlyList<LocalizedMarkupSegment> segments, out string? error);

        Assert.False(parsed);
        Assert.Empty(segments);
        Assert.NotNull(error);
    }

    [Fact]
    public void Markup_is_detected_without_parsing()
    {
        Assert.False(LocalizedMarkup.ContainsMarkup("Just text"));
        Assert.True(LocalizedMarkup.ContainsMarkup("A <a>link</a>"));
        Assert.True(LocalizedMarkup.ContainsMarkup("Two\\nlines"));
    }

    [Fact]
    public void Ids_are_listed_only_for_links_that_have_one()
    {
        IReadOnlyList<string> ids = LocalizedMarkup.GetLinkIds(
            "<a id=\"a\">one</a> <a href=\"https://example.com\">two</a> <a>three</a>");

        Assert.Equal(["a"], ids);
    }
}
