using System.Globalization;

namespace LocalizationKit;

/// <summary>
/// Turns a message pattern taken from a resource, plus its arguments, into a finished string.
/// </summary>
/// <remarks>
/// <para>
/// This is the seam for ICU MessageFormat. The implementation registered by default,
/// <c>SimpleMessageFormatter</c>, handles argument substitution and ICU apostrophe quoting only, and
/// refuses the <c>plural</c>, <c>selectordinal</c> and <c>select</c> constructs with
/// <see cref="UnsupportedMessageConstructException"/> rather than guessing at a plural category.
/// </para>
/// <para>
/// Registering another implementation replaces it. That is how <c>LocalizationKit.Messages.Icu</c>
/// brings CLDR plural rules, ordinals, gender selection and in-message number and date formatting
/// without a single change to <see cref="ILocalizer"/> or to any call site.
/// </para>
/// </remarks>
public interface IMessageFormatter
{
    /// <summary>Formats a message pattern.</summary>
    /// <param name="message">The pattern, as stored in the resource.</param>
    /// <param name="arguments">The arguments, keyed by name.</param>
    /// <param name="culture">The culture to format argument values in.</param>
    /// <returns>The formatted message.</returns>
    /// <exception cref="FormatException">The pattern is malformed, or a referenced argument was not supplied.</exception>
    /// <exception cref="UnsupportedMessageConstructException">The pattern uses a construct this implementation does not support.</exception>
    string Format(string message, IReadOnlyDictionary<string, object?> arguments, CultureInfo culture);

    /// <summary>
    /// Checks a pattern without formatting it, so that build-time validation can report a malformed
    /// message without inventing argument values.
    /// </summary>
    /// <param name="message">The pattern to check.</param>
    /// <param name="error">The reason the pattern is invalid, when this method returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when the pattern is well formed.</returns>
    bool TryValidate(string message, out string? error);
}
