using System.Text.Json;

namespace LocalizationKit;

/// <summary>Reads a JSON translation file into flat key/value pairs.</summary>
/// <remarks>
/// <para>
/// Both shapes are accepted, because both are written in practice:
/// </para>
/// <code>
/// { "Common.Save": "Salva" }
/// { "Common": { "Save": "Salva" } }
/// </code>
/// <para>
/// A nested object is flattened by joining its path with dots, so the two files above produce the
/// same key. That keeps keys comparable across formats, which matters when a project has both JSON
/// and <c>.resw</c>, and it is what the validator and the tooling expect.
/// </para>
/// <para>
/// A value that is not a string is rejected rather than coerced: a number or a boolean in a
/// translation file is a mistake worth hearing about, and <c>null</c> would be an empty translation
/// masquerading as a real one.
/// </para>
/// </remarks>
public static class JsonResourceFile
{
    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Reads a JSON translation file.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The entries, keyed case-insensitively.</returns>
    /// <exception cref="JsonException">The file is not valid JSON, or holds a value that is not a string.</exception>
    public static IReadOnlyDictionary<string, string> Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using FileStream stream = File.OpenRead(path);
        return Read(stream);
    }

    /// <summary>Reads a JSON translation document.</summary>
    /// <param name="stream">The stream to read.</param>
    /// <returns>The entries, keyed case-insensitively.</returns>
    /// <exception cref="JsonException">The document is not valid JSON, or holds a value that is not a string.</exception>
    public static IReadOnlyDictionary<string, string> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using JsonDocument document = JsonDocument.Parse(stream, ReadOptions);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException(
                $"A translation file must be a JSON object, not {document.RootElement.ValueKind}.");
        }

        Dictionary<string, string> entries = new(StringComparer.OrdinalIgnoreCase);
        Flatten(document.RootElement, prefix: null, entries);
        return entries;
    }

    private static void Flatten(JsonElement element, string? prefix, Dictionary<string, string> entries)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            string key = prefix is null ? property.Name : $"{prefix}.{property.Name}";

            switch (property.Value.ValueKind)
            {
                case JsonValueKind.String:
                    entries[key] = property.Value.GetString()!;
                    break;

                case JsonValueKind.Object:
                    Flatten(property.Value, key, entries);
                    break;

                default:
                    throw new JsonException(
                        $"The value of '{key}' is {property.Value.ValueKind}; a translation must be a string "
                        + "or a nested object of strings.");
            }
        }
    }
}
