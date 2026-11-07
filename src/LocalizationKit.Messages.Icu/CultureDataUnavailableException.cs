using System.Globalization;

namespace LocalizationKit;

/// <summary>
/// Thrown when a message needs CLDR plural or select rules for a culture the engine has no data for.
/// </summary>
/// <remarks>
/// The alternative would be to borrow another language's rules, which produces confidently wrong
/// grammar: it is the failure this library exists to prevent, so it is reported instead. Plain
/// messages in the same culture keep working, since they need no rules.
/// </remarks>
public sealed class CultureDataUnavailableException : LocalizationException
{
    /// <summary>Initializes a new instance of the <see cref="CultureDataUnavailableException"/> class.</summary>
    public CultureDataUnavailableException() { }

    /// <summary>Initializes a new instance of the <see cref="CultureDataUnavailableException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public CultureDataUnavailableException(string message) : base(message) { }

    /// <summary>Initializes a new instance of the <see cref="CultureDataUnavailableException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying error.</param>
    public CultureDataUnavailableException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>Initializes a new instance of the <see cref="CultureDataUnavailableException"/> class.</summary>
    /// <param name="culture">The culture with no CLDR data.</param>
    /// <param name="construct">The ICU construct that needed it, such as <c>plural</c>.</param>
    /// <param name="innerException">The underlying error.</param>
    public CultureDataUnavailableException(CultureInfo culture, string construct, Exception innerException)
        : base(
            $"The ICU message engine has no CLDR data for culture '{culture.Name}', so the '{construct}' "
            + "construct cannot be resolved for it. Plain messages still work. Ship a culture the engine "
            + "supports, check IcuMessageFormatter.SupportsCulture before relying on plurals, or register "
            + "an IMessageFormatter with wider data.",
            innerException)
    {
        Culture = culture;
        Construct = construct;
    }

    /// <summary>Gets the culture with no CLDR data, when known.</summary>
    public CultureInfo? Culture { get; }

    /// <summary>Gets the ICU construct that needed the data, when known.</summary>
    public string? Construct { get; }
}
