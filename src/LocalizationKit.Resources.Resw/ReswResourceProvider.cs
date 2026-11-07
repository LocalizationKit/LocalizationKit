using System.Globalization;
using System.Reflection;

namespace LocalizationKit;

/// <summary>
/// Loads translations from loose or embedded <c>.resw</c> files, deliberately bypassing
/// <c>ResourceLoader</c> and the compiled PRI.
/// </summary>
/// <remarks>
/// <para>
/// MRTCore resolves a language through the ambient resource context, and <c>x:Uid</c> is applied when
/// a page is loaded, so a PRI-based lookup cannot refresh a UI that is already on screen. Parsing the
/// files directly makes a culture switch an ordinary dictionary swap, and makes the same provider work
/// in a console application or on a server.
/// </para>
/// <para>
/// PRI still has a job in a packaged app: the manifest, the display name and asset qualifiers. This
/// provider does not interfere with any of that.
/// </para>
/// </remarks>
public sealed class ReswResourceProvider : ILocalizationResourceProvider
{
    private readonly ReswLocalizationOptions _options;

    /// <summary>Initializes a new instance of the <see cref="ReswResourceProvider"/> class.</summary>
    /// <param name="options">Where the files live and what they are called.</param>
    public ReswResourceProvider(ReswLocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc/>
    public string Name => "resw";

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyDictionary<string, string>?> LoadAsync(
        CultureInfo culture,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(culture);

        Dictionary<string, string>? merged = null;

        foreach (string path in EnumerateCandidatePaths(culture))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            using MemoryStream stream = new(bytes, writable: false);
            Merge(ref merged, ReswFile.ReadValues(stream));
        }

        foreach (Assembly assembly in _options.EmbeddedResourceAssemblies)
        {
            using Stream? stream = OpenEmbedded(assembly, culture);
            if (stream is not null)
            {
                Merge(ref merged, ReswFile.ReadValues(stream));
            }
        }

        return merged;
    }

    /// <summary>
    /// Lists the culture folders that actually exist under the configured roots, which is how a host
    /// can discover what it ships without hardcoding a culture list.
    /// </summary>
    /// <param name="options">The provider options to inspect.</param>
    /// <returns>The culture names found, sorted, with no duplicates.</returns>
    public static IReadOnlyList<string> DiscoverCultures(ReswLocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        SortedSet<string> cultures = new(StringComparer.OrdinalIgnoreCase);
        foreach (string root in EnumerateRoots(options))
        {
            string resourcesRoot = Path.Combine(root, options.ResourcesPath);
            if (!Directory.Exists(resourcesRoot))
            {
                continue;
            }

            foreach (string directory in Directory.EnumerateDirectories(resourcesRoot))
            {
                if (File.Exists(Path.Combine(directory, options.FileName)))
                {
                    cultures.Add(Path.GetFileName(directory));
                }
            }
        }

        return [.. cultures];
    }

    private static IEnumerable<string> EnumerateRoots(ReswLocalizationOptions options)
    {
        // Override directories come first so a later-shipped translation wins over the built-in one.
        foreach (string directory in options.OverrideDirectories)
        {
            yield return directory;
        }

        yield return options.RootDirectory ?? AppContext.BaseDirectory;
    }

    private IEnumerable<string> EnumerateCandidatePaths(CultureInfo culture)
    {
        foreach (string root in EnumerateRoots(_options))
        {
            yield return Path.Combine(root, _options.ResourcesPath, culture.Name, _options.FileName);
        }
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

    private Stream? OpenEmbedded(Assembly assembly, CultureInfo culture)
    {
        string suffix = string.Concat(
            ".",
            _options.ResourcesPath.Replace('\\', '.').Replace('/', '.'),
            ".",
            culture.Name,
            ".",
            _options.FileName);

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
