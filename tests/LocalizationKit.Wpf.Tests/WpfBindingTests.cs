using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using LocalizationKit.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizationKit.Wpf.Tests;

/// <summary>
/// Real WPF elements with real bindings, because the claim being tested is that a value follows a
/// language change — and that is the framework's work, not this library's. Asserting on the string
/// the extension returns would prove nothing about it.
/// </summary>
[Collection("wpf")]
public sealed class WpfBindingTests
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

    /// <summary>Binds a TextBlock's Text the way the markup extension does.</summary>
    private static TextBlock Bound(string key)
    {
        TextBlock block = new();

        _ = block.SetBinding(
            TextBlock.TextProperty,
            new Binding($"[{key}]") { Source = LocalizationSource.Current, Mode = BindingMode.OneWay });

        return block;
    }

    [Fact]
    public void A_bound_value_reads_the_translation()
    {
        Sta.Run(async () =>
        {
            (ServiceProvider services, _) = await StartAsync();
            using (services)
            {
                Assert.Equal("Save", Bound("Common.Save").Text);
            }
        });
    }

    [Fact]
    public void A_bound_value_follows_a_language_change()
    {
        Sta.Run(async () =>
        {
            // The whole reason the extension returns a binding instead of a string.
            (ServiceProvider services, ILocalizationContext context) = await StartAsync();
            using (services)
            {
                TextBlock block = Bound("Common.Save");
                Assert.Equal("Save", block.Text);

                await context.SetCultureAsync("it-IT");

                Assert.Equal("Salva", block.Text);
            }
        });
    }

    [Fact]
    public void Every_bound_value_follows_it_at_once()
    {
        Sta.Run(async () =>
        {
            (ServiceProvider services, ILocalizationContext context) = await StartAsync();
            using (services)
            {
                TextBlock save = Bound("Common.Save");
                TextBlock label = Bound("Common.Label");

                await context.SetCultureAsync("it-IT");

                Assert.Equal("Salva", save.Text);
                Assert.Equal("Nome", label.Text);
            }
        });
    }

    [Fact]
    public void The_extension_produces_a_value_that_follows_the_culture_too()
    {
        Sta.Run(async () =>
        {
            // Through the extension itself rather than a hand-made binding, so the binding it builds is
            // the thing under test.
            (ServiceProvider services, ILocalizationContext context) = await StartAsync();
            using (services)
            {
                TextBlock block = new();
                TextExtension extension = new("Common.Save");

                object value = extension.ProvideValue(new TargetServiceProvider(block, TextBlock.TextProperty));

                // A binding returns a BindingExpression from ProvideValue, which is what XAML assigns.
                block.SetValue(TextBlock.TextProperty, value);

                Assert.Equal("Save", block.Text);

                await context.SetCultureAsync("it-IT");

                Assert.Equal("Salva", block.Text);
            }
        });
    }

    [Fact]
    public void A_string_format_wraps_the_translation()
    {
        Sta.Run(async () =>
        {
            (ServiceProvider services, _) = await StartAsync();
            using (services)
            {
                TextBlock block = new();
                TextExtension extension = new("Common.Label") { StringFormat = "{0}:" };

                block.SetValue(
                    TextBlock.TextProperty,
                    extension.ProvideValue(new TargetServiceProvider(block, TextBlock.TextProperty)));

                Assert.Equal("Name:", block.Text);
            }
        });
    }

    [Fact]
    public void Without_a_localizer_the_key_is_shown()
    {
        // The designer builds XAML without the application. A binding to nothing would show nothing,
        // which reads as a layout bug rather than a missing startup call.
        LocalizationKitXaml.Reset();

        Assert.False(LocalizationKitXaml.IsInitialized);
        Assert.Equal("Common.Save", new TextExtension("Common.Save").ProvideValue(null!));
        Assert.Equal("Save", new TextExtension("Common.Save") { DesignTimeValue = "Save" }.ProvideValue(null!));
    }

    [Fact]
    public void A_missing_key_is_refused_rather_than_shown_as_empty()
    {
        Assert.Throws<InvalidOperationException>(() => new TextExtension().ProvideValue(null!));
    }

    /// <summary>The services XAML gives a markup extension: which element and property it is for.</summary>
    private sealed class TargetServiceProvider(DependencyObject target, DependencyProperty property)
        : IServiceProvider, IProvideValueTarget
    {
        public object TargetObject => target;

        public object TargetProperty => property;

        public object? GetService(Type serviceType)
            => serviceType == typeof(IProvideValueTarget) ? this : null;
    }
}

/// <summary>
/// One at a time: the source XAML reads is static, as it has to be for a markup extension, so tests
/// that replace it cannot run beside each other.
/// </summary>
[CollectionDefinition("wpf", DisableParallelization = true)]
public sealed class WpfCollection
{
}
