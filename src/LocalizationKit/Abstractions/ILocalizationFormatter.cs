using System.Globalization;

namespace LocalizationKit;

/// <summary>
/// Culture-aware formatting for ordinary values, using the culture selected on
/// <see cref="ILocalizationContext"/> rather than <see cref="CultureInfo.CurrentCulture"/>.
/// </summary>
/// <remarks>
/// This deliberately does not change the behaviour of .NET's own formatting APIs. Opting into
/// LocalizationKit should not quietly re-format every <c>ToString</c> call in an application.
/// </remarks>
public interface ILocalizationFormatter
{
    /// <summary>Gets the culture this formatter formats in.</summary>
    CultureInfo Culture { get; }

    /// <summary>Returns a formatter bound to an explicit culture instead of the selected one.</summary>
    /// <param name="cultureName">An IETF language tag, such as <c>de-DE</c>.</param>
    /// <returns>A formatter for that culture.</returns>
    ILocalizationFormatter In(string cultureName);

    /// <summary>Returns a formatter bound to an explicit culture instead of the selected one.</summary>
    /// <param name="culture">The culture to format in.</param>
    /// <returns>A formatter for that culture.</returns>
    ILocalizationFormatter In(CultureInfo culture);

    /// <summary>Formats a number.</summary>
    /// <param name="value">The value to format.</param>
    /// <param name="format">A standard or custom .NET numeric format string, or <see langword="null"/> for the general form.</param>
    /// <returns>The formatted number.</returns>
    string Number(decimal value, string? format = null);

    /// <summary>Formats a percentage, where <c>0.15</c> renders as 15%.</summary>
    /// <param name="value">The ratio to format.</param>
    /// <param name="decimals">The number of decimal places to show.</param>
    /// <returns>The formatted percentage.</returns>
    string Percent(decimal value, int decimals = 0);

    /// <summary>
    /// Formats a monetary amount in the given currency, using the selected culture's conventions for
    /// grouping, decimal separator and symbol placement.
    /// </summary>
    /// <param name="value">The amount to format.</param>
    /// <param name="currencyCode">An ISO 4217 code, such as <c>EUR</c>.</param>
    /// <returns>The formatted amount.</returns>
    string Currency(decimal value, string currencyCode);

    /// <summary>Formats a date and time.</summary>
    /// <param name="value">The value to format.</param>
    /// <param name="format">A standard or custom .NET date format string, or <see langword="null"/> for the short date pattern.</param>
    /// <returns>The formatted value.</returns>
    string Date(DateTime value, string? format = null);

    /// <summary>Formats a date and time with its offset.</summary>
    /// <param name="value">The value to format.</param>
    /// <param name="format">A standard or custom .NET date format string, or <see langword="null"/> for the short date pattern.</param>
    /// <returns>The formatted value.</returns>
    string Date(DateTimeOffset value, string? format = null);

    /// <summary>Formats a date with no time component.</summary>
    /// <param name="value">The value to format.</param>
    /// <param name="format">A standard or custom .NET date format string, or <see langword="null"/> for the short date pattern.</param>
    /// <returns>The formatted value.</returns>
    string Date(DateOnly value, string? format = null);

    /// <summary>Formats the time component of a value.</summary>
    /// <param name="value">The value to format.</param>
    /// <param name="format">A standard or custom .NET time format string, or <see langword="null"/> for the short time pattern.</param>
    /// <returns>The formatted value.</returns>
    string Time(DateTime value, string? format = null);

    /// <summary>Formats a time of day.</summary>
    /// <param name="value">The value to format.</param>
    /// <param name="format">A standard or custom .NET time format string, or <see langword="null"/> for the short time pattern.</param>
    /// <returns>The formatted value.</returns>
    string Time(TimeOnly value, string? format = null);
}
