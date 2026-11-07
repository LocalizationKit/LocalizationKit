using System.Text;

namespace LocalizationKit;

/// <summary>Rewrites a message so that the problems translation causes are visible before it happens.</summary>
/// <remarks>
/// <para>
/// Three faults show up only once a language other than the one the UI was built in is in use: text
/// that no longer fits, text that was never externalized, and text that was concatenated rather than
/// translated. All three are cheap to find and expensive to find late.
/// </para>
/// <para>
/// A pseudolocalized build answers all three at once. Every translatable string becomes longer, so a
/// label that cannot grow is clipped where anyone can see it; every string is accented, so one that
/// is still plain English is a string nobody externalized; and the markers at each end make a
/// sentence assembled from two resources obvious, because it shows up with markers in the middle.
/// </para>
/// <para>
/// What it must not touch is the structure. An argument's name, the keywords of a plural or a select,
/// a category such as <c>few</c> or <c>=0</c>, and the tags of inline link markup are all read by
/// something rather than by a person: accent them and the message stops working, which would make
/// this a worse test than no test. The text inside a plural's branches, on the other hand, is exactly
/// what has to be rewritten — the plural branches are where the long words live. So this walks the
/// message and rewrites only what a reader reads.
/// </para>
/// </remarks>
public static class Pseudolocalizer
{
    /// <summary>What the rewritten message is wrapped in, so an un-rewritten one stands out.</summary>
    private const char OpenMarker = '⟦';
    private const char CloseMarker = '⟧';

    /// <summary>
    /// How much longer the text is made, as a fraction. German and Finnish commonly run a third
    /// longer than English for short strings, which is where the clipping happens.
    /// </summary>
    private const double Padding = 0.4;

    private const string PaddingCharacters = "∙∙∙∙∙∙∙∙";

    /// <summary>Rewrites the readable parts of a message, leaving everything a machine reads alone.</summary>
    /// <param name="message">The message to rewrite.</param>
    /// <returns>The rewritten message, which is still a valid message with the same arguments.</returns>
    public static string Transform(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.Length == 0)
        {
            return message;
        }

        StringBuilder result = new(message.Length + 8);
        _ = result.Append(OpenMarker);

        int letters = Rewrite(message, result);

        // Padding goes at the end, where it lengthens the string without disturbing an argument that
        // a layout is measured around.
        int pad = (int)Math.Round(letters * Padding);
        for (int i = 0; i < pad; i++)
        {
            _ = result.Append(PaddingCharacters[i % PaddingCharacters.Length]);
        }

