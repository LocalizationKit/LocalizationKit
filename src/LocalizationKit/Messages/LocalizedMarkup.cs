using System.Text;

namespace LocalizationKit;

/// <summary>What a piece of a translated string turned out to be.</summary>
public enum LocalizedMarkupKind
{
    /// <summary>Literal text.</summary>
    Text = 0,

    /// <summary>A line break, written as <c>\n</c> or as a real newline.</summary>
    LineBreak = 1,

    /// <summary>A link, written as <c>&lt;a&gt;…&lt;/a&gt;</c>.</summary>
    Link = 2,
}

/// <summary>One piece of a translated string.</summary>
/// <param name="Kind">What this piece is.</param>
/// <param name="Text">The literal or link text; empty for a line break.</param>
/// <param name="Id">The link's <c>id</c>, used to match it to an action. <see langword="null"/> when absent.</param>
/// <param name="Href">The link's <c>href</c>, for a link that just opens a URL. <see langword="null"/> when absent.</param>
public sealed record LocalizedMarkupSegment(
    LocalizedMarkupKind Kind,
    string Text,
    string? Id = null,
    string? Href = null);

/// <summary>
/// Parses the small amount of markup a translated sentence is allowed to contain: links and line
/// breaks.
/// </summary>
/// <remarks>
/// <para>
/// This exists so that a sentence containing a link stays one resource. Splitting
/// <c>"Don't have an account? "</c>, <c>"Sign up"</c> and <c>"."</c> into three resources gives the
/// translator three disconnected fragments and no way to move the link, which every language with a
/// different word order needs to do.
/// </para>
/// <para>
/// A link is identified by its <c>id</c>, never by its position, so that reordering links during
/// translation cannot silently point them at the wrong action:
/// </para>
/// <code>
/// By signing up you agree to the &lt;a id="terms"&gt;Terms&lt;/a&gt; and the &lt;a href="https://example.com/privacy"&gt;Privacy Policy&lt;/a&gt;.
/// </code>
/// <para>
/// Only <c>&lt;a&gt;</c> and <c>&lt;/a&gt;</c> are markup. Any other angle bracket is literal text,
/// so <c>"a &lt; b"</c> parses as it reads. The format is deliberately not HTML: it carries exactly
/// what a sentence needs, and anything else is a parse error rather than a silent mystery.
/// </para>
/// </remarks>
public static class LocalizedMarkup
{
    private const string LinkOpenStart = "<a";
    private const string LinkClose = "</a>";

    /// <summary>Gets a value indicating whether a string contains anything this parser treats as markup.</summary>
    /// <param name="value">The translated string.</param>
    /// <returns><see langword="true"/> when the string has a link or a line break in it.</returns>
    public static bool ContainsMarkup(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.Contains(LinkOpenStart, StringComparison.OrdinalIgnoreCase)
            || value.Contains(LinkClose, StringComparison.OrdinalIgnoreCase)
            || value.Contains("\\n", StringComparison.Ordinal)
            || value.Contains('\n');
    }

    /// <summary>Splits a translated string into its pieces.</summary>
    /// <param name="value">The translated string.</param>
    /// <returns>The pieces, in order. A string with no markup yields a single text piece.</returns>
    /// <exception cref="FormatException">The markup is malformed.</exception>
    public static IReadOnlyList<LocalizedMarkupSegment> Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        List<LocalizedMarkupSegment> segments = [];
        StringBuilder text = new();
        int index = 0;
        bool insideLink = false;
        string? linkId = null;
        string? linkHref = null;

        void FlushText(List<LocalizedMarkupSegment> into)
        {
            if (text.Length > 0)
            {
                into.Add(new LocalizedMarkupSegment(LocalizedMarkupKind.Text, text.ToString()));
                text.Clear();
            }
        }

        while (index < value.Length)
        {
            char current = value[index];

            // A line break, written either as the two characters \n or as a real newline.
            if (current == '\\' && index + 1 < value.Length && value[index + 1] == 'n')
            {
                AddLineBreak();
                index += 2;
                continue;
            }

            if (current is '\r' or '\n')
            {
                AddLineBreak();
                index += current == '\r' && index + 1 < value.Length && value[index + 1] == '\n' ? 2 : 1;
                continue;
            }

            if (current == '<' && StartsWith(value, index, LinkClose))
            {
                if (!insideLink)
                {
                    throw new FormatException(
                        $"'</a>' at position {index} closes a link that was never opened. Value: \"{value}\".");
                }

                segments.Add(new LocalizedMarkupSegment(
                    LocalizedMarkupKind.Link,
                    text.ToString(),
                    linkId,
                    linkHref));

                text.Clear();
                insideLink = false;
                linkId = null;
                linkHref = null;
                index += LinkClose.Length;
                continue;
            }

            if (current == '<' && IsLinkOpen(value, index, out int openEnd))
            {
                if (insideLink)
                {
                    throw new FormatException(
                        $"A link is opened at position {index} while another is still open; links cannot nest. Value: \"{value}\".");
                }

                (linkId, linkHref) = ParseAttributes(value, index + LinkOpenStart.Length, openEnd, value);
                FlushText(segments);
                insideLink = true;
                index = openEnd + 1;
                continue;
            }

            text.Append(current);
            index++;
        }

