using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LocalizationKit.Generators;

/// <summary>Turns a resource name segment into something C# will accept.</summary>
/// <remarks>
/// Resource names are written by whoever writes the resources, so they can contain spaces, hyphens,
/// digits at the front and words the language has reserved. Generated code that does not compile is
/// worse than no generated code, so every segment goes through here.
/// </remarks>
internal static class Identifier
{
    private static readonly HashSet<string> Keywords = new HashSet<string>(System.StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class",
        "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event",
        "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if",
        "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new",
        "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
        "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static",
        "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong",
        "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
    };

    /// <summary>Converts one name segment into a C# identifier.</summary>
    /// <param name="segment">The segment, as written in the resource file.</param>
    /// <returns>An identifier the compiler accepts.</returns>
    public static string For(string segment)
    {
        StringBuilder builder = new StringBuilder(segment.Length);

        foreach (char character in segment)
        {
            builder.Append(IsIdentifierCharacter(character) ? character : '_');
        }

        string identifier = builder.ToString();

        if (identifier.Length == 0)
        {
            return "_";
        }

        // A digit cannot start an identifier, and neither can a combining mark.
        if (!IsIdentifierStart(identifier[0]))
        {
            identifier = "_" + identifier;
        }

        // @-escaping rather than renaming, so the generated name still reads like the key.
        return Keywords.Contains(identifier) ? "@" + identifier : identifier;
    }

    /// <summary>Makes a name that is not taken yet, and reserves it.</summary>
    /// <param name="used">The names already used at this level.</param>
    /// <param name="preferred">The name to use if it is free.</param>
    /// <returns>The name to emit.</returns>
    public static string Unique(HashSet<string> used, string preferred)
    {
        string candidate = preferred;

        for (int suffix = 2; !used.Add(candidate); suffix++)
        {
            candidate = preferred + suffix.ToString(CultureInfo.InvariantCulture);
        }

        return candidate;
    }

    private static bool IsIdentifierStart(char character)
    {
        if (character == '_')
        {
            return true;
        }

        UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);

        return category is UnicodeCategory.UppercaseLetter
            or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter
            or UnicodeCategory.OtherLetter
            or UnicodeCategory.LetterNumber;
    }

    private static bool IsIdentifierCharacter(char character)
    {
        if (IsIdentifierStart(character))
        {
            return true;
        }

        UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);

        return category is UnicodeCategory.DecimalDigitNumber
            or UnicodeCategory.ConnectorPunctuation
            or UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.Format;
    }
}
