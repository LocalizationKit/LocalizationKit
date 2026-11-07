using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace LocalizationKit;

/// <summary>
/// The entries loaded for one culture, linked to the set for the next culture in its fallback chain.
/// Lookup walks the links, which is what makes a resolved lookup a few dictionary probes.
/// </summary>
internal sealed class CultureResourceSet(
    CultureInfo culture,
    IReadOnlyDictionary<string, string> entries,
    CultureResourceSet? fallback)
{
    /// <summary>Gets the culture these entries belong to.</summary>
    public CultureInfo Culture { get; } = culture;

    /// <summary>Gets the entries authored for this culture exactly, with no fallback applied.</summary>
    public IReadOnlyDictionary<string, string> Entries { get; } = entries;

    /// <summary>Gets the set to consult when this one does not have a key.</summary>
    public CultureResourceSet? Fallback { get; } = fallback;

    /// <summary>Resolves a key through this set and then its fallback chain.</summary>
    /// <param name="key">The resource key.</param>
    /// <param name="value">The value, when found.</param>
    /// <returns><see langword="true"/> when some culture in the chain has the key.</returns>
    public bool TryGet(string key, [NotNullWhen(true)] out string? value)
    {
        for (CultureResourceSet? set = this; set is not null; set = set.Fallback)
        {
            if (set.Entries.TryGetValue(key, out string? found))
            {
                value = found;
                return true;
            }
        }

        value = null;
        return false;
    }

    /// <summary>Gets the cultures this set searches, in order, for diagnostics.</summary>
    /// <returns>The fallback chain starting at this set's culture.</returns>
    public IReadOnlyList<CultureInfo> GetChain()
    {
        List<CultureInfo> chain = [];
        for (CultureResourceSet? set = this; set is not null; set = set.Fallback)
        {
            chain.Add(set.Culture);
        }

        return chain;
    }
}
