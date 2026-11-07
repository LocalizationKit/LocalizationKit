using System.Xml.Linq;

namespace LocalizationKit.Tooling;

/// <summary>
/// Everything the tooling needs to know about one project: where its <c>.resw</c> files live, which
/// cultures it declares, and which developer-local files describe it to Visual Studio.
/// </summary>
public sealed class ProjectContext
{
    /// <summary>The MSBuild property that owns the culture list.</summary>
    public const string CulturesProperty = "LocalizationKitCultures";

    /// <summary>The MSBuild property a Windows app uses for its default language.</summary>
    public const string DefaultLanguageProperty = "DefaultLanguage";

    private ProjectContext(
        string projectFile,
        string? solutionDirectory,
        string resourcesPath,
        string resourceFileName,
        string? defaultLanguage,
        IReadOnlyList<string> declaredCultures,
        IReadOnlyList<string> cultureFolders,
        IReadOnlyList<string> resourceExplorerSettingsFiles)
    {
        ProjectFile = projectFile;
        SolutionDirectory = solutionDirectory;
        ResourcesPath = resourcesPath;
        ResourceFileName = resourceFileName;
        DefaultLanguage = defaultLanguage;
        DeclaredCultures = declaredCultures;
        CultureFolders = cultureFolders;
        ResourceExplorerSettingsFiles = resourceExplorerSettingsFiles;
    }

    /// <summary>Gets the absolute path of the project file.</summary>
    public string ProjectFile { get; }

    /// <summary>Gets the directory holding the project file.</summary>
    public string ProjectDirectory => Path.GetDirectoryName(ProjectFile)!;

    /// <summary>Gets the directory holding the solution, when one was found.</summary>
    public string? SolutionDirectory { get; }

    /// <summary>Gets the project-relative folder holding one subfolder per culture.</summary>
    public string ResourcesPath { get; }

    /// <summary>Gets the resource file name inside each culture folder.</summary>
    public string ResourceFileName { get; }

    /// <summary>Gets the value of the project's <c>DefaultLanguage</c> property, when set.</summary>
    public string? DefaultLanguage { get; }

    /// <summary>Gets the cultures declared by the <c>LocalizationKitCultures</c> property.</summary>
    public IReadOnlyList<string> DeclaredCultures { get; }

    /// <summary>Gets the culture folders that actually contain a resource file.</summary>
    public IReadOnlyList<string> CultureFolders { get; }

    /// <summary>Gets the Resource Explorer settings files found under the solution's <c>.vs</c> folder.</summary>
    public IReadOnlyList<string> ResourceExplorerSettingsFiles { get; }

    /// <summary>Gets the path of the project's developer-local <c>.user</c> file, which may not exist.</summary>
    public string CsprojUserFile => ProjectFile + ".user";

    /// <summary>Gets the absolute path of the resources folder.</summary>
    public string ResourcesDirectory => Path.Combine(ProjectDirectory, ResourcesPath);

    /// <summary>
    /// Gets the cultures to act on: those declared by the project, followed by any culture folder on
    /// disk that the project does not declare.
    /// </summary>
    public IReadOnlyList<string> Cultures
    {
        get
        {
            List<string> cultures = [.. DeclaredCultures];
            foreach (string folder in CultureFolders)
            {
                if (!cultures.Contains(folder, StringComparer.OrdinalIgnoreCase))
                {
                    cultures.Add(folder);
                }
            }

            return cultures;
        }
    }

    /// <summary>Gets the absolute path of one culture's resource file.</summary>
    /// <param name="culture">The culture name, which is also the folder name.</param>
    /// <returns>The path, whether or not the file exists.</returns>
    public string ResourceFileFor(string culture)
        => Path.Combine(ResourcesDirectory, culture, ResourceFileName);

    /// <summary>Gets one culture's resource file path, relative to the project directory.</summary>
    /// <param name="culture">The culture name.</param>
    /// <returns>The relative path, with backslash separators, as Visual Studio writes it.</returns>
    public string RelativeResourceFileFor(string culture)
        => Path.Combine(ResourcesPath, culture, ResourceFileName).Replace('/', '\\');

    /// <summary>Finds the project to work on and reads everything the tooling needs from it.</summary>
    /// <param name="startDirectory">The directory to search from, normally the working directory.</param>
    /// <param name="projectPath">An explicit project file, which skips the search.</param>
    /// <param name="resourcesPath">The folder holding culture subfolders. Defaults to <c>Strings</c>.</param>
    /// <param name="resourceFileName">The resource file name. Defaults to <c>Resources.resw</c>.</param>
    /// <returns>The discovered context.</returns>
    /// <exception cref="FileNotFoundException">No project could be found, or the given one does not exist.</exception>
    /// <exception cref="InvalidOperationException">Several candidate projects were found.</exception>
    public static ProjectContext Discover(
        string startDirectory,
        string? projectPath = null,
        string? resourcesPath = null,
        string? resourceFileName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);

