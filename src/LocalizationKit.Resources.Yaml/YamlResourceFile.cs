using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace LocalizationKit;

/// <summary>Reads a YAML translation file into flat key/value pairs.</summary>
/// <remarks>
/// <para>
/// The same two shapes the JSON provider accepts, for the same reason — both are written in practice —
/// and flattened the same way, so a YAML key, a JSON key and a <c>.resw</c> name all land in one key
/// space:
/// </para>
/// <code>
/// Common.Save: Salva
///
/// Common:
///   Save: Salva
/// </code>
/// <para>
/// What YAML adds over JSON is worth the dependency for a translator: comments survive, a long message
/// can be folded over several lines, and quoting is optional. What it also adds is ambiguity, and the
/// rules here are deliberately narrow. A value must be a scalar; a sequence or a nested document is
/// rejected rather than guessed at. A scalar is taken as written, not as YAML would type it, so a key
/// whose translation is <c>No</c> or <c>2026-01-01</c> stays that text instead of becoming a boolean
/// or a date — which is the trap that makes YAML configuration files surprising, and a translation
/// file is never anything but strings.
/// </para>
/// </remarks>
public static class YamlResourceFile
{
    /// <summary>Reads a YAML translation file.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The entries, keyed case-insensitively.</returns>
    /// <exception cref="YamlException">The file is not valid YAML, or holds a value that is not a scalar.</exception>
    public static IReadOnlyDictionary<string, string> Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using StreamReader reader = new(path);
        return Read(reader);
    }

    /// <summary>Reads a YAML translation document.</summary>
    /// <param name="reader">The text to read.</param>
    /// <returns>The entries, keyed case-insensitively.</returns>
    /// <exception cref="YamlException">The document is not valid YAML, or holds a value that is not a scalar.</exception>
    public static IReadOnlyDictionary<string, string> Read(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        YamlStream stream = [];
        stream.Load(reader);

        Dictionary<string, string> entries = new(StringComparer.OrdinalIgnoreCase);

        if (stream.Documents.Count == 0)
        {
            // An empty file is an empty translation set, not an error: a culture can be scaffolded
            // before anyone has written anything in it.
            return entries;
        }

        if (stream.Documents.Count > 1)
        {
            throw new YamlException(
                stream.Documents[1].RootNode.Start,
                stream.Documents[1].RootNode.End,
                "A translation file must hold one YAML document.");
        }

        YamlNode root = stream.Documents[0].RootNode;

        if (root is not YamlMappingNode mapping)
        {
            throw new YamlException(
                root.Start,
                root.End,
                $"A translation file must be a mapping of keys to text, not {Describe(root)}.");
        }

        Flatten(mapping, prefix: null, entries);
        return entries;
    }

    private static void Flatten(YamlMappingNode mapping, string? prefix, Dictionary<string, string> entries)
    {
        foreach (KeyValuePair<YamlNode, YamlNode> entry in mapping.Children)
        {
            if (entry.Key is not YamlScalarNode { Value: { Length: > 0 } name })
            {
                throw new YamlException(
                    entry.Key.Start,
                    entry.Key.End,
                    "A resource name must be text.");
            }

            string key = prefix is null ? name : $"{prefix}.{name}";

            switch (entry.Value)
            {
                case YamlScalarNode { Value: not null } scalar:
                    entries[key] = scalar.Value;
                    break;

                case YamlMappingNode nested:
                    Flatten(nested, key, entries);
                    break;

                default:
                    throw new YamlException(
                        entry.Value.Start,
                        entry.Value.End,
                        $"The value of '{key}' is {Describe(entry.Value)}; a translation must be text or a "
                        + "nested mapping of text.");
            }
        }
    }

    private static string Describe(YamlNode node)
    {
        return node switch
        {
            YamlSequenceNode => "a sequence",
            YamlMappingNode => "a mapping",
            YamlScalarNode { Value: null } => "empty",
            YamlScalarNode => "text",
            _ => "unsupported",
        };
    }
}
