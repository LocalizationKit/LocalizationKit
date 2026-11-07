using System.Globalization;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizationKit.Tests;

public sealed class LocalizationFormatterTests
{
    private static async Task<ILocalizationFormatter> BuildAsync(string culture)
    {
        ServiceCollection services = new();
        services.AddLocalizationKit(options =>
        {
            options.DefaultCulture = "en-US";
            options.SupportedCultures = ["en-US", "it-IT", "de-DE"];
            options.InitialCulture = culture;
            options.FlowToThreadCulture = false;
        });

        services.AddSingleton<ILocalizationResourceProvider>(new InMemoryResourceProvider());

        ServiceProvider provider = services.BuildServiceProvider();
        await provider.InitializeLocalizationKitAsync();
        return provider.GetRequiredService<ILocalizationFormatter>();
    }

    [Fact]
    public async Task Uses_the_selected_culture_rather_than_the_thread_culture()
    {
        ILocalizationFormatter formatter = await BuildAsync("it-IT");

        Assert.Equal("it-IT", formatter.Culture.Name);
        Assert.Equal("1.234,50", formatter.Number(1234.50m, "N2"));
    }

    [Fact]
    public async Task Formats_dates_in_the_selected_culture()
    {
        ILocalizationFormatter italian = await BuildAsync("it-IT");
        ILocalizationFormatter american = await BuildAsync("en-US");
        DateOnly date = new(2026, 11, 4);

        Assert.Equal("04/11/2026", italian.Date(date));
        Assert.Equal("11/4/2026", american.Date(date));
    }

    [Fact]
    public async Task An_explicit_culture_does_not_disturb_the_selected_one()
    {
        ILocalizationFormatter formatter = await BuildAsync("it-IT");

        Assert.Equal("1.234,50", formatter.In("de-DE").Number(1234.50m, "N2"));
        Assert.Equal("1.234,50", formatter.Number(1234.50m, "N2"));
        Assert.Equal("it-IT", formatter.Culture.Name);
    }

    [Fact]
    public async Task Currency_keeps_the_culture_conventions_and_swaps_the_symbol()
    {
        ILocalizationFormatter italian = await BuildAsync("it-IT");

        string euros = italian.Currency(1234.50m, "EUR");
        string dollars = italian.Currency(1234.50m, "USD");

        Assert.Contains("1.234,50", euros, StringComparison.Ordinal);
        Assert.Contains("€", euros, StringComparison.Ordinal);

        // Italian grouping with a dollar symbol, not American formatting.
        Assert.Contains("1.234,50", dollars, StringComparison.Ordinal);
        Assert.Contains("$", dollars, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_currency_falls_back_to_its_code()
    {
        ILocalizationFormatter formatter = await BuildAsync("en-US");

        Assert.Contains("XTS", formatter.Currency(10m, "XTS"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Percent_formats_a_ratio()
    {
        ILocalizationFormatter formatter = await BuildAsync("en-US");

        Assert.Equal("15%", formatter.Percent(0.15m).Replace(" ", string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Time_uses_the_short_pattern_by_default()
    {
        ILocalizationFormatter italian = await BuildAsync("it-IT");
        TimeOnly time = new(14, 30);

        Assert.Equal(time.ToString("t", CultureInfo.GetCultureInfo("it-IT")), italian.Time(time));
    }
}
