using System.Globalization;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizationKit.Resources.Resw.Tests;

public sealed class ReswResourceProviderTests
{
    [Fact]
    public async Task Loads_the_file_for_one_culture()
    {
        using TempDirectory temp = new();
        temp.WriteResw("it-IT", new ReswEntry("Common.Save", "Salva"));

        ReswResourceProvider provider = new(new ReswLocalizationOptions { RootDirectory = temp.Path });
        IReadOnlyDictionary<string, string>? entries =
            await provider.LoadAsync(CultureInfo.GetCultureInfo("it-IT"));

        Assert.NotNull(entries);
        Assert.Equal("Salva", entries["Common.Save"]);
    }

    [Fact]
    public async Task Returns_null_for_a_culture_it_does_not_have()
    {
        using TempDirectory temp = new();
        temp.WriteResw("it-IT", new ReswEntry("Common.Save", "Salva"));

        ReswResourceProvider provider = new(new ReswLocalizationOptions { RootDirectory = temp.Path });

        // Null rather than empty: the engine must be free to try the next culture in the chain.
        Assert.Null(await provider.LoadAsync(CultureInfo.GetCultureInfo("de-DE")));
    }

    [Fact]
    public async Task Does_not_apply_its_own_fallback()
    {
        using TempDirectory temp = new();
        temp.WriteResw("it-IT", new ReswEntry("Common.Save", "Salva"));

        ReswResourceProvider provider = new(new ReswLocalizationOptions { RootDirectory = temp.Path });

        Assert.Null(await provider.LoadAsync(CultureInfo.GetCultureInfo("it")));
    }

    [Fact]
    public async Task An_override_directory_wins_over_the_shipped_file()
    {
        using TempDirectory shipped = new();
        using TempDirectory updates = new();
        shipped.WriteResw("it-IT", new ReswEntry("Common.Save", "Salva"), new ReswEntry("Only.Shipped", "Spedito"));
        updates.WriteResw("it-IT", new ReswEntry("Common.Save", "Conserva"));

        ReswLocalizationOptions options = new() { RootDirectory = shipped.Path };
        options.OverrideDirectories.Add(updates.Path);

        IReadOnlyDictionary<string, string>? entries =
            await new ReswResourceProvider(options).LoadAsync(CultureInfo.GetCultureInfo("it-IT"));

        Assert.NotNull(entries);
        Assert.Equal("Conserva", entries["Common.Save"]);
        Assert.Equal("Spedito", entries["Only.Shipped"]);
    }

    [Fact]
    public void Discovers_the_cultures_it_ships()
    {
        using TempDirectory temp = new();
        temp.WriteResw("en-US", new ReswEntry("A", "A"));
        temp.WriteResw("it-IT", new ReswEntry("A", "A"));
        Directory.CreateDirectory(temp.Combine("Strings", "de-DE"));

        IReadOnlyList<string> cultures = ReswResourceProvider.DiscoverCultures(
            new ReswLocalizationOptions { RootDirectory = temp.Path });

        // de-DE has a folder but no resource file, so it is not something the app ships.
        Assert.Equal(["en-US", "it-IT"], cultures);
    }

    [Fact]
    public async Task Works_end_to_end_through_dependency_injection()
    {
        using TempDirectory temp = new();
        temp.WriteResw(
            "en-US",
            new ReswEntry("Common.Save", "Save"),
            new ReswEntry("Welcome", "Welcome, {name}!"));

        temp.WriteResw("it-IT", new ReswEntry("Common.Save", "Salva"));

        ServiceCollection services = new();
        services.AddLocalizationKit(options =>
        {
            options.DefaultCulture = "en-US";
            options.SupportedCultures = ["en-US", "it-IT"];
            options.InitialCulture = "en-US";
            options.FlowToThreadCulture = false;
        });

        services.AddReswLocalization(options => options.RootDirectory = temp.Path);

        await using ServiceProvider provider = services.BuildServiceProvider();
        ILocalizationContext context = await provider.InitializeLocalizationKitAsync();
        ILocalizer localizer = provider.GetRequiredService<ILocalizer>();

        ILocalizedString save = localizer.Observe("Common.Save");
        Assert.Equal("Save", save.Value);

        await context.SetCultureAsync("it-IT");

        Assert.Equal("Salva", save.Value);

        // Untranslated in Italian, so the chain falls back to the default culture.
        Assert.Equal("Welcome, Francesco!", localizer.Format("Welcome", new { name = "Francesco" }));
    }

    [Fact]
    public async Task Reload_picks_up_a_file_edited_while_the_application_runs()
    {
        using TempDirectory temp = new();
        temp.WriteResw("en-US", new ReswEntry("Common.Save", "Save"));

        ServiceCollection services = new();
        services.AddLocalizationKit(options =>
        {
            options.DefaultCulture = "en-US";
            options.InitialCulture = "en-US";
            options.FlowToThreadCulture = false;
        });

        services.AddReswLocalization(options => options.RootDirectory = temp.Path);

        await using ServiceProvider provider = services.BuildServiceProvider();
        ILocalizationContext context = await provider.InitializeLocalizationKitAsync();
        ILocalizedString save = provider.GetRequiredService<ILocalizer>().Observe("Common.Save");

        Assert.Equal("Save", save.Value);

        temp.WriteResw("en-US", new ReswEntry("Common.Save", "Store"));
        await context.ReloadAsync();

        Assert.Equal("Store", save.Value);
    }
}
