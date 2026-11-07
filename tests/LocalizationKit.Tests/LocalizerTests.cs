using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizationKit.Tests;

public sealed class LocalizerTests
{
    private static ServiceProvider Build(
        InMemoryResourceProvider provider,
        Action<LocalizationKitOptions>? configure = null)
    {
        ServiceCollection services = new();
        services.AddLocalizationKit(options =>
        {
            options.DefaultCulture = "en-US";
            options.SupportedCultures = ["en-US", "it-IT"];
            options.InitialCulture = "en-US";
            options.FlowToThreadCulture = false;
            configure?.Invoke(options);
        });

        services.AddSingleton<ILocalizationResourceProvider>(provider);
        return services.BuildServiceProvider();
    }

    private static InMemoryResourceProvider TwoCultures() => new InMemoryResourceProvider()
        .With("en-US", ("Common.Save", "Save"), ("Welcome", "Welcome, {name}!"), ("OnlyEnglish", "English"))
        .With("it-IT", ("Common.Save", "Salva"), ("Welcome", "Bentornato, {name}!"));

    [Fact]
    public async Task Resolves_the_initial_culture()
    {
        await using ServiceProvider services = Build(TwoCultures());
        await services.InitializeLocalizationKitAsync();

        ILocalizer localizer = services.GetRequiredService<ILocalizer>();

        Assert.Equal("Save", localizer["Common.Save"]);
    }

    [Fact]
    public async Task Switching_culture_changes_what_lookups_return()
    {
        await using ServiceProvider services = Build(TwoCultures());
        ILocalizationContext context = await services.InitializeLocalizationKitAsync();
        ILocalizer localizer = services.GetRequiredService<ILocalizer>();

        await context.SetCultureAsync("it-IT");

        Assert.Equal("Salva", localizer["Common.Save"]);
        Assert.Equal("it-IT", context.UICulture.Name);
    }

    [Fact]
    public async Task Falls_back_to_the_default_culture_for_an_untranslated_key()
    {
        await using ServiceProvider services = Build(TwoCultures());
        ILocalizationContext context = await services.InitializeLocalizationKitAsync();
        ILocalizer localizer = services.GetRequiredService<ILocalizer>();

        await context.SetCultureAsync("it-IT");

        Assert.Equal("English", localizer["OnlyEnglish"]);
    }

    [Fact]
    public async Task Missing_key_returns_the_key_by_default()
    {
        await using ServiceProvider services = Build(TwoCultures());
        await services.InitializeLocalizationKitAsync();

        Assert.Equal("Nope.Missing", services.GetRequiredService<ILocalizer>()["Nope.Missing"]);
    }

    [Fact]
    public async Task Missing_key_can_throw_instead()
    {
        await using ServiceProvider services = Build(
            TwoCultures(),
            options => options.FallbackBehavior = FallbackBehavior.Throw);

        await services.InitializeLocalizationKitAsync();

        Assert.Throws<MissingResourceException>(() => services.GetRequiredService<ILocalizer>()["Nope.Missing"]);
    }

    [Fact]
    public async Task Missing_key_is_reported_to_the_diagnostics_callback()
    {
        List<string> missing = [];
        await using ServiceProvider services = Build(
            TwoCultures(),
            options => options.OnMissingKey = info => missing.Add(info.Key));

        await services.InitializeLocalizationKitAsync();
        _ = services.GetRequiredService<ILocalizer>()["Nope.Missing"];

        Assert.Equal(["Nope.Missing"], missing);
    }

    [Fact]
    public async Task Format_substitutes_arguments_from_an_anonymous_object()
    {
        await using ServiceProvider services = Build(TwoCultures());
        ILocalizationContext context = await services.InitializeLocalizationKitAsync();
        ILocalizer localizer = services.GetRequiredService<ILocalizer>();

        await context.SetCultureAsync("it-IT");

        Assert.Equal("Bentornato, Francesco!", localizer.Format("Welcome", new { name = "Francesco" }));
    }

    [Fact]
    public async Task Plural_reports_the_key_it_could_not_format()
    {
        InMemoryResourceProvider provider = TwoCultures();
        provider.Set("en-US", "Cart.Items", "{count, plural, one {# item} other {# items}}");

        await using ServiceProvider services = Build(provider);
        await services.InitializeLocalizationKitAsync();

        UnsupportedMessageConstructException error = Assert.Throws<UnsupportedMessageConstructException>(
            () => services.GetRequiredService<ILocalizer>().Plural("Cart.Items", 5));

        Assert.Equal("Cart.Items", error.Key);
        Assert.Equal("plural", error.Construct);
    }

