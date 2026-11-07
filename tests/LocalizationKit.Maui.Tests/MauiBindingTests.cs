using System.Globalization;
using LocalizationKit.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;

namespace LocalizationKit.Maui.Tests;

/// <summary>
/// The assumption worth testing is not that the extension returns a binding — that is a line of code
/// — but that MAUI's binding engine re-reads an indexer binding when the source raises
/// <c>Item[]</c>. Everything here rests on that, and nothing in the library can make it true.
/// </summary>
/// <remarks>
/// It binds a <see cref="BindableObject"/> of its own rather than a Label, because a MAUI control
/// cannot be constructed in a plain test process: its static initializer reaches the platform
/// handlers, and on Windows that wants a registered WinAppSDK runtime
/// (<c>REGDB_E_CLASSNOTREG</c>). The binding engine needs none of that, and the binding engine is
/// what is under test — the same one on every platform.
/// </remarks>
[Collection("maui")]
public sealed class MauiBindingTests
{
    static MauiBindingTests()
    {
        // Before any BindableObject is constructed: see ImmediateDispatcher for why.
        Microsoft.Maui.Dispatching.DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    }

    private sealed class Resources : ILocalizationResourceProvider
    {
        public string Name => "test";

        public ValueTask<IReadOnlyDictionary<string, string>?> LoadAsync(
            CultureInfo culture,
            CancellationToken cancellationToken = default)
        {
            Dictionary<string, string> entries = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Common.Save"] = culture.Name == "it-IT" ? "Salva" : "Save",
                ["Common.Label"] = culture.Name == "it-IT" ? "Nome" : "Name",
            };

            return ValueTask.FromResult<IReadOnlyDictionary<string, string>?>(entries);
        }
    }

    private static async Task<(ServiceProvider Services, ILocalizationContext Context)> StartAsync()
    {
        ServiceCollection services = new();
        services.AddLocalizationKit(options =>
        {
            options.DefaultCulture = "en-US";
            options.SupportedCultures = ["en-US", "it-IT"];
            options.InitialCulture = "en-US";
            options.FlowToThreadCulture = false;
        });

        services.AddSingleton<ILocalizationResourceProvider>(new Resources());

        ServiceProvider provider = services.BuildServiceProvider();
        ILocalizationContext context = await provider.InitializeLocalizationKitAsync();

        _ = LocalizationKitXaml.Initialize(provider);

        return (provider, context);
    }

    /// <summary>Something bindable, without a platform behind it.</summary>
    private sealed class Probe : BindableObject
    {
        public static readonly BindableProperty TextProperty =
            BindableProperty.Create(nameof(Text), typeof(string), typeof(Probe), default(string));

        public string? Text
        {
            get => (string?)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }
    }

    private static Probe Bound(string key, string? format = null)
    {
        Probe probe = new();
        TextExtension extension = new() { Key = key, StringFormat = format };

        probe.SetBinding(Probe.TextProperty, extension.ProvideValue(null!));

        return probe;
    }

    [Fact]
    public async Task A_bound_value_reads_the_translation()
    {
        (ServiceProvider services, _) = await StartAsync();
        using (services)
        {
            Assert.Equal("Save", Bound("Common.Save").Text);
        }
    }

    [Fact]
    public async Task A_bound_value_follows_a_language_change()
    {
        // The assumption the whole design rests on: MAUI re-reads an indexer binding when the source
        // says Item[] changed.
        (ServiceProvider services, ILocalizationContext context) = await StartAsync();
        using (services)
        {
            Probe label = Bound("Common.Save");
            Assert.Equal("Save", label.Text);

            await context.SetCultureAsync("it-IT");

            Assert.Equal("Salva", label.Text);
        }
    }

    [Fact]
    public async Task Every_bound_value_follows_it_at_once()
    {
        (ServiceProvider services, ILocalizationContext context) = await StartAsync();
        using (services)
        {
            Probe save = Bound("Common.Save");
            Probe name = Bound("Common.Label");

            await context.SetCultureAsync("it-IT");

            Assert.Equal("Salva", save.Text);
            Assert.Equal("Nome", name.Text);
        }
    }

    [Fact]
    public async Task A_string_format_wraps_the_translation()
    {
        (ServiceProvider services, _) = await StartAsync();
        using (services)
        {
            Assert.Equal("Name:", Bound("Common.Label", "{0}:").Text);
        }
    }

    [Fact]
    public void Without_a_localizer_the_key_is_shown()
    {
        // A preview builds the XAML without the application.
        LocalizationKitXaml.Reset();

        Assert.Equal("Common.Save", Bound("Common.Save").Text);
        Assert.False(LocalizationKitXaml.IsInitialized);
    }

    [Fact]
    public void A_missing_key_is_refused()
    {
        Assert.Throws<InvalidOperationException>(() => new TextExtension().ProvideValue(null!));
    }
}

/// <summary>
/// One at a time: the source XAML reads is static, as it must be for a markup extension, so tests
/// that replace it cannot run beside each other.
/// </summary>
[CollectionDefinition("maui", DisableParallelization = true)]
public sealed class MauiCollection
{
}
