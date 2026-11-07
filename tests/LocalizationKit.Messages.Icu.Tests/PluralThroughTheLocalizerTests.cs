using System.Globalization;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizationKit.Messages.Icu.Tests;

/// <summary>
/// The point of the exercise: <see cref="ILocalizer.Plural"/> works once the engine is registered,
/// and no calling code changes to make that happen.
/// </summary>
public sealed class PluralThroughTheLocalizerTests
{
    private sealed class Resources : ILocalizationResourceProvider
    {
        public string Name => "test";

        public ValueTask<IReadOnlyDictionary<string, string>?> LoadAsync(
            CultureInfo culture,
            CancellationToken cancellationToken = default)
        {
            Dictionary<string, string> entries = new(StringComparer.OrdinalIgnoreCase);

            if (culture.Name == "en-US")
            {
                entries["Cart.Items"] = "{count, plural, =0 {No items} one {# item} other {# items}}";
                entries["Common.Save"] = "Save";
            }
            else if (culture.Name == "it-IT")
            {
                entries["Cart.Items"] = "{count, plural, =0 {Nessun articolo} one {# articolo} other {# articoli}}";
            }

            return ValueTask.FromResult(entries.Count == 0
                ? null
                : (IReadOnlyDictionary<string, string>?)entries);
        }
    }

    private static ServiceProvider Build(bool withIcu)
    {
        ServiceCollection services = new();

        if (withIcu)
        {
            services.AddIcuMessages();
        }

        services.AddLocalizationKit(options =>
        {
            options.DefaultCulture = "en-US";
            options.SupportedCultures = ["en-US", "it-IT"];
            options.InitialCulture = "en-US";
            options.FlowToThreadCulture = false;
        });

        services.AddSingleton<ILocalizationResourceProvider>(new Resources());
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(0, "No items")]
    [InlineData(1, "1 item")]
    [InlineData(5, "5 items")]
    public async Task Plural_resolves_the_category(int count, string expected)
    {
        await using ServiceProvider services = Build(withIcu: true);
        await services.InitializeLocalizationKitAsync();

        Assert.Equal(expected, services.GetRequiredService<ILocalizer>().Plural("Cart.Items", count));
    }

    [Fact]
    public async Task Switching_culture_switches_the_plural_rules_with_it()
    {
        await using ServiceProvider services = Build(withIcu: true);
        ILocalizationContext context = await services.InitializeLocalizationKitAsync();
        ILocalizer localizer = services.GetRequiredService<ILocalizer>();

        Assert.Equal("1 item", localizer.Plural("Cart.Items", 1));

        await context.SetCultureAsync("it-IT");

        Assert.Equal("1 articolo", localizer.Plural("Cart.Items", 1));
        Assert.Equal("3 articoli", localizer.Plural("Cart.Items", 3));
        Assert.Equal("Nessun articolo", localizer.Plural("Cart.Items", 0));
    }

    [Fact]
    public async Task An_observed_plural_string_refreshes_on_a_culture_change()
    {
        await using ServiceProvider services = Build(withIcu: true);
        ILocalizationContext context = await services.InitializeLocalizationKitAsync();
        ILocalizer localizer = services.GetRequiredService<ILocalizer>();

        ILocalizedString raw = localizer.Observe("Cart.Items");
        int notifications = 0;
        raw.PropertyChanged += (_, _) => notifications++;

        await context.SetCultureAsync("it-IT");

        Assert.Equal(1, notifications);
        Assert.StartsWith("{count, plural", raw.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Registration_order_does_not_matter()
    {
        ServiceCollection services = new();
        services.AddLocalizationKit(options =>
        {
            options.DefaultCulture = "en-US";
            options.InitialCulture = "en-US";
            options.FlowToThreadCulture = false;
        });

        services.AddIcuMessages();
        services.AddSingleton<ILocalizationResourceProvider>(new Resources());

        await using ServiceProvider provider = services.BuildServiceProvider();
        await provider.InitializeLocalizationKitAsync();

        Assert.IsType<IcuMessageFormatter>(provider.GetRequiredService<IMessageFormatter>());
        Assert.Equal("2 items", provider.GetRequiredService<ILocalizer>().Plural("Cart.Items", 2));
    }

    [Fact]
    public async Task Without_the_engine_the_same_call_refuses_rather_than_guessing()
    {
        await using ServiceProvider services = Build(withIcu: false);
        await services.InitializeLocalizationKitAsync();

        UnsupportedMessageConstructException error = Assert.Throws<UnsupportedMessageConstructException>(
            () => services.GetRequiredService<ILocalizer>().Plural("Cart.Items", 5));

        Assert.Equal("Cart.Items", error.Key);
        Assert.Equal("plural", error.Construct);
    }

    [Fact]
    public async Task Ordinary_messages_still_work_through_the_icu_engine()
    {
        await using ServiceProvider services = Build(withIcu: true);
        await services.InitializeLocalizationKitAsync();

        Assert.Equal("Save", services.GetRequiredService<ILocalizer>()["Common.Save"]);
    }
}