    [Fact]
    public async Task Observed_string_refreshes_when_the_culture_changes()
    {
        await using ServiceProvider services = Build(TwoCultures());
        ILocalizationContext context = await services.InitializeLocalizationKitAsync();
        ILocalizer localizer = services.GetRequiredService<ILocalizer>();

        ILocalizedString save = localizer.Observe("Common.Save");
        List<string?> changed = [];
        save.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.Equal("Save", save.Value);

        await context.SetCultureAsync("it-IT");

        Assert.Equal("Salva", save.Value);
        Assert.Equal([nameof(ILocalizedString.Value)], changed);
    }

    [Fact]
    public async Task Observing_the_same_key_twice_returns_the_same_instance()
    {
        await using ServiceProvider services = Build(TwoCultures());
        await services.InitializeLocalizationKitAsync();
        ILocalizer localizer = services.GetRequiredService<ILocalizer>();

        Assert.Same(localizer.Observe("Common.Save"), localizer.Observe("Common.Save"));
    }

    [Fact]
    public async Task Reload_picks_up_edited_resources_and_notifies()
    {
        InMemoryResourceProvider provider = TwoCultures();
        await using ServiceProvider services = Build(provider);
        ILocalizationContext context = await services.InitializeLocalizationKitAsync();
        ILocalizer localizer = services.GetRequiredService<ILocalizer>();

        ILocalizedString save = localizer.Observe("Common.Save");
        int notifications = 0;
        save.PropertyChanged += (_, _) => notifications++;

        provider.Set("en-US", "Common.Save", "Store");
        await context.ReloadAsync();

        Assert.Equal("Store", save.Value);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public async Task The_default_culture_is_available_as_a_fallback_choice()
    {
        await using ServiceProvider services = Build(TwoCultures());
        ILocalizationContext context = await services.InitializeLocalizationKitAsync();

        await context.SetCultureAsync("it-IT");

        Assert.Equal("en-US", context.DefaultCulture.Name);
    }

    [Fact]
    public async Task Unsupported_culture_is_refused()
    {
        await using ServiceProvider services = Build(TwoCultures());
        ILocalizationContext context = await services.InitializeLocalizationKitAsync();

        await Assert.ThrowsAsync<CultureNotSupportedException>(() => context.SetCultureAsync("ja-JP"));
    }

    [Fact]
    public async Task Resolving_before_initialization_is_an_error()
    {
        await using ServiceProvider services = Build(TwoCultures());
        ILocalizer localizer = services.GetRequiredService<ILocalizer>();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => localizer["Common.Save"]);
        Assert.Contains("InitializeLocalizationKitAsync", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resources_are_parsed_once_per_culture()
    {
        InMemoryResourceProvider provider = TwoCultures();
        await using ServiceProvider services = Build(provider);
        ILocalizationContext context = await services.InitializeLocalizationKitAsync();

        int afterInitialize = provider.LoadCount;
        await context.SetCultureAsync("it-IT");
        await context.SetCultureAsync("en-US");
        await context.SetCultureAsync("it-IT");

        // en-US and en were loaded during initialization; only it-IT and it are new.
        Assert.Equal(afterInitialize + 2, provider.LoadCount);
    }

    [Fact]
    public async Task Earlier_providers_win()
    {
        InMemoryResourceProvider overrides = new InMemoryResourceProvider("overrides")
            .With("en-US", ("Common.Save", "Keep"));

        ServiceCollection services = new();
        services.AddLocalizationKit(options =>
        {
            options.DefaultCulture = "en-US";
            options.InitialCulture = "en-US";
            options.FlowToThreadCulture = false;
        });

        services.AddSingleton<ILocalizationResourceProvider>(overrides);
        services.AddSingleton<ILocalizationResourceProvider>(TwoCultures());

        await using ServiceProvider provider = services.BuildServiceProvider();
        await provider.InitializeLocalizationKitAsync();

        Assert.Equal("Keep", provider.GetRequiredService<ILocalizer>()["Common.Save"]);
    }
}
