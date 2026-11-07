using System.Collections.Concurrent;
using System.Globalization;
using Rh.MessageFormat.Exceptions;
using RhFormatter = Rh.MessageFormat.MessageFormatter;

namespace LocalizationKit;

/// <summary>
/// The ICU MessageFormat engine: CLDR pluralization, ordinals, gender selection, nesting, offsets and
/// in-message number and date formatting.
/// </summary>
/// <remarks>
/// <para>
/// Formatting is delegated to <c>Rh.MessageFormat</c>, which carries the CLDR data for 200+ locales.
/// Plural rules are not something to hand-roll: English and Italian need <c>one</c> and <c>other</c>,
/// Polish needs <c>one</c>, <c>few</c>, <c>many</c> and <c>other</c>, and Arabic uses all six
/// categories.
/// </para>
/// <para>
/// Validation is deliberately not delegated. <see cref="IcuMessageSyntax"/> does it, so a pattern is
/// checked the same way whether or not this package is installed, and so that a misspelled plural
/// category is reported rather than silently rendered as <c>other</c>.
/// </para>
/// <para>
/// One behaviour is tightened on top of the engine: an argument the pattern refers to but the caller
/// did not supply is a <see cref="FormatException"/>, not an empty string. A typo in an argument name
/// should fail where it is made, not ship a sentence with a hole in it.
/// </para>
/// </remarks>
public sealed class IcuMessageFormatter : IMessageFormatter
{
    private readonly ConcurrentDictionary<string, RhFormatter?> _formatters = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _argumentNames = new(StringComparer.Ordinal);
    private readonly SimpleMessageFormatter _withoutCldr = new();

    /// <summary>
    /// Reports whether the engine has CLDR data for a culture, directly or through one of its parents.
    /// </summary>
    /// <param name="culture">The culture to ask about.</param>
    /// <returns>
    /// <see langword="true"/> when a plural or select construct can be resolved for that culture.
    /// </returns>
    /// <remarks>
    /// The data covers 200+ locales but not every tag .NET knows: deprecated ISO codes such as
    /// <c>iw</c>, <c>in</c> and <c>tl</c>, and a tail of rarely requested languages, are absent. A
    /// plain message still works for those; see <see cref="Format"/>.
    /// </remarks>
    public bool SupportsCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return Formatter(culture) is not null;
    }

    /// <inheritdoc/>
    /// <exception cref="FormatException">
    /// The pattern is malformed, or it refers to an argument that was not supplied.
    /// </exception>
    public string Format(string message, IReadOnlyDictionary<string, object?> arguments, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(culture);

        foreach (string name in _argumentNames.GetOrAdd(message, IcuMessageSyntax.GetArgumentNames))
        {
            if (!arguments.ContainsKey(name))
            {
                throw new FormatException(
                    $"Message argument '{name}' was not supplied. Message: \"{message}\".");
            }
        }

        RhFormatter? engine = Formatter(culture);
        if (engine is null)
        {
            return WithoutCldrData(message, arguments, culture);
        }

        try
        {
            return engine.FormatMessage(message, arguments);
        }
        catch (MessageFormatterException ex)
        {
            throw new FormatException($"{ex.Message} Message: \"{message}\".", ex);
        }
    }

    /// <summary>
    /// Handles a culture the CLDR data does not cover.
    /// </summary>
    /// <remarks>
    /// A plain message needs no plural rules, so it is substituted as usual and the application keeps
    /// working. A message that does need them cannot be rendered, and saying so is the only honest
    /// option: picking another language's rules would quietly produce the wrong grammar, which is
    /// exactly the bug this library exists to prevent.
    /// </remarks>
    private string WithoutCldrData(
        string message,
        IReadOnlyDictionary<string, object?> arguments,
        CultureInfo culture)
    {
        try
        {
            return _withoutCldr.Format(message, arguments, culture);
        }
        catch (UnsupportedMessageConstructException ex)
        {
            throw new CultureDataUnavailableException(culture, ex.Construct ?? "plural", ex);
        }
    }

    /// <inheritdoc/>
    public bool TryValidate(string message, out string? error)
        => IcuMessageSyntax.TryValidate(message, out error);

    /// <summary>
    /// Gets the engine for a culture, walking up the parent chain, or <see langword="null"/> when the
    /// CLDR data covers none of them.
    /// </summary>
    /// <remarks>
    /// Walking parents is what makes <c>pt-BR</c> fall back to <c>pt</c>, and a private-use tag such
    /// as <c>en-US-x-custom</c> fall back to <c>en-US</c>. It deliberately stops there: falling back
    /// to a default language would apply one language's plural rules to another.
    /// </remarks>
    private RhFormatter? Formatter(CultureInfo culture)
        => _formatters.GetOrAdd(culture.Name, static name => Create(name));

    private static RhFormatter? Create(string name)
    {
        for (CultureInfo? candidate = name.Length == 0 ? null : CultureInfo.GetCultureInfo(name);
             candidate is not null && candidate.Name.Length > 0;
             candidate = candidate.Parent)
        {
            try
            {
                return new RhFormatter(candidate.Name);
            }
            catch (InvalidLocaleException)
            {
                // Try the parent.
            }
        }

        return null;
    }
}
