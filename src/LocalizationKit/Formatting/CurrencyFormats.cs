using System.Collections.Concurrent;
using System.Globalization;

namespace LocalizationKit;

/// <summary>
/// Produces a <see cref="NumberFormatInfo"/> that keeps a culture's own grouping, decimal separator
/// and symbol placement while showing the symbol of a specific ISO 4217 currency.
/// </summary>
/// <remarks>
/// That combination is what an application usually wants: an Italian user looking at a price in
/// dollars expects <c>1.234,50 $</c>, not <c>$1,234.50</c>. The symbol lookup is best-effort and
/// falls back to the ISO code itself, which is an acceptable rendering for any currency .NET has no
/// symbol for.
/// </remarks>
internal static class CurrencyFormats
{
    private static readonly ConcurrentDictionary<(string Culture, string Code), NumberFormatInfo> Cache = new();
    private static readonly Lazy<Dictionary<string, string>> SymbolsByCode = new(BuildSymbolTable);

    /// <summary>Gets the number format to use for an amount in <paramref name="currencyCode"/>.</summary>
    /// <param name="culture">The culture whose conventions apply.</param>
    /// <param name="currencyCode">An ISO 4217 code, such as <c>EUR</c>.</param>
    /// <returns>A cached, read-only number format.</returns>
    public static NumberFormatInfo For(CultureInfo culture, string currencyCode)
    {
        string code = currencyCode.ToUpperInvariant();
        return Cache.GetOrAdd((culture.Name, code), static key => Build(key.Culture, key.Code));
    }

    private static NumberFormatInfo Build(string cultureName, string code)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);

        if (string.Equals(GetCultureCurrency(culture), code, StringComparison.OrdinalIgnoreCase))
        {
            return culture.NumberFormat;
        }

        NumberFormatInfo format = (NumberFormatInfo)culture.NumberFormat.Clone();
        format.CurrencySymbol = SymbolsByCode.Value.TryGetValue(code, out string? symbol) ? symbol : code;
        return NumberFormatInfo.ReadOnly(format);
    }

    private static string? GetCultureCurrency(CultureInfo culture)
    {
        if (culture.IsNeutralCulture || culture.Equals(CultureInfo.InvariantCulture))
        {
            return null;
        }

        try
        {
            return new RegionInfo(culture.Name).ISOCurrencySymbol;
        }
        catch (ArgumentException)
        {
            // Not every culture name maps to a region; treat it as "no currency of its own".
            return null;
        }
    }

    private static Dictionary<string, string> BuildSymbolTable()
    {
        Dictionary<string, string> symbols = new(StringComparer.OrdinalIgnoreCase);

        foreach (CultureInfo culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                RegionInfo region = new(culture.Name);
                string symbol = region.CurrencySymbol;
                if (!string.IsNullOrEmpty(symbol))
                {
                    symbols.TryAdd(region.ISOCurrencySymbol, symbol);
                }
            }
            catch (ArgumentException)
            {
                // Skip culture names that are not regions.
            }
        }

        return symbols;
    }
}
