using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace LocalizationKit;

/// <summary>
/// Loads cultures through the registered providers, caches what it has parsed, and answers lookups
/// synchronously afterwards.
/// </summary>
/// <remarks>
/// Providers are consulted in registration order and the first one that has a key wins, so a
/// database or remote provider registered before the file provider can override shipped translations.
/// Keys are compared case-insensitively, matching how Windows resource names behave.
/// </remarks>
internal sealed class ResourceCatalog
{
    private readonly ILocalizationResourceProvider[] _providers;
    private readonly CultureInfo _defaultCulture;
    private readonly bool _pseudolocalize;
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> _entriesByCulture =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, CultureResourceSet> _setsByCulture =
        new(StringComparer.OrdinalIgnoreCase);

    public ResourceCatalog(IEnumerable<ILocalizationResourceProvider> providers, LocalizationKitOptions options)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(options);

        _providers = [.. providers];
        _defaultCulture = CultureInfo.GetCultureInfo(options.DefaultCulture);
        _pseudolocalize = options.Pseudolocalize;
    }

    /// <summary>Gets a value indicating whether any culture has been loaded.</summary>
    public bool IsInitialized => !_setsByCulture.IsEmpty;

    /// <summary>Gets a value indicating whether one culture's resources are loaded and ready for lookup.</summary>
    /// <param name="culture">The culture to ask about.</param>
    /// <returns><see langword="true"/> when the culture can be resolved synchronously.</returns>
    public bool IsLoaded(CultureInfo culture) => _setsByCulture.ContainsKey(culture.Name);

    /// <summary>Gets the names of the providers in use, for diagnostics.</summary>
    public IEnumerable<string> ProviderNames => _providers.Select(p => p.Name);

    /// <summary>Loads <paramref name="culture"/> and every culture in its fallback chain.</summary>
    /// <param name="culture">The culture to make available for lookup.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The resource set for <paramref name="culture"/>.</returns>
    public async Task<CultureResourceSet> LoadAsync(CultureInfo culture, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(culture);

        IReadOnlyList<CultureInfo> chain = CultureFallback.BuildChain(culture, _defaultCulture);

        // Build from the end of the chain backwards so each set can link to the one it falls back to.
        CultureResourceSet? fallback = null;
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            CultureInfo link = chain[i];
            IReadOnlyDictionary<string, string> entries = await GetEntriesAsync(link, cancellationToken)
                .ConfigureAwait(false);

            CultureResourceSet set = new(link, entries, fallback);
            _setsByCulture[link.Name] = set;
            fallback = set;
        }

        // The loop ends on chain[0], which is the requested culture.
        return fallback ?? new CultureResourceSet(culture, EmptyEntries, null);
    }

    /// <summary>Resolves a key in an already-loaded culture.</summary>
    /// <param name="key">The resource key.</param>
    /// <param name="culture">The UI culture to resolve in.</param>
    /// <param name="value">The value, when found.</param>
    /// <returns><see langword="true"/> when the key resolves through that culture's chain.</returns>
    public bool TryGet(string key, CultureInfo culture, [NotNullWhen(true)] out string? value)
    {
        if (_setsByCulture.TryGetValue(culture.Name, out CultureResourceSet? set))
        {
            return set.TryGet(key, out value);
        }

        value = null;
        return false;
    }

    /// <summary>Gets the fallback chain of a loaded culture, for diagnostics.</summary>
    /// <param name="culture">The culture to describe.</param>
    /// <returns>The chain, or just the culture itself when it has not been loaded.</returns>
    public IReadOnlyList<CultureInfo> GetChain(CultureInfo culture)
        => _setsByCulture.TryGetValue(culture.Name, out CultureResourceSet? set) ? set.GetChain() : [culture];

    /// <summary>Gets every entry available in a loaded culture, including inherited ones.</summary>
    /// <param name="culture">The culture to enumerate.</param>
    /// <param name="includeFallbacks">Whether to include entries inherited through the fallback chain.</param>
    /// <returns>
    /// The entries, with a culture's own value winning over an inherited one, or an empty map when the
    /// culture has not been loaded.
    /// </returns>
    public IReadOnlyDictionary<string, string> GetAll(CultureInfo culture, bool includeFallbacks = true)
    {
        Dictionary<string, string> entries = new(StringComparer.OrdinalIgnoreCase);

        if (_setsByCulture.TryGetValue(culture.Name, out CultureResourceSet? set))
        {
            // Walking the chain outwards and keeping the first value seen gives the same answer a
            // lookup would give for each key.
            for (CultureResourceSet? current = set;
                 current is not null;
                 current = includeFallbacks ? current.Fallback : null)
            {
                foreach (KeyValuePair<string, string> entry in current.Entries)
                {
                    entries.TryAdd(entry.Key, entry.Value);
                }
            }
        }

        return entries;
    }

    /// <summary>Gets every key available in a loaded culture, including inherited ones.</summary>
    /// <param name="culture">The culture to enumerate.</param>
    /// <returns>The keys, or an empty set when the culture has not been loaded.</returns>
    public IReadOnlySet<string> GetKeys(CultureInfo culture)
    {
        HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase);
        if (_setsByCulture.TryGetValue(culture.Name, out CultureResourceSet? set))
        {
            for (CultureResourceSet? current = set; current is not null; current = current.Fallback)
            {
                foreach (string key in current.Entries.Keys)
                {
                    keys.Add(key);
                }
            }
        }

        return keys;
    }

    /// <summary>Drops every parsed resource so the next load goes back to the providers.</summary>
    public void Clear()
    {
        _entriesByCulture.Clear();
        _setsByCulture.Clear();
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyEntries =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private async Task<IReadOnlyDictionary<string, string>> GetEntriesAsync(
        CultureInfo culture,
        CancellationToken cancellationToken)
    {
        if (_entriesByCulture.TryGetValue(culture.Name, out IReadOnlyDictionary<string, string>? cached))
        {
            return cached;
        }

        Dictionary<string, string> merged = new(StringComparer.OrdinalIgnoreCase);
        foreach (ILocalizationResourceProvider provider in _providers)
        {
            IReadOnlyDictionary<string, string>? entries =
                await provider.LoadAsync(culture, cancellationToken).ConfigureAwait(false);

            if (entries is null)
            {
                continue;
            }

            foreach (KeyValuePair<string, string> entry in entries)
            {
                // First provider wins, so registration order is the override order.
                //
                // Rewriting here rather than at lookup time means it happens once per string instead
                // of once per read, and that everything downstream — the formatter, the validator,
                // GetAll — sees the same text the screen will.
                merged.TryAdd(
                    entry.Key,
                    _pseudolocalize ? Pseudolocalizer.Transform(entry.Value) : entry.Value);
            }
        }

        // A concurrent load of the same culture is wasteful but not wrong; keep the first result.
        return _entriesByCulture.GetOrAdd(culture.Name, merged);
    }
}
