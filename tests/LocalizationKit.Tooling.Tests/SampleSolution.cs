using System.Text;

namespace LocalizationKit.Tooling.Tests;

/// <summary>
/// A throwaway copy of the real thing: a WinUI-shaped project with a solution, a <c>.vs</c> folder,
/// and the two developer-local files this tooling maintains.
/// </summary>
/// <remarks>
/// The fixtures are byte-for-byte what Visual Studio writes, including the UTF-8 BOM on the project
/// user file, its absence on the settings file, CRLF endings and no trailing newline. Test source
/// files are not guaranteed to use CRLF, so every fixture is normalized explicitly.
/// </remarks>
internal sealed class SampleSolution : IDisposable
{
    public const string VisualStudioUserFile =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <Project ToolsVersion="Current" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
          <PropertyGroup />
          <ItemGroup>
            <Page Update="Views\LoginPage.xaml">
              <SubType>Designer</SubType>
            </Page>
          </ItemGroup>
          <ItemGroup>
            <PRIResource Update="Strings\it-IT\Resources.resw">
              <SubType>Designer</SubType>
            </PRIResource>
          </ItemGroup>
        </Project>
        """;

    public const string VisualStudioSettingsFile =
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
            "file-path",
            "file-name"
          ],
          "VisibleColumnKeys": [
            "name",
            "en-US",
            "es-ES"
          ]
        }
        """;

    private const string ProjectFileTemplate =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
            <UseWinUI>true</UseWinUI>
            <DefaultLanguage>en-US</DefaultLanguage>
        __CULTURES__  </PropertyGroup>
        </Project>
        """;

    public SampleSolution(string? cultures = null, string projectName = "Sample.WinUI")
    {
        Root = Path.Combine(Path.GetTempPath(), "localizationkit-tooling-tests", Guid.NewGuid().ToString("n"));
        ProjectName = projectName;
        ProjectDirectory = Path.Combine(Root, projectName);
        Directory.CreateDirectory(ProjectDirectory);

        File.WriteAllText(Path.Combine(Root, "sample.slnx"), Crlf("<Solution />"));

        string culturesProperty = cultures is null
            ? string.Empty
            : $"    <{ProjectContext.CulturesProperty}>{cultures}</{ProjectContext.CulturesProperty}>\n";

        File.WriteAllText(
            ProjectFile,
            Crlf(ProjectFileTemplate.Replace("__CULTURES__", culturesProperty, StringComparison.Ordinal)));
    }

    public string Root { get; }

    public string ProjectName { get; }

    public string ProjectDirectory { get; }

    public string ProjectFile => Path.Combine(ProjectDirectory, ProjectName + ".csproj");

    public string UserFile => ProjectFile + ".user";

    public string SettingsFile => Path.Combine(
        Root, ".vs", "sample.slnx", "v18", "ResourceExplorer", "settings.json");

    /// <summary>Normalizes a fixture to the CRLF, no-trailing-newline shape Visual Studio writes.</summary>
    public static string Crlf(string text) => text.ReplaceLineEndings("\r\n");

    public SampleSolution WithResw(string culture, params ReswEntry[] entries)
    {
        ReswFile.Write(Path.Combine(ProjectDirectory, "Strings", culture, "Resources.resw"), entries);
        return this;
    }

    /// <summary>Writes a source file, for the scan that reads them.</summary>
    /// <param name="name">The file name, whose extension decides whether the scanner reads it.</param>
    /// <param name="content">What it contains.</param>
    /// <returns>The same solution, for chaining.</returns>
    public SampleSolution WithSource(string name, string content)
    {
        File.WriteAllText(Path.Combine(ProjectDirectory, name), content);
        return this;
    }

    public SampleSolution WithUserFile(string? content = null)
    {
        File.WriteAllText(UserFile, Crlf(content ?? VisualStudioUserFile), new UTF8Encoding(true));
        return this;
    }

    public SampleSolution WithResourceExplorerSettings(string? content = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        File.WriteAllText(SettingsFile, Crlf(content ?? VisualStudioSettingsFile), new UTF8Encoding(false));
        return this;
    }

    public ProjectContext Discover() => ProjectContext.Discover(Root, ProjectFile);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Scratch directory; a lock here is not a test failure.
        }
    }
}
