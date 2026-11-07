using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizationKit.Resources.Json.Tests;

public sealed class JsonResourceProviderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "localizationkit-json-tests",
        Guid.NewGuid().ToString("n"));

    private string Write(string culture, string json)
    {
        string path = Path.Combine(_root, "Localization", culture + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json, new UTF8Encoding(false));
        return path;
    }

    private JsonLocalizationOptions Options()
    {
        return new JsonLocalizationOptions { RootDirectory = _root };
    }

    [Fact]
    public async Task Reads_a_flat_file()
    {
        Write("it-IT", """{ "Common.Save": "Salva", "Common.Cancel": "Annulla" }""");

        IReadOnlyDictionary<string, string>? entries =
            await new JsonResourceProvider(Options()).LoadAsync(CultureInfo.GetCultureInfo("it-IT"));

        Assert.NotNull(entries);
        Assert.Equal("Salva", entries["Common.Save"]);
        Assert.Equal("Annulla", entries["Common.Cancel"]);
    }

    [Fact]
    public async Task A_nested_file_flattens_to_the_same_keys()
    {
        // Both shapes are written in practice, and they have to be comparable with .resw keys.
        Write("it-IT", """{ "Common": { "Save": "Salva", "Deep": { "Key": "Valore" } } }""");

        IReadOnlyDictionary<string, string>? entries =
            await new JsonResourceProvider(Options()).LoadAsync(CultureInfo.GetCultureInfo("it-IT"));

        Assert.NotNull(entries);
        Assert.Equal("Salva", entries["Common.Save"]);
        Assert.Equal("Valore", entries["Common.Deep.Key"]);
    }

    [Fact]
    public async Task Keys_are_case_insensitive_like_the_rest_of_the_engine()
    {
        Write("en-US", """{ "Common.Save": "Save" }""");

        IReadOnlyDictionary<string, string>? entries =
            await new JsonResourceProvider(Options()).LoadAsync(CultureInfo.GetCultureInfo("en-US"));

        Assert.NotNull(entries);
        Assert.Equal("Save", entries["common.save"]);
    }

    [Fact]
    public async Task Comments_and_trailing_commas_are_tolerated()
    {
        Write("en-US", """
            {
              // the save button
              "Common.Save": "Save",
            }
            """);

        IReadOnlyDictionary<string, string>? entries =
            await new JsonResourceProvider(Options()).LoadAsync(CultureInfo.GetCultureInfo("en-US"));

        Assert.NotNull(entries);
        Assert.Equal("Save", entries["Common.Save"]);
    }

    [Fact]
    public void A_value_that_is_not_a_string_is_rejected()
    {
        string path = Write("en-US", """{ "Count": 5 }""");

        JsonException error = Assert.Throws<JsonException>(() => JsonResourceFile.Read(path));

        Assert.Contains("Count", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_document_that_is_not_an_object_is_rejected()
    {
        string path = Write("en-US", """["Save"]""");

        Assert.Throws<JsonException>(() => JsonResourceFile.Read(path));
    }

    [Fact]
    public async Task Returns_null_for_a_culture_it_does_not_have()
    {
        Write("en-US", """{ "Common.Save": "Save" }""");

        // Null rather than empty: the engine must be free to try the next culture in the chain.
        Assert.Null(await new JsonResourceProvider(Options()).LoadAsync(CultureInfo.GetCultureInfo("de-DE")));
    }

    [Fact]
    public async Task The_file_name_pattern_can_be_a_folder_per_culture()
    {
        string path = Path.Combine(_root, "Localization", "it-IT", "strings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{ "Common.Save": "Salva" }""");

        JsonLocalizationOptions options = Options();
        options.FileNameFormat = "{culture}/strings.json";

        IReadOnlyDictionary<string, string>? entries =
            await new JsonResourceProvider(options).LoadAsync(CultureInfo.GetCultureInfo("it-IT"));

        Assert.NotNull(entries);
        Assert.Equal("Salva", entries["Common.Save"]);
    }

    [Fact]
    public async Task Works_end_to_end_through_dependency_injection()
    {
        Write("en-US", """{ "Common": { "Save": "Save" }, "Welcome": "Welcome, {name}!" }""");
        Write("it-IT", """{ "Common": { "Save": "Salva" } }""");

        ServiceCollection services = new();
        services.AddLocalizationKit(options =>
        {
            options.DefaultCulture = "en-US";
            options.SupportedCultures = ["en-US", "it-IT"];
            options.InitialCulture = "en-US";
            options.FlowToThreadCulture = false;
        });

        services.AddJsonLocalization(options => options.RootDirectory = _root);

        await using ServiceProvider provider = services.BuildServiceProvider();
        ILocalizationContext context = await provider.InitializeLocalizationKitAsync();
        ILocalizer localizer = provider.GetRequiredService<ILocalizer>();

        Assert.Equal("Save", localizer["Common.Save"]);

        await context.SetCultureAsync("it-IT");

        Assert.Equal("Salva", localizer["Common.Save"]);

        // Untranslated in Italian, so the chain falls back to the default culture.
        Assert.Equal("Welcome, Francesco!", localizer.Format("Welcome", new { name = "Francesco" }));
    }

    [Fact]
    public async Task An_override_directory_wins_over_the_shipped_file()
    {
        Write("it-IT", """{ "Common.Save": "Salva", "Only.Shipped": "Spedito" }""");

        string updates = Path.Combine(_root, "updates");
        string path = Path.Combine(updates, "Localization", "it-IT.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{ "Common.Save": "Conserva" }""");

        JsonLocalizationOptions options = Options();
        options.OverrideDirectories.Add(updates);

        IReadOnlyDictionary<string, string>? entries =
            await new JsonResourceProvider(options).LoadAsync(CultureInfo.GetCultureInfo("it-IT"));

        Assert.NotNull(entries);
        Assert.Equal("Conserva", entries["Common.Save"]);
        Assert.Equal("Spedito", entries["Only.Shipped"]);
    }

    [Fact]
    public async Task Json_and_resw_style_keys_meet_in_the_same_key_space()
    {
        // A project can run both providers; a nested JSON key and a dotted .resw name are the same key.
        Write("en-US", """{ "Settings": { "Theme": { "Header": "Theme" } } }""");

        IReadOnlyDictionary<string, string>? entries =
            await new JsonResourceProvider(Options()).LoadAsync(CultureInfo.GetCultureInfo("en-US"));

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
