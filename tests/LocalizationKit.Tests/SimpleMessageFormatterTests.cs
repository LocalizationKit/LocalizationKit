using System.Globalization;

namespace LocalizationKit.Tests;

public sealed class SimpleMessageFormatterTests
{
    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");
    private readonly SimpleMessageFormatter _formatter = new();

    private string Format(string message, params (string Key, object? Value)[] arguments)
    {
        Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, object? value) in arguments)
        {
            values[key] = value;
        }

        return _formatter.Format(message, values, Italian);
    }

    [Fact]
    public void Substitutes_named_arguments()
    {
        Assert.Equal("Bentornato, Francesco!", Format("Bentornato, {name}!", ("name", "Francesco")));
    }

    [Fact]
    public void Formats_argument_values_in_the_given_culture()
    {
        Assert.Equal("1234,5", Format("{value}", ("value", 1234.5m)));
    }

    [Fact]
    public void Treats_a_plain_apostrophe_as_text()
    {
        // The reason the ICU quoting rule matters: this is an ordinary Italian string.
        Assert.Equal("L'articolo è pronto", Format("L'articolo è pronto"));
    }

    [Fact]
    public void Quotes_braces_after_an_apostrophe()
    {
        Assert.Equal("{name}", Format("'{'name'}'", ("name", "ignored")));
    }

    [Fact]
    public void Doubles_an_apostrophe_into_one()
    {
        Assert.Equal("l'articolo", Format("l''articolo"));
    }

    [Fact]
    public void Missing_argument_is_an_error_rather_than_an_empty_string()
    {
        FormatException error = Assert.Throws<FormatException>(() => Format("Hello {name}"));
        Assert.Contains("name", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unbalanced_brace_is_an_error()
    {
        Assert.Throws<FormatException>(() => Format("Hello {name"));
    }

    [Fact]
    public void Plural_is_refused_instead_of_guessed()
    {
        UnsupportedMessageConstructException error = Assert.Throws<UnsupportedMessageConstructException>(
            () => Format("{count, plural, one {# articolo} other {# articoli}}", ("count", 2)));

        Assert.Equal("plural", error.Construct);
        Assert.Equal("count", error.ArgumentName);
    }

    [Fact]
    public void Validation_accepts_messages_written_for_the_phase_2_engine()
    {
        bool valid = _formatter.TryValidate(
            "{count, plural, =0 {Nessun articolo} one {# articolo} other {# articoli}}",
            out string? error);

        Assert.True(valid);
        Assert.Null(error);
    }

    [Fact]
    public void Validation_rejects_a_malformed_message()
    {
        bool valid = _formatter.TryValidate("{count, plural, one {# articolo}", out string? error);

        Assert.False(valid);
        Assert.NotNull(error);
    }

    [Fact]
    public void Leaves_a_message_without_placeholders_untouched()
    {
        Assert.Equal("Salva", Format("Salva"));
    }
}
