using System.Globalization;

namespace LocalizationKit;

/// <summary>
/// Formats values in the culture selected on <see cref="ILocalizationContext"/>, or in an explicit
/// culture obtained through <see cref="In(string)"/>.
/// </summary>
internal sealed class LocalizationFormatter : ILocalizationFormatter
{
    private readonly ILocalizationContext? _context;
    private readonly CultureInfo? _fixedCulture;

    public LocalizationFormatter(ILocalizationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    private LocalizationFormatter(CultureInfo culture) => _fixedCulture = culture;

    public CultureInfo Culture => _fixedCulture ?? _context!.Culture;

    public ILocalizationFormatter In(string cultureName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);
        return In(CultureInfo.GetCultureInfo(cultureName));
    }

    public ILocalizationFormatter In(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return new LocalizationFormatter(culture);
    }

    public string Number(decimal value, string? format = null)
        => value.ToString(format ?? "N", Culture);

    public string Percent(decimal value, int decimals = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(decimals);
        return value.ToString("P" + decimals.ToString(CultureInfo.InvariantCulture), Culture);
    }

    public string Currency(decimal value, string currencyCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);

        CultureInfo culture = Culture;
        NumberFormatInfo format = CurrencyFormats.For(culture, currencyCode);
        return value.ToString("C", format);
    }

    public string Date(DateTime value, string? format = null)
        => value.ToString(format ?? "d", Culture);

    public string Date(DateTimeOffset value, string? format = null)
        => value.ToString(format ?? "d", Culture);

    public string Date(DateOnly value, string? format = null)
        => value.ToString(format ?? "d", Culture);

    public string Time(DateTime value, string? format = null)
        => value.ToString(format ?? "t", Culture);

    public string Time(TimeOnly value, string? format = null)
        => value.ToString(format ?? "t", Culture);
}
