using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using YamlDotNet.Core;

namespace LocalizationKit.Resources.Yaml.Tests;

public sealed class YamlResourceProviderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "localizationkit-yaml-tests",
        Guid.NewGuid().ToString("n"));

    private string Write(string culture, string yaml, string extension = "yaml")
    {
        string path = Path.Combine(_root, "Localization", $"{culture}.{extension}");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, yaml, new UTF8Encoding(false));
        return path;
    }

    private YamlLocalizationOptions Options() => new() { RootDirectory = _root };

    [Fact]
    public async Task Reads_a_flat_file()
    {
        Write("it-IT", "Common.Save: Salva\nCommon.Cancel: Annulla\n");

        IReadOnlyDictionary<string, string>? entries =
            await new YamlResourceProvider(Options()).LoadAsync(CultureInfo.GetCultureInfo("it-IT"));

        Assert.NotNull(entries);
        Assert.Equal("Salva", entries["Common.Save"]);
        Assert.Equal("Annulla", entries["Common.Cancel"]);
    }

    [Fact]
    public async Task A_nested_file_flattens_to_the_same_keys()
    {
        Write("it-IT", """
            Common:
              Save: Salva
              Deep:
                Key: Valore
            """);

        IReadOnlyDictionary<string, string>? entries =
            await new YamlResourceProvider(Options()).LoadAsync(CultureInfo.GetCultureInfo("it-IT"));

        Assert.NotNull(entries);
        Assert.Equal("Salva", entries["Common.Save"]);
        Assert.Equal("Valore", entries["Common.Deep.Key"]);
    }

    [Fact]
    public async Task Comments_survive_and_a_long_message_can_be_folded()
    {
        // The reason a translator might want YAML in the first place.
        Write("en-US", """
            # The save button, on the toolbar.
            Common.Save: Save

            Dialog.Explanation: >-
              This note has been edited on another device.
              Choose which version to keep.
            """);

        IReadOnlyDictionary<string, string>? entries =
            await new YamlResourceProvider(Options()).LoadAsync(CultureInfo.GetCultureInfo("en-US"));

        Assert.NotNull(entries);
        Assert.Equal("Save", entries["Common.Save"]);
        Assert.Equal(
            "This note has been edited on another device. Choose which version to keep.",
            entries["Dialog.Explanation"]);
    }

    [Fact]
    public void A_translation_that_looks_like_another_type_stays_text()
    {
        // YAML's defining trap: No is a boolean, 1.0 a float, 2026-01-01 a date. A translation is
        // never anything but text, and these are real translations — "No" in English, "On" in Italian.
        string path = Write("en-US", """
            Dialog.No: No
            Dialog.On: On
            Version.Number: 1.0
            Release.Date: 2026-01-01
            """);

        IReadOnlyDictionary<string, string> entries = YamlResourceFile.Read(path);

        Assert.Equal("No", entries["Dialog.No"]);
        Assert.Equal("On", entries["Dialog.On"]);
        Assert.Equal("1.0", entries["Version.Number"]);
        Assert.Equal("2026-01-01", entries["Release.Date"]);
    }

    [Fact]
    public void A_value_that_is_a_sequence_is_rejected()
    {
        string path = Write("en-US", "Common.Save:\n  - Save\n  - Store\n");

        YamlException error = Assert.Throws<YamlException>(() => YamlResourceFile.Read(path));

        Assert.Contains("Common.Save", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_document_that_is_not_a_mapping_is_rejected()
    {
        string path = Write("en-US", "- Save\n- Store\n");

        Assert.Throws<YamlException>(() => YamlResourceFile.Read(path));
    }

    [Fact]
    public void An_empty_file_is_an_empty_translation_set()
    {
        // A culture can be scaffolded before anyone has written anything in it.
        Assert.Empty(YamlResourceFile.Read(Write("en-US", string.Empty)));
    }

    [Fact]
    public async Task Keys_are_case_insensitive_like_the_rest_of_the_engine()
    {
        Write("en-US", "Common.Save: Save\n");

        IReadOnlyDictionary<string, string>? entries =
            await new YamlResourceProvider(Options()).LoadAsync(CultureInfo.GetCultureInfo("en-US"));

        Assert.NotNull(entries);
        Assert.Equal("Save", entries["common.save"]);
    }

    [Fact]
    public async Task Both_extensions_are_found()
    {
        Write("it-IT", "Common.Save: Salva\n", extension: "yml");

        IReadOnlyDictionary<string, string>? entries =
            await new YamlResourceProvider(Options()).LoadAsync(CultureInfo.GetCultureInfo("it-IT"));

        Assert.NotNull(entries);
        Assert.Equal("Salva", entries["Common.Save"]);
    }

    [Fact]
    public async Task Returns_null_for_a_culture_it_does_not_have()
    {
        Write("en-US", "Common.Save: Save\n");

        // Null rather than empty: the engine must be free to try the next culture in the chain.
        Assert.Null(await new YamlResourceProvider(Options()).LoadAsync(CultureInfo.GetCultureInfo("de-DE")));
    }

    [Fact]
    public async Task An_override_directory_wins_over_the_shipped_file()
    {
        Write("it-IT", "Common.Save: Salva\nOnly.Shipped: Spedito\n");

        string updates = Path.Combine(_root, "updates");
        string path = Path.Combine(updates, "Localization", "it-IT.yaml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "Common.Save: Conserva\n");

        YamlLocalizationOptions options = Options();
        options.OverrideDirectories.Add(updates);

        IReadOnlyDictionary<string, string>? entries =
            await new YamlResourceProvider(options).LoadAsync(CultureInfo.GetCultureInfo("it-IT"));

        Assert.NotNull(entries);
        Assert.Equal("Conserva", entries["Common.Save"]);
        Assert.Equal("Spedito", entries["Only.Shipped"]);
    }

    [Fact]
    public async Task Works_end_to_end_through_dependency_injection()
    {
        Write("en-US", "Common:\n  Save: Save\nWelcome: Welcome, {name}!\n");
        Write("it-IT", "Common:\n  Save: Salva\n");

        ServiceCollection services = new();
        services.AddLocalizationKit(options =>
        {
            options.DefaultCulture = "en-US";
            options.SupportedCultures = ["en-US", "it-IT"];
            options.InitialCulture = "en-US";
            options.FlowToThreadCulture = false;
        });

        services.AddYamlLocalization(options => options.RootDirectory = _root);

        await using ServiceProvider provider = services.BuildServiceProvider();
        ILocalizationContext context = await provider.InitializeLocalizationKitAsync();
        ILocalizer localizer = provider.GetRequiredService<ILocalizer>();

        Assert.Equal("Save", localizer["Common.Save"]);

        await context.SetCultureAsync("it-IT");

        Assert.Equal("Salva", localizer["Common.Save"]);

        // Untranslated in Italian, so the chain falls back to the default culture.
        Assert.Equal(
            "Welcome, Francesco!",
            localizer.Format("Welcome", new Dictionary<string, object?> { ["name"] = "Francesco" }));
    }

    [Fact]
    public async Task Yaml_json_and_resw_style_keys_meet_in_the_same_key_space()
    {
        Write("en-US", "Settings:\n  Theme:\n    Header: Theme\n");

        IReadOnlyDictionary<string, string>? entries =
            await new YamlResourceProvider(Options()).LoadAsync(CultureInfo.GetCultureInfo("en-US"));

        Assert.NotNull(entries);
        Assert.Equal("Theme", entries["Settings.Theme.Header"]);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Scratch directory.
        }
    }
}
