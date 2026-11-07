namespace LocalizationKit.Tooling.Tests;

public sealed class ProjectContextTests
{
    [Fact]
    public void Reads_the_properties_and_the_folders()
    {
        using SampleSolution solution = new(cultures: "en-US;it-IT");
        solution.WithResw("en-US", new ReswEntry("A", "A"))
            .WithResourceExplorerSettings();

        ProjectContext project = solution.Discover();

        Assert.Equal("en-US", project.DefaultLanguage);
        Assert.Equal(["en-US", "it-IT"], project.DeclaredCultures);
        Assert.Equal(["en-US"], project.CultureFolders);
        Assert.Equal(solution.Root, project.SolutionDirectory);
        Assert.Equal(solution.UserFile, project.CsprojUserFile);
        Assert.Equal([solution.SettingsFile], project.ResourceExplorerSettingsFiles);
    }

    [Fact]
    public void Cultures_combine_what_is_declared_with_what_is_on_disk()
    {
        using SampleSolution solution = new(cultures: "en-US");
        solution.WithResw("en-US", new ReswEntry("A", "A")).WithResw("fr-FR", new ReswEntry("A", "A"));

        Assert.Equal(["en-US", "fr-FR"], solution.Discover().Cultures);
    }

    [Fact]
    public void Falls_back_to_the_folders_when_no_culture_list_is_declared()
    {
        using SampleSolution solution = new();
        solution.WithResw("en-US", new ReswEntry("A", "A")).WithResw("it-IT", new ReswEntry("A", "A"));

        ProjectContext project = solution.Discover();

        Assert.Empty(project.DeclaredCultures);
        Assert.Equal(["en-US", "it-IT"], project.Cultures);
    }

    [Fact]
    public void Resource_paths_are_project_relative_with_backslashes()
    {
        using SampleSolution solution = new();

        Assert.Equal("Strings\\it-IT\\Resources.resw", solution.Discover().RelativeResourceFileFor("it-IT"));
    }

    [Fact]
    public void A_folder_without_a_resource_file_is_not_a_culture()
    {
        using SampleSolution solution = new();
        solution.WithResw("en-US", new ReswEntry("A", "A"));
        Directory.CreateDirectory(Path.Combine(solution.ProjectDirectory, "Strings", "de-DE"));

        Assert.Equal(["en-US"], solution.Discover().CultureFolders);
    }
}