        string resources = resourcesPath ?? "Strings";
        string fileName = resourceFileName ?? "Resources.resw";
        string projectFile = Path.GetFullPath(projectPath ?? FindProject(startDirectory, resources));

        if (!File.Exists(projectFile))
        {
            throw new FileNotFoundException($"Project file '{projectFile}' does not exist.", projectFile);
        }

        string projectDirectory = Path.GetDirectoryName(projectFile)!;
        string? solutionDirectory = FindSolutionDirectory(projectDirectory);

        return new ProjectContext(
            projectFile,
            solutionDirectory,
            resources,
            fileName,
            ReadProperty(projectFile, solutionDirectory, DefaultLanguageProperty),
            ParseCultureList(ReadProperty(projectFile, solutionDirectory, CulturesProperty)),
            FindCultureFolders(Path.Combine(projectDirectory, resources), fileName),
            solutionDirectory is null ? [] : ResourceExplorerSettings.Discover(solutionDirectory));
    }

    private static string FindProject(string startDirectory, string resourcesPath)
    {
        string[] here = Directory.GetFiles(startDirectory, "*.csproj", SearchOption.TopDirectoryOnly);
        if (here.Length == 1)
        {
            return here[0];
        }

        if (here.Length > 1)
        {
            throw new InvalidOperationException(
                $"Several projects are in '{startDirectory}'. Pass --project to choose one: "
                + string.Join(", ", here.Select(Path.GetFileName)));
        }

        // Fall back to the project that actually has a resources folder, which is the one meant.
        List<string> candidates = [];
        foreach (string project in EnumerateProjects(startDirectory))
        {
            if (Directory.Exists(Path.Combine(Path.GetDirectoryName(project)!, resourcesPath)))
            {
                candidates.Add(project);
            }
        }

        return candidates.Count switch
        {
            1 => candidates[0],
            0 => throw new FileNotFoundException(
                $"No project with a '{resourcesPath}' folder was found under '{startDirectory}'. Pass --project."),
            _ => throw new InvalidOperationException(
                "Several projects have a resources folder. Pass --project to choose one: "
                + string.Join(", ", candidates.Select(Path.GetFileName))),
        };
    }

    private static IEnumerable<string> EnumerateProjects(string root)
    {
        EnumerationOptions options = new()
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = 4,
            IgnoreInaccessible = true,
        };

        foreach (string project in Directory.EnumerateFiles(root, "*.csproj", options))
        {
            string path = project.Replace('/', '\\');
            if (path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
                || path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase)
                || path.Contains("\\.vs\\", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return project;
        }
    }

    private static string? FindSolutionDirectory(string projectDirectory)
    {
        for (DirectoryInfo? directory = new(projectDirectory); directory is not null; directory = directory.Parent)
        {
            if (directory.EnumerateFiles("*.slnx").Any()
                || directory.EnumerateFiles("*.sln").Any()
                || Directory.Exists(Path.Combine(directory.FullName, ".vs")))
            {
                return directory.FullName;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> FindCultureFolders(string resourcesDirectory, string fileName)
    {
        if (!Directory.Exists(resourcesDirectory))
        {
            return [];
        }

        List<string> cultures = [];
        foreach (string directory in Directory.EnumerateDirectories(resourcesDirectory))
        {
            if (File.Exists(Path.Combine(directory, fileName)))
            {
                cultures.Add(Path.GetFileName(directory));
            }
        }

        cultures.Sort(StringComparer.OrdinalIgnoreCase);
        return cultures;
    }

    private static IReadOnlyList<string> ParseCultureList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return [.. value
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Reads a property out of the project file, then out of any <c>Directory.Build.props</c> above
    /// it. This is a deliberate shortcut: evaluating MSBuild properly would mean taking a dependency
    /// on the MSBuild API for two string properties.
    /// </summary>
    private static string? ReadProperty(string projectFile, string? solutionDirectory, string name)
    {
        string? value = ReadPropertyFromFile(projectFile, name);
        if (value is not null)
        {
            return value;
        }

        DirectoryInfo? directory = new FileInfo(projectFile).Directory;
        while (directory is not null)
        {
            string props = Path.Combine(directory.FullName, "Directory.Build.props");
            if (File.Exists(props))
            {
                value = ReadPropertyFromFile(props, name);
                if (value is not null)
                {
                    return value;
                }
            }

            if (solutionDirectory is not null
                && string.Equals(directory.FullName, solutionDirectory, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string? ReadPropertyFromFile(string file, string name)
    {
        try
        {
            XDocument document = XDocument.Load(file);
            if (document.Root is null)
            {
                return null;
            }

            XNamespace ns = document.Root.GetDefaultNamespace();
            foreach (XElement group in document.Root.Elements(ns + "PropertyGroup"))
            {
                XElement? property = group.Element(ns + name);
                if (property is not null && !string.IsNullOrWhiteSpace(property.Value))
                {
                    return property.Value.Trim();
                }
            }
        }
        catch (System.Xml.XmlException)
        {
            // A project we cannot parse simply has no properties to offer.
        }

        return null;
    }
}
