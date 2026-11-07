using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace LocalizationKit.StringLocalizer.Tests;

/// <summary>
/// The adoption path: code written against <see cref="IStringLocalizer"/> keeps working and resolves
/// through LocalizationKit.
/// </summary>
public sealed class StringLocalizerBridgeTests
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
                entries["Common.Save"] = "Save";
                entries["Welcome.Positional"] = "Welcome, {0}!";
                entries["Welcome.Named"] = "Welcome, {name}!";
                entries["Cart.Items"] = "{count, plural, one {# item} other {# items}}";
                entries["Only.English"] = "English only";
                entries["StringLocalizerBridgeTests.Save"] = "Scoped save";
            }
            else if (culture.Name == "it-IT")
            {
                entries["Common.Save"] = "Salva";
                entries["Welcome.Positional"] = "Bentornato, {0}!";
            }

            return ValueTask.FromResult(entries.Count == 0
                ? null
                : (IReadOnlyDictionary<string, string>?)entries);
        }
    }

    private static ServiceProvider Build()
    {
        ServiceCollection services = new();
        services.AddIcuMessages();
        services.AddLocalizationKit(options =>
        {
            options.DefaultCulture = "en-US";
            options.SupportedCultures = ["en-US", "it-IT"];
            options.InitialCulture = "en-US";
            options.FlowToThreadCulture = false;
            options.PreloadSupportedCultures = true;
        });

        services.AddSingleton<ILocalizationResourceProvider>(new Resources());
        services.AddLocalizationKitStringLocalizer();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Resolves_a_string()
    {
        await using ServiceProvider services = Build();
        await services.InitializeLocalizationKitAsync();

        LocalizedString value = services.GetRequiredService<IStringLocalizer>()["Common.Save"];

        Assert.Equal("Save", value.Value);
        Assert.Equal("Common.Save", value.Name);
        Assert.False(value.ResourceNotFound);
    }

    [Fact]
    public async Task A_missing_key_is_reported_as_not_found()
    {
        await using ServiceProvider services = Build();
        await services.InitializeLocalizationKitAsync();

        // Microsoft's contract: the name is returned and the flag says it was not found. The flag is
        // accurate here because the bridge asks TryGet rather than taking the configured fallback.
        LocalizedString value = services.GetRequiredService<IStringLocalizer>()["Nope.Missing"];

        Assert.Equal("Nope.Missing", value.Value);
        Assert.True(value.ResourceNotFound);
    }

    [Fact]
    public async Task Positional_arguments_work_as_existing_call_sites_expect()
    {
        await using ServiceProvider services = Build();
        await services.InitializeLocalizationKitAsync();

        Assert.Equal(
            "Welcome, Francesco!",
            services.GetRequiredService<IStringLocalizer>()["Welcome.Positional", "Francesco"].Value);
    }

    [Fact]
    public async Task A_resource_with_named_arguments_also_works_positionally()
    {
        await using ServiceProvider services = Build();
        await services.InitializeLocalizationKitAsync();

        // The resource was migrated to ICU named arguments; the old call site did not change.
        Assert.Equal(
            "Welcome, Francesco!",
            services.GetRequiredService<IStringLocalizer>()["Welcome.Named", "Francesco"].Value);
    }

    [Fact]
    public async Task An_icu_plural_resolves_through_the_bridge()
    {
        await using ServiceProvider services = Build();
        await services.InitializeLocalizationKitAsync();

        IStringLocalizer localizer = services.GetRequiredService<IStringLocalizer>();

        Assert.Equal("1 item", localizer["Cart.Items", 1].Value);
        Assert.Equal("4 items", localizer["Cart.Items", 4].Value);
    }

    [Fact]
    public async Task The_culture_follows_the_localization_context()
    {
        await using ServiceProvider services = Build();
        ILocalizationContext context = await services.InitializeLocalizationKitAsync();
        IStringLocalizer localizer = services.GetRequiredService<IStringLocalizer>();

        await context.SetCultureAsync("it-IT");

        Assert.Equal("Salva", localizer["Common.Save"].Value);
        Assert.Equal("Bentornato, Francesco!", localizer["Welcome.Positional", "Francesco"].Value);
    }

    [Fact]
    public async Task A_culture_scope_applies_to_the_bridge_too()
    {
        await using ServiceProvider services = Build();
        ILocalizationContext context = await services.InitializeLocalizationKitAsync();
        IStringLocalizer localizer = services.GetRequiredService<IStringLocalizer>();

        using (context.BeginCultureScope("it-IT"))
        {
            Assert.Equal("Salva", localizer["Common.Save"].Value);
        }

        Assert.Equal("Save", localizer["Common.Save"].Value);
    }

    [Fact]
    public async Task GetAllStrings_can_include_or_exclude_inherited_entries()
    {
        await using ServiceProvider services = Build();
        ILocalizationContext context = await services.InitializeLocalizationKitAsync();
        IStringLocalizer localizer = services.GetRequiredService<IStringLocalizer>();

        await context.SetCultureAsync("it-IT");

        IReadOnlyList<string> withParents = [.. localizer.GetAllStrings(includeParentCultures: true).Select(s => s.Name)];
        IReadOnlyList<string> ownOnly = [.. localizer.GetAllStrings(includeParentCultures: false).Select(s => s.Name)];

        Assert.Contains("Only.English", withParents);
        Assert.DoesNotContain("Only.English", ownOnly);
        Assert.Contains("Common.Save", ownOnly);
    }

    [Fact]
    public async Task The_generic_localizer_reads_its_type_as_a_prefix()
    {
        await using ServiceProvider services = Build();
        await services.InitializeLocalizationKitAsync();

        // What the type parameter means here: a key prefix, which is the nearest thing in one flat
        // key space to the resource file Microsoft's own implementation would pick.
        Assert.Equal(
            "Scoped save",
            services.GetRequiredService<IStringLocalizer<StringLocalizerBridgeTests>>()["Save"].Value);
    }

    [Fact]
    public async Task A_key_the_prefix_does_not_have_still_resolves()
    {
        // So adding the generic localizer to code that resolved flat keys changes nothing for them.
        await using ServiceProvider services = Build();
        await services.InitializeLocalizationKitAsync();

        Assert.Equal(
            "Save",
            services.GetRequiredService<IStringLocalizer<StringLocalizerBridgeTests>>()["Common.Save"].Value);
    }

    [Fact]
    public async Task The_factory_reads_a_base_names_last_segment_as_the_prefix()
    {
        await using ServiceProvider services = Build();
        await services.InitializeLocalizationKitAsync();

        IStringLocalizerFactory factory = services.GetRequiredService<IStringLocalizerFactory>();

        Assert.Equal("Scoped save", factory.Create("App.Pages.StringLocalizerBridgeTests", "Shop")["Save"].Value);
        Assert.Equal("Scoped save", factory.Create(typeof(StringLocalizerBridgeTests))["Save"].Value);
    }

    [Fact]
    public async Task The_factory_serves_the_same_resources()
    {
        await using ServiceProvider services = Build();
        await services.InitializeLocalizationKitAsync();

        IStringLocalizerFactory factory = services.GetRequiredService<IStringLocalizerFactory>();

        Assert.Equal("Save", factory.Create(typeof(StringLocalizerBridgeTests))["Common.Save"].Value);
        Assert.Equal("Save", factory.Create("Any", "Where")["Common.Save"].Value);
        Assert.Equal("Save", factory.Create(string.Empty, "Where")["Common.Save"].Value);
    }

    [Fact]
    public async Task It_replaces_the_registration_AddLocalization_would_make()
    {
        ServiceCollection services = new();
        services.AddLocalization();   // Microsoft's own RESX-backed registration
        services.AddLocalizationKit(options =>
        {
            options.DefaultCulture = "en-US";
            options.InitialCulture = "en-US";
            options.FlowToThreadCulture = false;
        });

        services.AddSingleton<ILocalizationResourceProvider>(new Resources());
        services.AddLocalizationKitStringLocalizer();

        await using ServiceProvider provider = services.BuildServiceProvider();
        await provider.InitializeLocalizationKitAsync();

        Assert.IsType<LocalizationKitStringLocalizerFactory>(provider.GetRequiredService<IStringLocalizerFactory>());
        Assert.Equal("Save", provider.GetRequiredService<IStringLocalizer>()["Common.Save"].Value);
    }
}
