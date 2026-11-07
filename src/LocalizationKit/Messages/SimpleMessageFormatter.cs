using System.Globalization;
using System.Text;

namespace LocalizationKit;

/// <summary>
/// The Phase 1 message engine: ICU-compatible argument substitution and apostrophe quoting, and an
/// honest refusal for everything that needs CLDR plural rules.
/// </summary>
/// <remarks>
/// <para>
/// Supported: literal text, <c>{name}</c> substitution, <c>''</c> for a literal apostrophe, and
/// <c>'{'</c>-style quoting of braces. An apostrophe that is not followed by <c>{</c>, <c>}</c>,
/// <c>#</c> or <c>|</c> is a literal apostrophe, which is what keeps <c>l'articolo</c> intact.
/// </para>
/// <para>
/// Not supported: any argument with a type, such as <c>{count, plural, ...}</c>,
/// <c>{gender, select, ...}</c> or <c>{amount, number, ::currency/EUR}</c>. Those throw
/// <see cref="UnsupportedMessageConstructException"/> rather than returning a plausible but wrong
/// string; <c>LocalizationKit.Messages.Icu</c> is the engine that renders them.
/// <see cref="TryValidate"/> still checks them in full, so translations are validated the same way
/// whether or not that package is installed.
/// </para>
/// </remarks>
public sealed class SimpleMessageFormatter : IMessageFormatter
{
    /// <inheritdoc/>
    public string Format(string message, IReadOnlyDictionary<string, object?> arguments, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(culture);

        if (message.IndexOfAny(['{', '}', '\'']) < 0)
        {
            return message;
        }

        StringBuilder result = new(message.Length + 16);
        foreach (Segment segment in Tokenize(message))
        {
            if (segment.Kind == SegmentKind.Literal)
            {
                result.Append(segment.Text);
                continue;
            }

            if (segment.ArgumentType is { Length: > 0 } construct)
            {
                throw new UnsupportedMessageConstructException(construct, segment.Text, key: null);
            }

            if (!arguments.TryGetValue(segment.Text, out object? value))
            {
                throw new FormatException(
                    $"Message argument '{segment.Text}' was not supplied. Message: \"{message}\".");
            }

            result.Append(FormatValue(value, culture));
        }

        return result.ToString();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Validation is stricter than this formatter's own capabilities on purpose: it checks the full
    /// ICU structure through <see cref="IcuMessageSyntax"/>, so a translation written for the ICU
    /// engine is checked properly whether or not that engine is installed.
    /// </remarks>
    public bool TryValidate(string message, out string? error)
        => IcuMessageSyntax.TryValidate(message, out error);

    private static string FormatValue(object? value, CultureInfo culture) => value switch
    {
        null => string.Empty,
        string text => text,
        IFormattable formattable => formattable.ToString(null, culture),
        _ => value.ToString() ?? string.Empty,
    };

    private static List<Segment> Tokenize(string message)
    {
        List<Segment> segments = [];
        StringBuilder literal = new();
        int index = 0;

        while (index < message.Length)
        {
            char current = message[index];

            if (current == '\'')
            {
                index = ReadQuoted(message, index, literal);
                continue;
            }

            if (current == '}')
            {
                throw new FormatException(
                    $"Unexpected '}}' at position {index}. Quote it as '}}' if it is meant literally. Message: \"{message}\".");
            }

            if (current != '{')
            {
                literal.Append(current);
                index++;
                continue;
            }

            int close = FindClosingBrace(message, index);
            if (close < 0)
            {
                throw new FormatException(
                    $"Unbalanced '{{' at position {index}. Message: \"{message}\".");
            }

            if (literal.Length > 0)
            {
                segments.Add(new Segment(SegmentKind.Literal, literal.ToString(), null));
                literal.Clear();
            }

            segments.Add(ReadArgument(message, index + 1, close));
            index = close + 1;
        }

        if (literal.Length > 0)
        {
            segments.Add(new Segment(SegmentKind.Literal, literal.ToString(), null));
        }

        return segments;
    }

    /// <summary>
    /// Applies the ICU apostrophe rule: <c>''</c> is a literal apostrophe, an apostrophe before
    /// <c>{</c>, <c>}</c>, <c>#</c> or <c>|</c> opens a quoted literal, and any other apostrophe is
    /// itself.
    /// </summary>
    private static int ReadQuoted(string message, int index, StringBuilder literal)
    {
        if (index + 1 < message.Length && message[index + 1] == '\'')
        {
            literal.Append('\'');
            return index + 2;
        }

        if (index + 1 < message.Length && message[index + 1] is '{' or '}' or '#' or '|')
        {
            int end = message.IndexOf('\'', index + 1);
            if (end < 0)
            {
                // Unterminated quote: ICU treats the rest of the pattern as literal.
                literal.Append(message, index + 1, message.Length - index - 1);
                return message.Length;
            }

            literal.Append(message, index + 1, end - index - 1);
            return end + 1;
        }

        literal.Append('\'');
        return index + 1;
    }

    private static Segment ReadArgument(string message, int start, int close)
    {
        string body = message[start..close];
        int comma = body.IndexOf(',');

        string name = (comma < 0 ? body : body[..comma]).Trim();
        if (name.Length == 0)
        {
            throw new FormatException($"An argument has no name. Message: \"{message}\".");
        }

        if (comma < 0)
        {
            return new Segment(SegmentKind.Argument, name, null);
        }

        string rest = body[(comma + 1)..];
        int nextComma = rest.IndexOf(',');
        string construct = (nextComma < 0 ? rest : rest[..nextComma]).Trim();
        if (construct.Length == 0)
        {
            throw new FormatException(
                $"Argument '{name}' has a ',' but no type. Message: \"{message}\".");
        }

        return new Segment(SegmentKind.Argument, name, construct);
    }

    /// <summary>Finds the brace that closes the one at <paramref name="open"/>, honouring nesting and quoting.</summary>
    private static int FindClosingBrace(string message, int open)
    {
        int depth = 0;
        for (int i = open; i < message.Length; i++)
        {
            char current = message[i];

            if (current == '\'')
            {
                if (i + 1 < message.Length && message[i + 1] == '\'')
                {
                    i++;
                    continue;
                }

                if (i + 1 < message.Length && message[i + 1] is '{' or '}' or '#' or '|')
                {
                    int end = message.IndexOf('\'', i + 1);
                    i = end < 0 ? message.Length : end;
                }

                continue;
            }

            if (current == '{')
            {
                depth++;
            }
            else if (current == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private enum SegmentKind
    {
        Literal,
        Argument,
    }

    private readonly struct Segment(SegmentKind kind, string text, string? argumentType)
    {
        public SegmentKind Kind { get; } = kind;

        /// <summary>The literal text, or the argument name.</summary>
        public string Text { get; } = text;

        /// <summary>The ICU construct on the argument, such as <c>plural</c>, or <see langword="null"/> for a plain substitution.</summary>
        public string? ArgumentType { get; } = argumentType;
    }
}
