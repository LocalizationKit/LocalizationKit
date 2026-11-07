using System.Globalization;

namespace LocalizationKit;

/// <summary>Maps a requested culture onto one the application actually ships.</summary>
internal static class CultureNegotiator
{
    /// <summary>
    /// Finds the best supported culture for <paramref name="requested"/>: an exact match first, then a
    /// culture sharing the same language, so a request for <c>it</c> resolves to a shipped <c>it-IT</c>,
    /// and so does a request for another Italian region the application does not ship.
    /// </summary>
    /// <param name="requested">The culture that was asked for.</param>
    /// <param name="supported">The cultures the application ships. An empty list means anything goes.</param>
    /// <returns>The culture to select, or <see langword="null"/> when none matches.</returns>
    public static CultureInfo? Negotiate(CultureInfo requested, IReadOnlyList<CultureInfo> supported)
    {
        if (supported.Count == 0)
        {
            return requested;
        }

        foreach (CultureInfo candidate in supported)
        {
            if (string.Equals(candidate.Name, requested.Name, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        // Walk up the requested culture so that an Italian region we do not ship still matches a
        // shipped it-IT, via the shared "it".
        for (CultureInfo? current = requested;
             current is not null && !string.IsNullOrEmpty(current.Name);
             current = current.Parent)
        {
            foreach (CultureInfo candidate in supported)
            {
                if (string.Equals(candidate.Name, current.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            foreach (CultureInfo candidate in supported)
            {
                if (string.Equals(
                        candidate.TwoLetterISOLanguageName,
                        current.TwoLetterISOLanguageName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