        if (insideLink)
        {
            throw new FormatException($"A link is never closed with '</a>'. Value: \"{value}\".");
        }

        FlushText(segments);
        return segments;

        void AddLineBreak()
        {
            FlushText(segments);
            segments.Add(new LocalizedMarkupSegment(LocalizedMarkupKind.LineBreak, string.Empty));
        }
    }

    /// <summary>Splits a translated string without throwing.</summary>
    /// <param name="value">The translated string.</param>
    /// <param name="segments">The pieces, or an empty list when the markup is malformed.</param>
    /// <param name="error">Why the markup is malformed, when this method returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when the markup is well formed.</returns>
    public static bool TryParse(
        string value,
        out IReadOnlyList<LocalizedMarkupSegment> segments,
        out string? error)
    {
        try
        {
            segments = Parse(value);
            error = null;
            return true;
        }
        catch (FormatException ex)
        {
            segments = [];
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Lists the link ids in a translated string, in order, so that every culture's set of ids can be
    /// compared against the culture the keys were authored in.
    /// </summary>
    /// <param name="value">The translated string.</param>
    /// <returns>The ids of links that have one. Links identified only by <c>href</c> are not listed.</returns>
    /// <exception cref="FormatException">The markup is malformed.</exception>
    public static IReadOnlyList<string> GetLinkIds(string value)
        => [.. Parse(value)
            .Where(segment => segment.Kind == LocalizedMarkupKind.Link && segment.Id is { Length: > 0 })
            .Select(segment => segment.Id!)];

    private static bool StartsWith(string value, int index, string text)
        => string.CompareOrdinal(value, index, text, 0, text.Length) == 0;

    /// <summary>
    /// Recognizes <c>&lt;a&gt;</c> and <c>&lt;a …&gt;</c>, and nothing else. <c>&lt;abbr&gt;</c> is
    /// literal text, not a broken link tag.
    /// </summary>
    private static bool IsLinkOpen(string value, int index, out int closingBracket)
    {
        closingBracket = -1;

        if (!StartsWith(value, index, LinkOpenStart))
        {
            return false;
        }

        int after = index + LinkOpenStart.Length;
        if (after < value.Length && value[after] is not ('>' or ' ' or '\t'))
        {
            return false;
        }

        closingBracket = value.IndexOf('>', after);
        return closingBracket >= 0;
    }

    private static (string? Id, string? Href) ParseAttributes(string value, int start, int end, string whole)
    {
        string? id = null;
        string? href = null;
        int index = start;

        while (index < end)
        {
            if (char.IsWhiteSpace(value[index]))
            {
                index++;
                continue;
            }

            int nameEnd = value.IndexOf('=', index);
            if (nameEnd < 0 || nameEnd > end)
            {
                throw new FormatException(
                    $"A link attribute at position {index} has no value. Only id and href are supported. Value: \"{whole}\".");
            }

            string name = value[index..nameEnd].Trim();
            int quoteIndex = nameEnd + 1;
            if (quoteIndex >= end || value[quoteIndex] is not ('"' or '\''))
            {
                throw new FormatException(
                    $"The value of link attribute '{name}' must be quoted. Value: \"{whole}\".");
            }

            char quote = value[quoteIndex];
            int valueEnd = value.IndexOf(quote, quoteIndex + 1);
            if (valueEnd < 0 || valueEnd > end)
            {
                throw new FormatException(
                    $"The value of link attribute '{name}' is not closed. Value: \"{whole}\".");
            }

            string attributeValue = value[(quoteIndex + 1)..valueEnd];

            if (name.Equals("id", StringComparison.OrdinalIgnoreCase))
            {
                id = attributeValue;
            }
            else if (name.Equals("href", StringComparison.OrdinalIgnoreCase))
            {
                href = attributeValue;
            }
            else
            {
                throw new FormatException(
                    $"Unknown link attribute '{name}'. A link takes id and href. Value: \"{whole}\".");
            }

            index = valueEnd + 1;
        }

        return (id, href);
    }
}
