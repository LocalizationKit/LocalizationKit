using System.Globalization;
using System.Reflection;
using System.Text;

namespace LocalizationKit;

/// <summary>Configuration for the YAML resource provider.</summary>
public sealed class YamlLocalizationOptions
{
    /// <summary>Gets or sets the folder holding one file per culture, relative to each root directory.</summary>
    public string ResourcesPath { get; set; } = "Localization";

    /// <summary>
    /// Gets or sets the file name patterns to try, where <c>{culture}</c> is replaced by the culture
    /// name. The first one that exists is used.
    /// </summary>
    /// <remarks>
    /// Both extensions are in the default list because both are in common use and neither is more
    /// correct than the other. Set it to <c>{culture}/strings.yaml</c> for a folder per culture.
    /// </remarks>
    public IList<string> FileNameFormats { get; set; } = ["{culture}.yaml", "{culture}.yml"];

    /// <summary>
    /// Gets or sets the directory the resource folder is resolved against. Defaults to the
    /// application's base directory.
    /// </summary>
    public string? RootDirectory { get; set; }

    /// <summary>Gets the directories searched before <see cref="RootDirectory"/>, for translations shipped later.</summary>
    public IList<string> OverrideDirectories { get; } = [];

    /// <summary>Gets the assemblies whose embedded resources are searched when no file is found on disk.</summary>
    public IList<Assembly> EmbeddedResourceAssemblies { get; } = [];
}

/// <summary>Loads translations from YAML files, flat or nested.</summary>
/// <remarks>
/// For the places a translator reads the file: comments survive, long messages fold over several
/// lines, and quoting is optional. Keys land in the same space as the other providers', so a project
/// can run this alongside <c>.resw</c> or JSON.
/// </remarks>
public sealed class YamlResourceProvider : ILocalizationResourceProvider
{
    private readonly YamlLocalizationOptions _options;

    /// <summary>Initializes a new instance of the <see cref="YamlResourceProvider"/> class.</summary>
    /// <param name="options">Where the files live and what they are called.</param>
    public YamlResourceProvider(YamlLocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc/>
    public string Name => "yaml";

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyDictionary<string, string>?> LoadAsync(
        CultureInfo culture,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(culture);

        Dictionary<string, string>? merged = null;

        IReadOnlyList<string> relativePaths =
        [
            .. _options.FileNameFormats
                .Where(format => !string.IsNullOrWhiteSpace(format))
                .Select(format => format.Replace("{culture}", culture.Name, StringComparison.Ordinal)),
        ];

        foreach (string root in Roots())
        {
            foreach (string relative in relativePaths)
            {
                string path = Path.Combine(root, _options.ResourcesPath, relative);
                if (!File.Exists(path))
                {
                    continue;
                }

                string content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                using StringReader reader = new(content);
                Merge(ref merged, YamlResourceFile.Read(reader));

                // One file per culture per root: a project with both .yaml and .yml for the same
                // culture has a mistake, not two sources.
                break;
            }
        }

        foreach (Assembly assembly in _options.EmbeddedResourceAssemblies)
        {
            foreach (string relative in relativePaths)
            {
                using Stream? stream = OpenEmbedded(assembly, relative);
                if (stream is null)
                {
                    continue;
                }

                using StreamReader reader = new(stream, Encoding.UTF8);
                Merge(ref merged, YamlResourceFile.Read(reader));
                break;
            }
        }

        return merged;
    }

    private IEnumerable<string> Roots()
    {
        // Override directories come first so a later-shipped translation wins over the built-in one.
        foreach (string directory in _options.OverrideDirectories)
        {
            yield return directory;
        }

        yield return _options.RootDirectory ?? AppContext.BaseDirectory;
    }

    private static void Merge(ref Dictionary<string, string>? merged, IReadOnlyDictionary<string, string> entries)
    {
        merged ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> entry in entries)
        {
            // Earlier sources win, matching the provider ordering rule in the engine.
            merged.TryAdd(entry.Key, entry.Value);
        }
    }

    private Stream? OpenEmbedded(Assembly assembly, string relative)
    {
        string suffix = "." + _options.ResourcesPath.Replace('\\', '.').Replace('/', '.')
            + "." + relative.Replace('\\', '.').Replace('/', '.');

        foreach (string name in assembly.GetManifestResourceNames())
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return assembly.GetManifestResourceStream(name);
            }
        }

        return null;
    }
}
