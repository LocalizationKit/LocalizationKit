namespace LocalizationKit.Tests;

public sealed class IcuMessageSyntaxTests
{
    [Theory]
    [InlineData("Salva")]
    [InlineData("Bentornato, {name}!")]
    [InlineData("{count, plural, =0 {No items} one {# item} other {# items}}")]
    [InlineData("{n, plural, offset:1 =0 {nobody} one {# other} other {# others}}")]
    [InlineData("{n, selectordinal, one {#st} two {#nd} few {#rd} other {#th}}")]
    [InlineData("{gender, select, female {She} male {He} other {They}}")]
    [InlineData("{g, select, female {She has {n, plural, one {# note} other {# notes}}} other {Nobody}}")]
    [InlineData("{amount, number, ::currency/EUR}")]
    [InlineData("Expires {when, date, long}")]
    [InlineData("{value, number}")]
    [InlineData("L'articolo e' pronto")]
    [InlineData("'{'name'}'")]
    [InlineData("{n, plural, one {item} other {items}}")]
    public void Accepts_a_well_formed_pattern(string message)
    {
        bool valid = IcuMessageSyntax.TryValidate(message, out string? error);

        Assert.True(valid, error);
        Assert.Null(error);
    }

    [Fact]
    public void Rejects_a_misspelled_plural_category()
    {
        // This is the one an engine cannot catch: it renders fine by falling through to 'other'.
        bool valid = IcuMessageSyntax.TryValidate("{n, plural, oen {# item} other {# items}}", out string? error);

        Assert.False(valid);
        Assert.Contains("oen", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_plural_without_an_other_branch()
    {
        bool valid = IcuMessageSyntax.TryValidate("{n, plural, one {# item}}", out string? error);

        Assert.False(valid);
        Assert.Contains("other", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_select_without_an_other_branch()
    {
        bool valid = IcuMessageSyntax.TryValidate("{g, select, male {He} female {She}}", out string? error);

        Assert.False(valid);
        Assert.Contains("other", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{count, plural, one {# item}")]
    [InlineData("Hello {name")]
    [InlineData("Hello }name{")]
    [InlineData("{}")]
    [InlineData("{ , plural, other {x}}")]
    [InlineData("{my name}")]
    [InlineData("{n, plural, =x {bad} other {ok}}")]
    [InlineData("{n, plural, one other {ok}}")]
    [InlineData("{g, select, =1 {no} other {ok}}")]
    public void Rejects_malformed_patterns(string message)
    {
        Assert.False(IcuMessageSyntax.TryValidate(message, out string? error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Lists_arguments_including_nested_ones()
    {
        IReadOnlyList<string> names = IcuMessageSyntax.GetArgumentNames(
            "{g, select, female {She has {n, plural, other {# notes}}} other {They have {n, plural, other {# notes}}}}");

        Assert.Equal(["g", "n"], names);
    }

    [Fact]
    public void A_branch_body_is_text_not_an_argument()
    {
        // The braces around "item" delimit a branch. A linear scan would read it as an argument.
        Assert.Empty(IcuMessageSyntax.GetArgumentNames("{n, plural, one {item} other {items}}").Except(["n"]));
        Assert.Equal(["n"], IcuMessageSyntax.GetArgumentNames("{n, plural, one {item} other {items}}"));
    }

    [Fact]
    public void Quoted_braces_are_not_arguments()
    {
        Assert.Empty(IcuMessageSyntax.GetArgumentNames("'{'name'}'"));
    }

    [Fact]
    public void An_argument_with_a_style_is_listed_once()
    {
        Assert.Equal(["amount"], IcuMessageSyntax.GetArgumentNames("{amount, number, ::currency/EUR}"));
    }

    [Fact]
    public void Plural_categories_are_the_six_cldr_ones()
    {
        Assert.Equal(
            ["few", "many", "one", "other", "two", "zero"],
            IcuMessageSyntax.PluralCategories.Order(StringComparer.Ordinal));
    }
}
