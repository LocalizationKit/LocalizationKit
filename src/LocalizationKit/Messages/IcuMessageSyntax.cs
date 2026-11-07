using System.Globalization;

namespace LocalizationKit;

/// <summary>
/// Checks the structure of an ICU MessageFormat pattern, and lists the arguments it refers to.
/// </summary>
/// <remarks>
/// <para>
/// This deliberately lives in the core, with no dependency on any message engine. Validation runs at
/// build time and has to be precise and stable: the engine is swappable, the rules are not. It also
/// catches two things an engine will not, because an engine's job is to render something rather than
/// to complain:
/// </para>
/// <list type="bullet">
/// <item><description>
/// A misspelled plural category. <c>{n, plural, oen {…} other {…}}</c> renders perfectly happily by
/// falling through to <c>other</c>, so the mistake reaches users as a sentence that never uses its
/// singular form.
/// </description></item>
/// <item><description>
/// A missing <c>other</c> branch, which ICU requires of <c>plural</c>, <c>selectordinal</c> and
/// <c>select</c>.
/// </description></item>
/// </list>
/// <para>
/// It validates structure, not meaning: an argument type it does not recognize is left alone, since
/// engines accept types beyond the ICU core such as number skeletons, durations and lists.
/// </para>
/// </remarks>
public static class IcuMessageSyntax
{
    /// <summary>The plural categories CLDR defines. Not every language uses every one.</summary>
    public static readonly IReadOnlySet<string> PluralCategories = new HashSet<string>(StringComparer.Ordinal)
    {
        "zero",
        "one",
        "two",
        "few",
        "many",
        "other",
    };

