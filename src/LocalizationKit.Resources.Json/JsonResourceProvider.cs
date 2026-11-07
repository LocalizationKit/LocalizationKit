using System.Globalization;
using System.Reflection;

namespace LocalizationKit;

/// <summary>Configuration for the JSON resource provider.</summary>
public sealed class JsonLocalizationOptions
{
    /// <summary>Gets or sets the folder holding one file per culture, relative to each root directory.</summary>
    public string ResourcesPath { get; set; } = "Localization";

    /// <summary>
    /// Gets or sets the file name pattern, where <c>{culture}</c> is replaced by the culture name.
    /// </summary>
    /// <remarks>
    /// The default produces <c>Localization/it-IT.json</c>. Set it to
    /// <c>{culture}/strings.json</c> for a folder per culture instead.
    /// </remarks>
    public string FileNameFormat { get; set; } = "{culture}.json";

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

/// <summary>Loads translations from JSON files, flat or nested.</summary>
/// <remarks>
/// The sibling of <c>ReswResourceProvider</c>, and the reason the provider contract is shaped the way
/// it is: a format that needs no Windows tooling, for a console application, a server, or anything
/// that would rather hand a translator a readable file than XML.
/// </remarks>
public sealed class JsonResourceProvider : ILocalizationResourceProvider
{
    private readonly JsonLocalizationOptions _options;

    /// <summary>Initializes a new instance of the <see cref="JsonResourceProvider"/> class.</summary>
    /// <param name="options">Where the files live and what they are called.</param>
    public JsonResourceProvider(JsonLocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc/>
    public string Name => "json";

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyDictionary<string, string>?> LoadAsync(
        CultureInfo culture,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(culture);

        Dictionary<string, string>? merged = null;
        string relative = _options.FileNameFormat.Replace("{culture}", culture.Name, StringComparison.Ordinal);

        foreach (string root in Roots())
        {
            string path = Path.Combine(root, _options.ResourcesPath, relative);
            if (!File.Exists(path))
            {
                continue;
            }

            byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            using MemoryStream stream = new(bytes, writable: false);
            Merge(ref merged, JsonResourceFile.Read(stream));
        }

        foreach (Assembly assembly in _options.EmbeddedResourceAssemblies)
        {
            using Stream? stream = OpenEmbedded(assembly, relative);
            if (stream is not null)
            {
                Merge(ref merged, JsonResourceFile.Read(stream));
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
