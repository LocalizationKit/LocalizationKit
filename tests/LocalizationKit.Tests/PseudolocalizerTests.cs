namespace LocalizationKit.Tests;

/// <summary>
/// The property that makes pseudolocalization safe to leave switched on: a rewritten message still
/// formats. If it did not, this would be a worse test of an application than no test at all, because
/// the failures it produced would be its own.
/// </summary>
public sealed class PseudolocalizerTests
{
    private static readonly string[] Messages =
    [
        "Save",
        "Welcome, {name}!",
        "{count, plural, one {# item} other {# items}}",
        "{count, plural, =0 {Nothing selected} one {# note selected} other {# notes selected}}",
        "{count, plural, offset:1 =0 {nobody} one {you and # other} other {you and # others}}",
        "{place, selectordinal, one {#st} two {#nd} few {#rd} other {#th}} place",
        "{gender, select, female {She} male {He} other {They}} replied",
        "{count, plural, =0 {{total, plural, one {It already has the tag} other {They all do}}} other {Added to #}}",
        "Read the <a id=\"help\">documentation</a> or <a id=\"support\">ask for help</a>.",
        "A line\\nand another",
        "It''s quoted, and so is '{this}'",
        "L'articolo è pronto",
    ];

    [Theory]
    [MemberData(nameof(Cases))]
    public void A_rewritten_message_is_still_a_valid_message(string message)
    {
        string rewritten = Pseudolocalizer.Transform(message);

        Assert.True(
            IcuMessageSyntax.TryValidate(rewritten, out string? error),
            $"{rewritten} is not valid: {error}");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void A_rewritten_message_asks_for_the_same_arguments(string message)
    {
        // The arguments are what the code supplies; renaming one would break the call site rather
        // than the layout it is meant to test.
        Assert.Equal(
            IcuMessageSyntax.GetArgumentNames(message),
            IcuMessageSyntax.GetArgumentNames(Pseudolocalizer.Transform(message)));
    }

    public static TheoryData<string> Cases()
    {
        TheoryData<string> cases = [];
        foreach (string message in Messages)
        {
            cases.Add(message);
        }

        return cases;
    }

    [Fact]
    public void Plain_text_is_accented_padded_and_bracketed()
    {
        string rewritten = Pseudolocalizer.Transform("Save");

        Assert.StartsWith("⟦", rewritten, StringComparison.Ordinal);
        Assert.EndsWith("⟧", rewritten, StringComparison.Ordinal);
        Assert.DoesNotContain("Save", rewritten, StringComparison.Ordinal);
        Assert.Contains("∙", rewritten, StringComparison.Ordinal);

        // Longer than it was, which is the point: a label that cannot grow is now visibly clipped.
        Assert.True(rewritten.Length > "Save".Length + 2, rewritten);
    }

    [Fact]
    public void An_arguments_name_is_left_alone()
    {
        string rewritten = Pseudolocalizer.Transform("Welcome, {name}!");

        Assert.Contains("{name}", rewritten, StringComparison.Ordinal);
        Assert.DoesNotContain("Welcome", rewritten, StringComparison.Ordinal);
    }

    [Fact]
    public void A_plurals_keywords_are_left_alone_and_its_branches_are_not()
    {
        string rewritten = Pseudolocalizer.Transform("{count, plural, one {# item} other {# items}}");

        // The formatter reads these.
        Assert.Contains("{count, plural, one {", rewritten, StringComparison.Ordinal);
        Assert.Contains("other {", rewritten, StringComparison.Ordinal);
        Assert.Contains("#", rewritten, StringComparison.Ordinal);

        // A reader reads these, and the long words in a translation live here.
        Assert.DoesNotContain("item", rewritten, StringComparison.Ordinal);
    }

    [Fact]
    public void Link_markup_keeps_its_tags_and_loses_its_text()
    {
        string rewritten = Pseudolocalizer.Transform("Read the <a id=\"help\">documentation</a>.");

        Assert.Contains("<a id=\"help\">", rewritten, StringComparison.Ordinal);
        Assert.Contains("</a>", rewritten, StringComparison.Ordinal);
        Assert.DoesNotContain("documentation", rewritten, StringComparison.Ordinal);

        // And it still parses as markup, with the link intact.
        Assert.True(LocalizedMarkup.TryParse(rewritten, out IReadOnlyList<LocalizedMarkupSegment> segments, out _));
        Assert.Contains(segments, segment => segment.Id == "help");
    }

    [Fact]
    public void An_empty_message_is_left_as_it_is()
    {
        // An empty resource is an untranslated one, and bracketing it would only hide that.
        Assert.Equal(string.Empty, Pseudolocalizer.Transform(string.Empty));
    }
}