    /// <summary>Checks that a pattern is well formed.</summary>
    /// <param name="message">The pattern to check.</param>
    /// <param name="error">Why the pattern is malformed, when this method returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when the pattern is well formed.</returns>
    public static bool TryValidate(string message, out string? error)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            new Walker(message, names: null).Walk();
            error = null;
            return true;
        }
        catch (FormatException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Lists the arguments a pattern refers to, at any depth, so that a caller can be told about one
    /// it forgot to supply.
    /// </summary>
    /// <param name="message">The pattern to inspect.</param>
    /// <returns>The argument names, in order of first appearance, without duplicates.</returns>
    /// <exception cref="FormatException">The pattern is malformed.</exception>
    public static IReadOnlyList<string> GetArgumentNames(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        List<string> names = [];
        new Walker(message, names).Walk();

        List<string> unique = [];
        foreach (string name in names)
        {
            if (!unique.Contains(name, StringComparer.Ordinal))
            {
                unique.Add(name);
            }
        }

        return unique;
    }

    /// <summary>
    /// A recursive-descent walk over the pattern. A branch body is itself a message, which is why a
    /// linear scan cannot do this job: in <c>{n, plural, one {item} other {items}}</c> the braces
    /// around <c>item</c> delimit a branch, and <c>item</c> is text, not an argument.
    /// </summary>
    private sealed class Walker(string text, List<string>? names)
    {
        private int _index;

        public void Walk()
        {
            WalkMessage(nested: false);

            if (_index < text.Length)
            {
                throw Error($"Unexpected '}}' at position {_index}. Quote it as '}}' if it is meant literally.");
            }
        }

        private void WalkMessage(bool nested)
        {
            while (_index < text.Length)
            {
                char current = text[_index];

                if (current == '\'')
                {
                    SkipQuoted();
                    continue;
                }

                if (current == '}')
                {
                    if (nested)
                    {
                        return;
                    }

                    // Left for Walk to report with its position.
                    return;
                }

                if (current == '{')
                {
                    WalkArgument();
                    continue;
                }

                _index++;
            }

            if (nested)
            {
                throw Error("A branch is not closed with '}'.");
            }
        }

        private void WalkArgument()
        {
            int start = _index;
            _index++;
            SkipWhitespace();

            string name = ReadUntilCommaOrBrace(start);
            if (name.Length == 0)
            {
                throw Error($"The argument at position {start} has no name.");
            }

            foreach (char character in name)
            {
                if (char.IsWhiteSpace(character))
                {
                    throw Error($"The argument name '{name}' at position {start} contains a space.");
                }
            }

            names?.Add(name);

            if (text[_index] == '}')
            {
                _index++;
                return;
            }

            _index++;
            SkipWhitespace();
            string type = ReadUntilCommaOrBrace(start);

            if (text[_index] == '}')
            {
                _index++;
                return;
            }

            _index++;

            if (type is "plural" or "selectordinal")
            {
                WalkBranches(name, type, exactMatchesAllowed: true);
            }
            else if (type == "select")
            {
                WalkBranches(name, type, exactMatchesAllowed: false);
            }
            else
            {
                // An unrecognized type carries a style this validator does not interpret.
                SkipStyle(start);
            }

            SkipWhitespace();
            if (_index >= text.Length || text[_index] != '}')
            {
                throw Error($"The argument starting at position {start} is not closed with '}}'.");
            }

            _index++;
        }

        private void WalkBranches(string name, string type, bool exactMatchesAllowed)
        {
            bool hasOther = false;
            SkipWhitespace();

            if (exactMatchesAllowed && StartsWith("offset:"))
            {
                _index += "offset:".Length;
                while (_index < text.Length && !char.IsWhiteSpace(text[_index]) && text[_index] != '{')
                {
                    _index++;
                }
            }

            while (true)
            {
                SkipWhitespace();

                if (_index >= text.Length)
                {
                    throw Error($"The {type} argument '{name}' is not closed with '}}'.");
                }

                if (text[_index] == '}')
                {
                    break;
                }

                int selectorStart = _index;
                while (_index < text.Length && !char.IsWhiteSpace(text[_index]) && text[_index] != '{')
                {
                    _index++;
                }

                string selector = text[selectorStart.._index];
                ValidateSelector(selector, name, type, exactMatchesAllowed, selectorStart);

                SkipWhitespace();
                if (_index >= text.Length || text[_index] != '{')
                {
                    throw Error(
                        $"'{selector}' in the {type} argument '{name}' is not followed by a branch in braces.");
                }

                _index++;
                WalkMessage(nested: true);

                if (_index >= text.Length || text[_index] != '}')
                {
                    throw Error($"The '{selector}' branch of '{name}' is not closed with '}}'.");
                }

                _index++;
                hasOther |= selector == "other";
            }

            if (!hasOther)
            {
                throw Error(
                    $"The {type} argument '{name}' has no 'other' branch. ICU requires one, because it is what "
                    + "every value that matches nothing else falls back to.");
            }
        }

        private void ValidateSelector(string selector, string name, string type, bool exactMatchesAllowed, int position)
        {
            if (selector.Length == 0)
            {
                throw Error($"The {type} argument '{name}' has an empty branch selector at position {position}.");
            }

            if (selector[0] == '=')
            {
                if (!exactMatchesAllowed)
                {
                    throw Error(
                        $"'{selector}' cannot be used in a select argument; exact matches belong to plural and selectordinal.");
                }

                string number = selector[1..];
                if (number.Length == 0
                    || !decimal.TryParse(number, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                {
                    throw Error($"'{selector}' in '{name}' is not an exact numeric match such as '=0'.");
                }

                return;
            }

            if (exactMatchesAllowed && !PluralCategories.Contains(selector))
            {
                throw Error(
                    $"'{selector}' is not a plural category in the {type} argument '{name}'. Use one of "
                    + $"{string.Join(", ", PluralCategories)}, or an exact match such as '=0'. A misspelled category "
                    + "is silently treated as 'other' at runtime, so it has to be caught here.");
            }
        }

        /// <summary>Skips a style that this validator does not interpret, such as a number skeleton.</summary>
        private void SkipStyle(int argumentStart)
        {
            int depth = 0;
            while (_index < text.Length)
            {
                char current = text[_index];

                if (current == '\'')
                {
                    SkipQuoted();
                    continue;
                }

                if (current == '{')
                {
                    depth++;
                }
                else if (current == '}')
                {
                    if (depth == 0)
                    {
                        return;
                    }

                    depth--;
                }

                _index++;
            }

            throw Error($"The argument starting at position {argumentStart} is not closed with '}}'.");
        }

        /// <summary>
        /// Applies the ICU apostrophe rule: <c>''</c> is a literal apostrophe, an apostrophe before
        /// <c>{</c>, <c>}</c>, <c>#</c> or <c>|</c> opens a quoted literal, and any other apostrophe
        /// is itself. This is what keeps <c>l'articolo</c> intact.
        /// </summary>
        private void SkipQuoted()
        {
            if (_index + 1 < text.Length && text[_index + 1] == '\'')
            {
                _index += 2;
                return;
            }

            if (_index + 1 < text.Length && text[_index + 1] is '{' or '}' or '#' or '|')
            {
                int end = text.IndexOf('\'', _index + 1);
                _index = end < 0 ? text.Length : end + 1;
                return;
            }

            _index++;
        }

        private string ReadUntilCommaOrBrace(int argumentStart)
        {
            int start = _index;
            while (_index < text.Length && text[_index] != ',' && text[_index] != '}')
            {
                _index++;
            }

            if (_index >= text.Length)
            {
                throw Error($"The argument starting at position {argumentStart} is not closed with '}}'.");
            }

            return text[start.._index].Trim();
        }

        private void SkipWhitespace()
        {
            while (_index < text.Length && char.IsWhiteSpace(text[_index]))
            {
                _index++;
            }
        }

        private bool StartsWith(string value)
            => string.CompareOrdinal(text, _index, value, 0, value.Length) == 0;

        private FormatException Error(string message)
            => new($"{message} Message: \"{text}\".");
    }
}
