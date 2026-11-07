using System.Globalization;

namespace LocalizationKit.Tests;

public sealed class CultureFallbackTests
{
    [Fact]
    public void Chain_walks_parents_then_the_default_culture()
    {
        IReadOnlyList<CultureInfo> chain = CultureFallback.BuildChain(
            CultureInfo.GetCultureInfo("pt-BR"),
            CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal(["pt-BR", "pt", "en-US", "en"], chain.Select(c => c.Name));
    }

    [Fact]
    public void Chain_does_not_repeat_the_default_culture()
    {
        IReadOnlyList<CultureInfo> chain = CultureFallback.BuildChain(
            CultureInfo.GetCultureInfo("en-US"),
            CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal(["en-US", "en"], chain.Select(c => c.Name));
    }

    [Fact]
    public void Chain_excludes_the_invariant_culture()
    {
        IReadOnlyList<CultureInfo> chain = CultureFallback.BuildChain(
            CultureInfo.GetCultureInfo("it"),
            CultureInfo.GetCultureInfo("en"));

        Assert.Equal(["it", "en"], chain.Select(c => c.Name));
    }
}

public sealed class CultureNegotiatorTests
{
    private static readonly CultureInfo[] Supported =
    [
        CultureInfo.GetCultureInfo("en-US"),
        CultureInfo.GetCultureInfo("it-IT"),
    ];

    [Theory]
    [InlineData("it-IT", "it-IT")]
    [InlineData("it", "it-IT")]
    [InlineData("it-CH", "it-IT")]
    [InlineData("en-GB", "en-US")]
    public void Negotiates_to_a_shipped_culture(string requested, string expected)
    {
        CultureInfo? result = CultureNegotiator.Negotiate(CultureInfo.GetCultureInfo(requested), Supported);

        Assert.NotNull(result);
        Assert.Equal(expected, result.Name);
    }

    [Fact]
    public void Returns_null_when_nothing_matches()
    {
        Assert.Null(CultureNegotiator.Negotiate(CultureInfo.GetCultureInfo("ja-JP"), Supported));
    }

    [Fact]
    public void Accepts_anything_when_no_cultures_are_declared()
    {
        CultureInfo? result = CultureNegotiator.Negotiate(CultureInfo.GetCultureInfo("ja-JP"), []);

        Assert.NotNull(result);
        Assert.Equal("ja-JP", result.Name);
    }
}
