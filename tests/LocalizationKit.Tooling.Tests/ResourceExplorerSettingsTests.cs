using System.Text;

namespace LocalizationKit.Tooling.Tests;

public sealed class ResourceExplorerSettingsTests
{
    [Fact]
    public void Finds_the_settings_file_without_assuming_the_solution_or_version_folder()
    {
        using SampleSolution solution = new();
        solution.WithResourceExplorerSettings();

        IReadOnlyList<string> found = ResourceExplorerSettings.Discover(solution.Root);

        Assert.Equal([solution.SettingsFile], found);
    }

    [Fact]
    public void Adds_a_culture_column_next_to_the_other_cultures()
    {
        using SampleSolution solution = new();
        solution.WithResourceExplorerSettings();

        FileChange? change = ResourceExplorerSettings.Plan(solution.SettingsFile, ["en-US", "it-IT"]);

        Assert.NotNull(change);
        Assert.Equal(
            SampleSolution.Crlf(
                """
                {
                  "ShowEmptyProjects": false,
                  "SelectedGroups": [],
                  "ExpandedProjects": [],
                  "EnableSpellChecker": true,
                  "ColumnOrder": [
                    "name",
                    "type",
                    "en-US",
                    "neutral-value",
                    "neutral-comment",
                    "es-ES",
                    "it-IT",
                    "file-path",
                    "file-name"
                  ],
                  "VisibleColumnKeys": [
                    "name",
                    "en-US",
                    "es-ES",
                    "it-IT"
                  ]
                }
                """),
            change.NewContent);
    }

    [Fact]
    public void Leaves_a_stale_culture_alone_unless_asked_to_prune()
    {
        using SampleSolution solution = new();
        solution.WithResourceExplorerSettings();

        FileChange? kept = ResourceExplorerSettings.Plan(solution.SettingsFile, ["en-US", "it-IT"]);
        Assert.Contains("es-ES", kept!.NewContent, StringComparison.Ordinal);

        FileChange? pruned = ResourceExplorerSettings.Plan(solution.SettingsFile, ["en-US", "it-IT"], prune: true);
        Assert.DoesNotContain("es-ES", pruned!.NewContent, StringComparison.Ordinal);
        Assert.Contains("remove es-ES", pruned.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Is_idempotent()
    {
        using SampleSolution solution = new();
        solution.WithResourceExplorerSettings();

        ResourceExplorerSettings.Plan(solution.SettingsFile, ["en-US", "it-IT"])!.Apply();

        Assert.Null(ResourceExplorerSettings.Plan(solution.SettingsFile, ["en-US", "it-IT"]));
    }

    [Fact]
    public void Writes_the_file_the_way_visual_studio_does()
    {
        using SampleSolution solution = new();
        solution.WithResourceExplorerSettings();

        ResourceExplorerSettings.Plan(solution.SettingsFile, ["en-US", "it-IT"])!.Apply();

        byte[] bytes = File.ReadAllBytes(solution.SettingsFile);
        string text = Encoding.UTF8.GetString(bytes);

        Assert.NotEqual([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.EndsWith("}", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', text.Replace("\r\n", string.Empty, StringComparison.Ordinal));
        Assert.Contains("\r\n  \"ColumnOrder\": [", text, StringComparison.Ordinal);
    }

    [Fact]
    public void View_state_is_not_touched()
    {
        using SampleSolution solution = new();
        solution.WithResourceExplorerSettings();

        FileChange? change = ResourceExplorerSettings.Plan(solution.SettingsFile, ["de-DE"]);

        Assert.NotNull(change);
        Assert.Contains("\"EnableSpellChecker\": true", change.NewContent, StringComparison.Ordinal);
        Assert.Contains("\"ShowEmptyProjects\": false", change.NewContent, StringComparison.Ordinal);
        Assert.Contains("\"SelectedGroups\": []", change.NewContent, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_settings_file_is_not_created()
    {
        using SampleSolution solution = new();

        // Visual Studio owns this file; conjuring one risks writing a shape a future version rejects.
        Assert.Null(ResourceExplorerSettings.Plan(solution.SettingsFile, ["it-IT"]));
    }
}
