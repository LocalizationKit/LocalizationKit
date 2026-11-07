using System.Globalization;

namespace LocalizationKit;

/// <summary>Builds the ordered list of cultures a lookup walks before giving up.</summary>
internal static class CultureFallback
{
    /// <summary>
    /// Builds the chain for <paramref name="culture"/>: the culture itself, each of its parents, then
    /// the default culture and its parents. For <c>pt-BR</c> with a default of <c>en-US</c> that is
    /// <c>pt-BR</c>, <c>pt</c>, <c>en-US</c>, <c>en</c>.
    /// </summary>
    /// <param name="culture">The requested culture.</param>
    /// <param name="defaultCulture">The culture every chain ends at.</param>
    /// <returns>The chain, in search order, with no duplicates and without the invariant culture.</returns>
    public static IReadOnlyList<CultureInfo> BuildChain(CultureInfo culture, CultureInfo defaultCulture)
    {
        List<CultureInfo> chain = [];
        AppendWithParents(chain, culture);
        AppendWithParents(chain, defaultCulture);
        return chain;
    }

    private static void AppendWithParents(List<CultureInfo> chain, CultureInfo culture)
    {
        for (CultureInfo? current = culture;
             current is not null && !string.IsNullOrEmpty(current.Name);
             current = current.Parent)
        {
            if (!Contains(chain, current))
            {
                chain.Add(current);
            }
        }
    }

    private static bool Contains(List<CultureInfo> chain, CultureInfo culture)
    {
        foreach (CultureInfo existing in chain)
        {
            if (string.Equals(existing.Name, culture.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
