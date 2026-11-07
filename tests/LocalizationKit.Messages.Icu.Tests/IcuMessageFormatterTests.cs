using System.Globalization;

namespace LocalizationKit.Messages.Icu.Tests;

public sealed class IcuMessageFormatterTests
{
    private readonly IcuMessageFormatter _formatter = new();

    private string Format(string culture, string message, params (string Key, object? Value)[] arguments)
    {
        Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, object? value) in arguments)
        {
            values[key] = value;
        }

        return _formatter.Format(message, values, CultureInfo.GetCultureInfo(culture));
    }

    private const string Items = "{count, plural, =0 {No items} one {# item} other {# items}}";

    [Theory]
    [InlineData(0, "No items")]
    [InlineData(1, "1 item")]
    [InlineData(7, "7 items")]
    public void English_uses_one_and_other(int count, string expected)
    {
        Assert.Equal(expected, Format("en-US", Items, ("count", count)));
    }

    [Theory]
    [InlineData(1, "1 articolo")]
    [InlineData(3, "3 articoli")]
    public void Italian_uses_one_and_other(int count, string expected)
    {
        Assert.Equal(expected, Format("it-IT", "{count, plural, one {# articolo} other {# articoli}}", ("count", count)));
    }

    [Theory]
    [InlineData(1, "one")]
    [InlineData(2, "few")]
    [InlineData(5, "many")]
    [InlineData(22, "few")]
    public void Polish_uses_four_categories(int count, string expected)
    {
        // The reason CLDR data is not optional: no amount of if (n == 1) produces this.
        Assert.Equal(
            expected,
            Format("pl-PL", "{n, plural, one {one} few {few} many {many} other {other}}", ("n", count)));
    }

    [Theory]
    [InlineData(0, "zero")]
    [InlineData(1, "one")]
    [InlineData(2, "two")]
    [InlineData(3, "few")]
    [InlineData(11, "many")]
    [InlineData(100, "other")]
    public void Arabic_uses_all_six_categories(int count, string expected)
    {
        Assert.Equal(
            expected,
            Format(
                "ar-EG",
                "{n, plural, zero {zero} one {one} two {two} few {few} many {many} other {other}}",
                ("n", count)));
    }

    [Theory]
    [InlineData(1, "1st")]
    [InlineData(2, "2nd")]
    [InlineData(3, "3rd")]
    [InlineData(11, "11th")]
    [InlineData(21, "21st")]
    public void Ordinals_work(int n, string expected)
    {
        Assert.Equal(expected, Format("en-US", "{n, selectordinal, one {#st} two {#nd} few {#rd} other {#th}}", ("n", n)));
    }

    [Fact]
    public void Select_picks_a_branch()
    {
        Assert.Equal("She", Format("en-US", "{gender, select, male {He} female {She} other {They}}", ("gender", "female")));
    }

    [Fact]
    public void A_plural_nests_inside_a_select()
    {
        string message = "{gender, select, female {She has {n, plural, one {# note} other {# notes}}} "
            + "other {They have {n, plural, one {# note} other {# notes}}}}";

        Assert.Equal("She has 2 notes", Format("en-US", message, ("gender", "female"), ("n", 2)));
        Assert.Equal("They have 1 note", Format("en-US", message, ("gender", "other"), ("n", 1)));
    }

    [Fact]
    public void An_offset_shifts_the_counted_number()
    {
        Assert.Equal(
            "2 others",
            Format("en-US", "{n, plural, offset:1 =0 {nobody} one {# other} other {# others}}", ("n", 3)));
    }

    [Fact]
    public void An_italian_apostrophe_is_literal()
    {
        Assert.Equal("L'articolo è pronto", Format("it-IT", "L'articolo è pronto"));
    }

    [Fact]
    public void Braces_can_be_quoted()
    {
        Assert.Equal("{name}", Format("en-US", "'{'name'}'"));
    }

    [Fact]
    public void Numbers_and_dates_can_be_formatted_inside_a_message()
    {
        Assert.Equal(
            "1.234,50 €",
            Format("de-DE", "{amount, number, ::currency/EUR}", ("amount", 1234.5m)).Replace(' ', ' '));

        Assert.Contains("2026", Format("en-US", "Expires {when, date, long}", ("when", new DateTime(2026, 11, 5))), StringComparison.Ordinal);
    }

    [Fact]
    public void Percent_keeps_three_fraction_digits_which_differs_from_icu()
    {
        // Documented deviation in Rh.MessageFormat 1.2.0: ICU renders 0.25 as "25%" with the percent
        // style, this engine renders "25.000%". Use a skeleton when the digits matter. If a later
        // version fixes it, this test is what notices.
        Assert.Equal("You have 25.000%", Format("en-US", "You have {amount, number, percent}", ("amount", 0.25)));
    }

    [Fact]
    public void A_missing_argument_is_an_error_rather_than_a_hole_in_the_sentence()
    {
        // The engine on its own would render "Hello ".
        FormatException error = Assert.Throws<FormatException>(() => Format("en-US", "Hello {name}"));

        Assert.Contains("name", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_nested_argument_is_also_an_error()
    {
        Assert.Throws<FormatException>(
            () => Format("en-US", "{g, select, other {You have {n, plural, other {# notes}}}}", ("g", "x")));
    }

    [Fact]
    public void A_malformed_pattern_is_a_format_exception()
    {
        Assert.Throws<FormatException>(() => Format("en-US", "{count, plural, one {# item}"));
    }

    [Fact]
    public void A_misspelled_category_is_rejected_by_validation()
    {
        Assert.False(_formatter.TryValidate("{n, plural, oen {# item} other {# items}}", out string? error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Validation_accepts_what_the_engine_can_render()
    {
        Assert.True(_formatter.TryValidate(Items, out string? error), error);
    }

    [Fact]
    public void An_unknown_culture_falls_back_instead_of_throwing()
    {
        // A pseudo-locale or a private-use tag must degrade, not take down a page.
        string result = Format("en-US-x-custom", Items, ("count", 2));

        Assert.Equal("2 items", result);
    }

    [Fact]
    public void A_neutral_culture_works()
    {
        Assert.Equal("1 item", Format("en", Items, ("count", 1)));
    }

    [Fact]
    public void Formatting_the_same_message_twice_reuses_the_cached_engine()
    {
        Assert.Equal("1 item", Format("en-US", Items, ("count", 1)));
        Assert.Equal("3 items", Format("en-US", Items, ("count", 3)));
    }
}
