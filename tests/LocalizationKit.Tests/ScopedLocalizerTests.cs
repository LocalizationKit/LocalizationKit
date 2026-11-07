using System.Globalization;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizationKit.Tests;

/// <summary>
/// A scope is a shorter way of writing a key, not a second namespace: the key space stays flat, and
/// a key the scope does not have falls through to the bare key so that shared keys stay shared.
/// </summary>
public sealed class ScopedLocalizerTests
{
    private sealed class Resources : ILocalizationResourceProvider
    {
        public string Name => "test";

        public ValueTask<IReadOnlyDictionary<string, string>?> LoadAsync(
            CultureInfo culture,
            CancellationToken cancellationToken = default)
        {
            Dictionary<string, string> entries = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Common.Cancel"] = "Cancel",
                ["Checkout.Save"] = "Place the order",
                ["Checkout.Items"] = "{count} in the basket",
                ["Checkout.Welcome"] = "Welcome, {name}!",
                ["Checkout.Review.Title"] = "Review",
                ["Save"] = "Save",
            };

            return ValueTask.FromResult<IReadOnlyDictionary<string, string>?>(entries);
        }
    }

    private static async Task<ILocalizer> BuildAsync()
    {
        ServiceCollection services = new();
        services.AddLocalizationKit(options =>
        {
            options.DefaultCulture = "en-US";
            options.InitialCulture = "en-US";
            options.FlowToThreadCulture = false;
        });

        services.AddSingleton<ILocalizationResourceProvider>(new Resources());

        ServiceProvider provider = services.BuildServiceProvider();
        await provider.InitializeLocalizationKitAsync();
        return provider.GetRequiredService<ILocalizer>();
    }

    [Fact]
    public async Task A_scoped_key_is_read_under_the_prefix()
    {
        ILocalizer checkout = (await BuildAsync()).Scoped("Checkout");

        Assert.Equal("Place the order", checkout["Save"]);
    }

    [Fact]
    public async Task The_unscoped_localizer_is_unaffected()
    {
        ILocalizer localizer = await BuildAsync();

        Assert.Equal("Save", localizer["Save"]);
        Assert.Equal("Place the order", localizer.Scoped("Checkout")["Save"]);
        Assert.Equal("Save", localizer["Save"]);
    }

    [Fact]
    public async Task A_key_the_scope_does_not_have_falls_through()
    {
        // What makes a scope safe to add to code that already works, and what keeps the shared keys
        // from having to be duplicated under every prefix.
        ILocalizer checkout = (await BuildAsync()).Scoped("Checkout");

        Assert.Equal("Cancel", checkout["Common.Cancel"]);
    }

    [Fact]
    public async Task Scopes_nest()
    {
        ILocalizer review = (await BuildAsync()).Scoped("Checkout").Scoped("Review");

        Assert.Equal("Review", review["Title"]);
    }

    [Fact]
    public async Task A_trailing_dot_is_the_same_prefix()
    {
        ILocalizer localizer = await BuildAsync();

        Assert.Equal(localizer.Scoped("Checkout")["Save"], localizer.Scoped("Checkout.")["Save"]);
    }

    [Fact]
    public async Task Formatting_and_plurals_go_through_the_scope()
    {
        ILocalizer checkout = (await BuildAsync()).Scoped("Checkout");

        // The simple formatter substitutes the count; the plural categories are the ICU package's job
        // and have their own tests there.
        Assert.Equal("4 in the basket", checkout.Plural("Items", 4));
        Assert.Equal(
            "Welcome, Francesco!",
            checkout.Format("Welcome", new Dictionary<string, object?> { ["name"] = "Francesco" }));
    }

    [Fact]
    public async Task TryGet_reports_what_resolves_either_way()
    {
        ILocalizer checkout = (await BuildAsync()).Scoped("Checkout");

        Assert.True(checkout.TryGet("Save", out string? scoped));
        Assert.Equal("Place the order", scoped);

        Assert.True(checkout.TryGet("Common.Cancel", out _));
        Assert.False(checkout.TryGet("Nope", out _));
    }

    [Fact]
    public async Task GetAll_returns_the_scopes_own_keys_without_the_prefix()
    {
        IReadOnlyDictionary<string, string> entries = (await BuildAsync()).Scoped("Checkout").GetAll();

        Assert.Equal("Place the order", entries["Save"]);
        Assert.Contains("Review.Title", entries.Keys);
        Assert.DoesNotContain("Common.Cancel", entries.Keys);
        Assert.DoesNotContain("Checkout.Save", entries.Keys);
    }

    [Fact]
    public async Task An_observable_string_follows_the_scope()
    {
        ILocalizedString observed = (await BuildAsync()).Scoped("Checkout").Observe("Save");

        Assert.Equal("Place the order", observed.Value);
    }

    [Fact]
    public async Task An_empty_prefix_is_refused()
    {
        ILocalizer localizer = await BuildAsync();

        Assert.Throws<ArgumentException>(() => localizer.Scoped(" "));
    }
}
