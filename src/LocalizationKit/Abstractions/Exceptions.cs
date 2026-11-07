using System.Globalization;

namespace LocalizationKit;

/// <summary>Information about a key that could not be resolved.</summary>
/// <param name="key">The key that was requested.</param>
/// <param name="culture">The UI culture it was requested in.</param>
/// <param name="searchedCultures">The fallback chain that was searched, in order.</param>
public sealed class MissingResourceInfo(string key, CultureInfo culture, IReadOnlyList<CultureInfo> searchedCultures)
{
    /// <summary>Gets the key that was requested.</summary>
    public string Key { get; } = key;

    /// <summary>Gets the UI culture it was requested in.</summary>
    public CultureInfo Culture { get; } = culture;

    /// <summary>Gets the fallback chain that was searched, in order.</summary>
    public IReadOnlyList<CultureInfo> SearchedCultures { get; } = searchedCultures;
}

/// <summary>The base type for the errors this library raises on purpose.</summary>
public class LocalizationException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="LocalizationException"/> class.</summary>
    public LocalizationException() { }

    /// <summary>Initializes a new instance of the <see cref="LocalizationException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public LocalizationException(string message) : base(message) { }

    /// <summary>Initializes a new instance of the <see cref="LocalizationException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying error.</param>
    public LocalizationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a key is missing from every culture in the fallback chain and
/// <see cref="LocalizationKitOptions.FallbackBehavior"/> is <see cref="FallbackBehavior.Throw"/>.
/// </summary>
public sealed class MissingResourceException : LocalizationException
{
    /// <summary>Initializes a new instance of the <see cref="MissingResourceException"/> class.</summary>
    public MissingResourceException() { }

    /// <summary>Initializes a new instance of the <see cref="MissingResourceException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public MissingResourceException(string message) : base(message) { }

    /// <summary>Initializes a new instance of the <see cref="MissingResourceException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying error.</param>
    public MissingResourceException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>Initializes a new instance of the <see cref="MissingResourceException"/> class.</summary>
    /// <param name="key">The key that was requested.</param>
    /// <param name="culture">The UI culture it was requested in.</param>
    public MissingResourceException(string key, CultureInfo culture)
        : base($"Resource '{key}' was not found for culture '{culture.Name}' or any culture in its fallback chain.")
    {
        Key = key;
        Culture = culture;
    }

    /// <summary>Gets the key that was requested, when known.</summary>
    public string? Key { get; }

    /// <summary>Gets the UI culture it was requested in, when known.</summary>
    public CultureInfo? Culture { get; }
}

/// <summary>Thrown when a culture is not in <see cref="ILocalizationContext.SupportedCultures"/>.</summary>
public sealed class CultureNotSupportedException : LocalizationException
{
    /// <summary>Initializes a new instance of the <see cref="CultureNotSupportedException"/> class.</summary>
    public CultureNotSupportedException() { }

    /// <summary>Initializes a new instance of the <see cref="CultureNotSupportedException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public CultureNotSupportedException(string message) : base(message) { }

    /// <summary>Initializes a new instance of the <see cref="CultureNotSupportedException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying error.</param>
    public CultureNotSupportedException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>Initializes a new instance of the <see cref="CultureNotSupportedException"/> class.</summary>
    /// <param name="requested">The culture that was requested.</param>
    /// <param name="supported">The cultures the application supports.</param>
    public CultureNotSupportedException(string requested, IEnumerable<string> supported)
        : base($"Culture '{requested}' is not supported. Supported cultures: {string.Join(", ", supported)}.")
    {
        RequestedCulture = requested;
    }

    /// <summary>Gets the culture that was requested, when known.</summary>
    public string? RequestedCulture { get; }
}

/// <summary>
/// Thrown when a message uses an ICU construct the registered <see cref="IMessageFormatter"/> does not
/// implement, rather than silently producing a wrong string.
/// </summary>
public sealed class UnsupportedMessageConstructException : LocalizationException
{
    /// <summary>Initializes a new instance of the <see cref="UnsupportedMessageConstructException"/> class.</summary>
    public UnsupportedMessageConstructException() { }

    /// <summary>Initializes a new instance of the <see cref="UnsupportedMessageConstructException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public UnsupportedMessageConstructException(string message) : base(message) { }

    /// <summary>Initializes a new instance of the <see cref="UnsupportedMessageConstructException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying error.</param>
    public UnsupportedMessageConstructException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>Initializes a new instance of the <see cref="UnsupportedMessageConstructException"/> class.</summary>
    /// <param name="construct">The ICU construct that is not supported, such as <c>plural</c>.</param>
    /// <param name="argumentName">The message argument it applies to.</param>
    /// <param name="key">The resource key the message came from, when known.</param>
    public UnsupportedMessageConstructException(string construct, string argumentName, string? key)
        : base(key is null
            ? $"The ICU '{construct}' construct on argument '{argumentName}' is not supported by the message engine in use. Add the LocalizationKit.Messages.Icu package and call AddIcuMessages() to get CLDR pluralization, ordinals and gender selection."
            : $"Resource '{key}' uses the ICU '{construct}' construct on argument '{argumentName}', which the message engine in use does not support. Add the LocalizationKit.Messages.Icu package and call AddIcuMessages() to get CLDR pluralization, ordinals and gender selection.")
    {
        Construct = construct;
        ArgumentName = argumentName;
        Key = key;
    }

    /// <summary>Gets the ICU construct that is not supported, when known.</summary>
    public string? Construct { get; }

    /// <summary>Gets the message argument the construct applies to, when known.</summary>
    public string? ArgumentName { get; }

    /// <summary>Gets the resource key the message came from, when known.</summary>
    public string? Key { get; }
}
