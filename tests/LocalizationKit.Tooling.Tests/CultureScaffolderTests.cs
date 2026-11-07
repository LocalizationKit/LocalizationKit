using System.Globalization;

namespace LocalizationKit.Tooling.Tests;

public sealed class CultureScaffolderTests
{
    [Fact]
    public void Adding_a_culture_touches_the_resource_file_the_project_and_both_ide_files()
    {
        using SampleSolution solution = new(cultures: "en-US");
        solution
            .WithResw("en-US", new ReswEntry("Common.Save", "Save", "Button label"))
            .WithUserFile()
            .WithResourceExplorerSettings();

        AddCultureResult result = CultureScaffolder.Plan(solution.Discover(), "de-DE");

        Assert.Equal("de-DE", result.Culture);
        Assert.Equal(
            [
                Path.Combine(solution.ProjectDirectory, "Strings", "de-DE", "Resources.resw"),
                solution.ProjectFile,
                solution.UserFile,
                solution.SettingsFile,
            ],
            result.Changes.Select(c => c.Path));
    }

    [Fact]
    public void The_new_file_carries_the_keys_and_comments_but_no_translations()
    {
        using SampleSolution solution = new(cultures: "en-US");
        solution.WithResw(
            "en-US",
            new ReswEntry("Common.Save", "Save", "Button label"),
            new ReswEntry("Welcome", "Welcome, {name}!"));

        AddCultureResult result = CultureScaffolder.Plan(solution.Discover(), "it-IT");
        result.Apply();

        IReadOnlyList<ReswEntry> entries = ReswFile.Read(
            Path.Combine(solution.ProjectDirectory, "Strings", "it-IT", "Resources.resw"));

        Assert.Equal(
            [
                new ReswEntry("Common.Save", string.Empty, "Button label"),
                new ReswEntry("Welcome", string.Empty),
            ],
            entries);
    }

    [Fact]
    public void An_empty_file_can_be_requested_instead()
    {
        using SampleSolution solution = new(cultures: "en-US");
        solution.WithResw("en-US", new ReswEntry("Common.Save", "Save"));

        CultureScaffolder.Plan(solution.Discover(), "it-IT", seedFromDefault: false).Apply();

        Assert.Empty(ReswFile.Read(Path.Combine(solution.ProjectDirectory, "Strings", "it-IT", "Resources.resw")));
    }

    [Fact]
    public void The_culture_list_is_extended_when_the_project_declares_one()
    {
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution.WithResw("en-US", new ReswEntry("A", "A"));

        CultureScaffolder.Plan(solution.Discover(), "de-DE").Apply();

        Assert.Contains(
            $"<{ProjectContext.CulturesProperty}>en-US;it-IT;de-DE</{ProjectContext.CulturesProperty}>",
            File.ReadAllText(solution.ProjectFile),
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_project_file_is_not_given_a_property_it_never_had()
    {
        using SampleSolution solution = new();
        solution.WithResw("en-US", new ReswEntry("A", "A"));
        string before = File.ReadAllText(solution.ProjectFile);

        AddCultureResult result = CultureScaffolder.Plan(solution.Discover(), "it-IT");
        result.Apply();

        Assert.Equal(before, File.ReadAllText(solution.ProjectFile));
        Assert.Contains(
            result.Notes,
            note => note.Contains(ProjectContext.CulturesProperty, StringComparison.Ordinal));
    }

    [Fact]
    public void The_new_culture_gets_its_designer_entry_and_its_column_in_the_same_run()
    {
        using SampleSolution solution = new(cultures: "en-US");
        solution.WithResw("en-US", new ReswEntry("A", "A"))
            .WithUserFile()
            .WithResourceExplorerSettings();

        CultureScaffolder.Plan(solution.Discover(), "de-DE").Apply();

        Assert.Contains(
            @"Update=""Strings\de-DE\Resources.resw""",
            File.ReadAllText(solution.UserFile),
            StringComparison.Ordinal);

        Assert.Contains("\"de-DE\"", File.ReadAllText(solution.SettingsFile), StringComparison.Ordinal);
    }

    [Fact]
    public void An_existing_culture_is_not_overwritten()
    {
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution.WithResw("en-US", new ReswEntry("A", "A"))
            .WithResw("it-IT", new ReswEntry("A", "Tradotto"));

        AddCultureResult result = CultureScaffolder.Plan(solution.Discover(), "it-IT");
        result.Apply();

        Assert.Equal("Tradotto", ReswFile.ReadValues(solution.Discover().ResourceFileFor("it-IT"))["A"]);
        Assert.Contains(result.Notes, note => note.Contains("already exists", StringComparison.Ordinal));
    }

    [Fact]
    public void A_name_that_is_not_a_culture_is_refused_before_anything_is_written()
    {
        using SampleSolution solution = new();
        solution.WithResw("en-US", new ReswEntry("A", "A"));

        Assert.Throws<CultureNotFoundException>(() => CultureScaffolder.Plan(solution.Discover(), "not-a-culture"));
    }
}
