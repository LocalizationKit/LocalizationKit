using Microsoft.CodeAnalysis;

namespace LocalizationKit.Generators.Tests;

public sealed class LocalizationKeysGeneratorTests
{
    private static string Resw(params string[] names)
    {
        IEnumerable<string> entries = names.Select(name =>
            $"""  <data name="{name}" xml:space="preserve"><value>text</value></data>""");

        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <root>
            {string.Join("\n", entries)}
            </root>
            """;
    }

    [Fact]
    public void A_dotted_key_becomes_the_same_path_in_csharp()
    {
        GeneratorHarness.GeneratorResult result = GeneratorHarness.Run(Resw("Settings.Theme.Header"));

        Assert.Empty(result.CompileErrors);
        Assert.Contains("namespace Sample.App;", result.Source, StringComparison.Ordinal);
        Assert.Contains("internal static partial class LocalizationKeys", result.Source, StringComparison.Ordinal);
        Assert.Contains("public static class Settings", result.Source, StringComparison.Ordinal);
        Assert.Contains("public static class Theme", result.Source, StringComparison.Ordinal);
        Assert.Contains(
            """public const string Header = "Settings.Theme.Header";""",
            result.Source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_key_without_dots_is_a_constant_at_the_top_level()
    {
        GeneratorHarness.GeneratorResult result = GeneratorHarness.Run(Resw("Welcome"));

        Assert.Empty(result.CompileErrors);
        Assert.Contains("""public const string Welcome = "Welcome";""", result.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void A_key_that_is_also_a_prefix_of_another_lands_on_the_class_as_Value()
    {
        // Settings.Theme next to Settings.Theme.System: a class cannot share a name with a constant.
        GeneratorHarness.GeneratorResult result = GeneratorHarness.Run(
            Resw("Settings.Theme", "Settings.Theme.System"));

        Assert.Empty(result.CompileErrors);
        Assert.Contains("""public const string Value = "Settings.Theme";""", result.Source, StringComparison.Ordinal);
        Assert.Contains("""public const string System = "Settings.Theme.System";""", result.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void Names_csharp_would_reject_still_produce_code_that_compiles()
    {
        // Resource names are written by whoever writes the resources: spaces, hyphens, a leading
        // digit, and words the language has reserved are all possible.
        GeneratorHarness.GeneratorResult result = GeneratorHarness.Run(
            Resw("Dialog.Delete forever", "Menu.Add-tag", "Errors.404", "Common.class", "Common.new"));

        Assert.Empty(result.CompileErrors);
        Assert.Contains("Delete_forever", result.Source, StringComparison.Ordinal);
        Assert.Contains("Add_tag", result.Source, StringComparison.Ordinal);
        Assert.Contains("_404", result.Source, StringComparison.Ordinal);
        Assert.Contains("@class", result.Source, StringComparison.Ordinal);
        Assert.Contains("@new", result.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_names_that_sanitize_to_the_same_identifier_do_not_collide()
    {
        // "Add tag" and "Add-tag" both want to be Add_tag, and generated code that does not compile
        // would be worse than no generated code.
        GeneratorHarness.GeneratorResult result = GeneratorHarness.Run(Resw("Menu.Add tag", "Menu.Add-tag"));

        Assert.Empty(result.CompileErrors);
        Assert.Contains("Add_tag ", result.Source, StringComparison.Ordinal);
        Assert.Contains("Add_tag2", result.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_class_name_namespace_and_accessibility_are_configurable()
    {
        GeneratorHarness.GeneratorResult result = GeneratorHarness.Run(
            Resw("Welcome"),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["build_property.LocalizationKitKeysClass"] = "Keys",
                ["build_property.LocalizationKitKeysNamespace"] = "Other.Place",
                ["build_property.LocalizationKitKeysAccessibility"] = "public",
            });

        Assert.Empty(result.CompileErrors);
        Assert.Contains("namespace Other.Place;", result.Source, StringComparison.Ordinal);
        Assert.Contains("public static partial class Keys", result.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void It_can_be_turned_off()
    {
        GeneratorHarness.GeneratorResult result = GeneratorHarness.Run(
            Resw("Welcome"),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["build_property.LocalizationKitGenerateKeys"] = "false",
            });

        Assert.Empty(result.Source);
    }

    [Fact]
    public void A_file_not_marked_as_a_resource_file_is_ignored()
    {
        // Every project has AdditionalFiles for other reasons; only the ones the targets marked are
        // resources.
        GeneratorHarness.GeneratorResult result = GeneratorHarness.Run(
            Resw("Welcome"),
            markAsResourceFile: false);

        Assert.Empty(result.Source);
    }

    [Fact]
    public void Nothing_is_generated_when_there_are_no_keys()
    {
        // An empty class would compile, and every call site using it would then fail with a less
        // obvious error than "the class does not exist".
        GeneratorHarness.GeneratorResult result = GeneratorHarness.Run(Resw());

        Assert.Empty(result.Source);
    }

    [Fact]
    public void Entries_that_are_not_strings_are_skipped()
    {
        string resw = """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="Logo" type="System.Drawing.Bitmap, System.Drawing"><value>logo.png</value></data>
              <data name="Icon" mimetype="application/x-microsoft.net.object.bytearray.base64"><value>AA==</value></data>
              <data name="Welcome" xml:space="preserve"><value>Welcome</value></data>
            </root>
            """;

        GeneratorHarness.GeneratorResult result = GeneratorHarness.Run(resw);

        Assert.Contains("Welcome", result.Source, StringComparison.Ordinal);
        Assert.DoesNotContain("Logo", result.Source, StringComparison.Ordinal);
        Assert.DoesNotContain("Icon", result.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void A_file_that_is_not_valid_xml_is_reported_and_does_not_fail_the_build()
    {
        GeneratorHarness.GeneratorResult result = GeneratorHarness.Run("<root><data name=\"A\">");

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("LKG001", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Empty(result.Source);
    }
}