        return result.Append(CloseMarker).ToString();
    }

    /// <summary>Walks the message, rewriting text and copying structure.</summary>
    /// <param name="message">The message being read.</param>
    /// <param name="result">The message being written.</param>
    /// <returns>How many letters were rewritten, which decides how much padding to add.</returns>
    private static int Rewrite(string message, StringBuilder result)
    {
        int letters = 0;
        int index = 0;

        while (index < message.Length)
        {
            char current = message[index];

            switch (current)
            {
                // An apostrophe quotes the next run in ICU, and what it quotes is literal text that
                // happens to contain a brace. Copying the quoted run verbatim keeps the quoting
                // intact; rewriting inside it risks changing what is being escaped.
                case '\'':
                    index = CopyQuoted(message, index, result);
                    break;

                case '{':
                    index = CopyArgument(message, index, result, ref letters);
                    break;

                // Inline link markup: <a id="help">text</a>. The tag is structure, the text between
                // tags is not, so only the tags are copied verbatim.
                case '<':
                    index = CopyTag(message, index, result);
                    break;

                default:
                    letters += Append(current, result);
                    index++;
                    break;
            }
        }

        return letters;
    }

    private static int CopyQuoted(string message, int index, StringBuilder result)
    {
        _ = result.Append(message[index]);
        index++;

        if (index < message.Length && message[index] == '\'')
        {
            // A doubled apostrophe is a literal one, and quotes nothing.
            _ = result.Append(message[index]);
            return index + 1;
        }

        while (index < message.Length)
        {
            char current = message[index];
            _ = result.Append(current);
            index++;

            if (current == '\'')
            {
                break;
            }
        }

        return index;
    }

    private static int CopyTag(string message, int index, StringBuilder result)
    {
        while (index < message.Length)
        {
            char current = message[index];
            _ = result.Append(current);
            index++;

            if (current == '>')
            {
                break;
            }
        }

        return index;
    }

    /// <summary>
    /// Copies an argument: its name and type verbatim, and the bodies of its branches rewritten.
    /// </summary>
    /// <param name="message">The message being read.</param>
    /// <param name="index">The index of the opening brace.</param>
    /// <param name="result">The message being written.</param>
    /// <param name="letters">The running count of rewritten letters.</param>
    /// <returns>The index just past the argument.</returns>
    private static int CopyArgument(string message, int index, StringBuilder result, ref int letters)
    {
        // The head is everything up to the first nested body or the closing brace: the argument's
        // name, its type, and any offset. All of it is read by the formatter.
        _ = result.Append(message[index]);
        index++;

        while (index < message.Length && message[index] != '{' && message[index] != '}')
        {
            _ = result.Append(message[index]);
            index++;
        }

        // Then alternating branch bodies and the category keywords between them.
        while (index < message.Length && message[index] != '}')
        {
            if (message[index] == '{')
            {
                index = CopyBranch(message, index, result, ref letters);
                continue;
            }

            // A category — one, few, other, =0 — or the whitespace around it.
            _ = result.Append(message[index]);
            index++;
        }

        if (index < message.Length)
        {
            _ = result.Append(message[index]);
            index++;
        }

        return index;
    }

    private static int CopyBranch(string message, int index, StringBuilder result, ref int letters)
    {
        _ = result.Append(message[index]);
        index++;

        int depth = 1;

        while (index < message.Length && depth > 0)
        {
            char current = message[index];

            switch (current)
            {
                case '\'':
                    index = CopyQuoted(message, index, result);
                    continue;

                case '{':
                    // A nested argument inside this branch: back to the head-and-branches rule.
                    index = CopyArgument(message, index, result, ref letters);
                    continue;

                case '<':
                    index = CopyTag(message, index, result);
                    continue;

                case '}':
                    depth--;
                    _ = result.Append(current);
                    index++;
                    continue;

                default:
                    letters += Append(current, result);
                    index++;
                    continue;
            }
        }

        return index;
    }

    /// <summary>Appends one character, accented when it is a letter.</summary>
    /// <param name="character">The character to append.</param>
    /// <param name="result">The message being written.</param>
    /// <returns>1 when a letter was rewritten, 0 otherwise.</returns>
    private static int Append(char character, StringBuilder result)
    {
        char accented = Accent(character);
        _ = result.Append(accented);
        return accented == character && !char.IsLetter(character) ? 0 : 1;
    }

    /// <summary>
    /// The accented form of a letter, chosen to stay readable: the point is to notice that a string
    /// went through here, not to make it unintelligible while checking a layout.
    /// </summary>
    /// <param name="character">The letter to accent.</param>
    /// <returns>The accented letter, or the character unchanged.</returns>
    private static char Accent(char character)
    {
        return character switch
        {
            'a' => 'à', 'b' => 'б', 'c' => 'ç', 'd' => 'ď', 'e' => 'é',
            'f' => 'ƒ', 'g' => 'ğ', 'h' => 'ĥ', 'i' => 'ì', 'j' => 'ĵ',
            'k' => 'ķ', 'l' => 'ļ', 'm' => 'ḿ', 'n' => 'ñ', 'o' => 'ö',
            'p' => 'ƥ', 'q' => 'ǫ', 'r' => 'ŗ', 's' => 'š', 't' => 'ţ',
            'u' => 'û', 'v' => 'ṿ', 'w' => 'ŵ', 'x' => 'ẋ', 'y' => 'ý',
            'z' => 'ž',
            'A' => 'À', 'B' => 'ƃ', 'C' => 'Ç', 'D' => 'Ď', 'E' => 'É',
            'F' => 'Ƒ', 'G' => 'Ğ', 'H' => 'Ĥ', 'I' => 'Ì', 'J' => 'Ĵ',
            'K' => 'Ķ', 'L' => 'Ļ', 'M' => 'Ḿ', 'N' => 'Ñ', 'O' => 'Ö',
            'P' => 'Ƥ', 'Q' => 'Ǫ', 'R' => 'Ŗ', 'S' => 'Š', 'T' => 'Ţ',
            'U' => 'Û', 'V' => 'Ṿ', 'W' => 'Ŵ', 'X' => 'Ẋ', 'Y' => 'Ý',
            'Z' => 'Ž',
            _ => character,
        };
    }
}
