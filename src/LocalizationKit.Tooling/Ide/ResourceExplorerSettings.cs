
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LocalizationKit.Tooling;

/// <summary>
/// Maintains the culture columns in Visual Studio's Resource Explorer state,
/// <c>.vs/&lt;solution&gt;/v&lt;version&gt;/ResourceExplorer/settings.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// The file lives under <c>.vs/</c>, which is gitignored, so it never arrives with a clone and drifts
/// the moment a language is added. Only the culture entries in <c>ColumnOrder</c> and
/// <c>VisibleColumnKeys</c> are touched: the view state around them belongs to the IDE.
/// </para>
/// <para>
/// Stale culture columns are removed only when explicitly asked for. Dropping a developer's column
/// layout unasked is worse than leaving a dead column behind.
/// </para>
/// </remarks>
public static class ResourceExplorerSettings
{
    /// <summary>The column keys that are not cultures.</summary>
    public static readonly IReadOnlySet<string> FixedColumnKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "type",
        "neutral-value",
        "neutral-comment",
        "file-path",
        "file-name",
    };

    private const string ColumnOrderKey = "ColumnOrder";
    private const string VisibleColumnsKey = "VisibleColumnKeys";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        // Visual Studio writes plain text; the default encoder would escape accented characters.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Finds every Resource Explorer settings file under a solution directory.</summary>
    /// <param name="solutionDirectory">The directory holding the <c>.vs</c> folder.</param>
    /// <returns>
    /// The settings files found. The solution folder name and the Visual Studio version folder are
    /// both discovered rather than assumed, since a repository can accumulate folders from several
    /// versions.
    /// </returns>
    public static IReadOnlyList<string> Discover(string solutionDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionDirectory);

        string vsDirectory = Path.Combine(solutionDirectory, ".vs");
        if (!Directory.Exists(vsDirectory))
        {
            return [];
        }

        List<string> found = [];
        foreach (string solution in Directory.EnumerateDirectories(vsDirectory))
        {
            foreach (string version in Directory.EnumerateDirectories(solution))
            {
                string candidate = Path.Combine(version, "ResourceExplorer", "settings.json");
                if (File.Exists(candidate))
                {
                    found.Add(candidate);
                }
            }
        }

        found.Sort(StringComparer.OrdinalIgnoreCase);
        return found;
    }

    /// <summary>Plans the column changes needed to show a set of cultures.</summary>
    /// <param name="settingsPath">The settings file. It must already exist; Visual Studio creates it.</param>
    /// <param name="cultures">The cultures the project ships.</param>
    /// <param name="prune">Whether to remove culture columns that are no longer shipped.</param>
    /// <returns>The change to make, or <see langword="null"/> when the file already matches.</returns>
    /// <exception cref="JsonException">The file is not valid JSON.</exception>
    public static FileChange? Plan(string settingsPath, IReadOnlyList<string> cultures, bool prune = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        ArgumentNullException.ThrowIfNull(cultures);

        if (!File.Exists(settingsPath))
        {
            return null;
        }

        string original = File.ReadAllText(settingsPath);
        JsonNode root = JsonNode.Parse(original)
            ?? throw new JsonException($"'{settingsPath}' is empty.");

        if (root is not JsonObject settings)
        {
            throw new JsonException($"'{settingsPath}' does not contain a JSON object.");
        }

        List<string> added = [];
        List<string> removed = [];

        SyncArray(settings, ColumnOrderKey, cultures, prune, added, removed);
        SyncArray(settings, VisibleColumnsKey, cultures, prune, added, removed);

        if (added.Count == 0 && removed.Count == 0)
        {
            return null;
        }

        string description = string.Join(
            " and ",
            new[]
            {
                added.Count > 0 ? $"add {string.Join(", ", added.Distinct(StringComparer.OrdinalIgnoreCase))}" : null,
                removed.Count > 0 ? $"remove {string.Join(", ", removed.Distinct(StringComparer.OrdinalIgnoreCase))}" : null,
            }.Where(part => part is not null));

        return new FileChange(
            settingsPath,
            $"Resource Explorer columns: {description}",
            Serialize(settings),
            FileChangeKind.Update,
            // Visual Studio writes this one without a byte order mark.
            WriteByteOrderMark: false);
    }

    private static void SyncArray(
        JsonObject settings,
        string key,
        IReadOnlyList<string> cultures,
        bool prune,
        List<string> added,
        List<string> removed)
    {
        if (settings[key] is not JsonArray array)
        {
            return;
        }

        List<string> values = [.. array.Select(node => node?.GetValue<string>() ?? string.Empty)];
        int addedBefore = added.Count;
        int removedBefore = removed.Count;

        if (prune)
        {
            for (int i = values.Count - 1; i >= 0; i--)
            {
                string value = values[i];
                if (!FixedColumnKeys.Contains(value)
                    && !cultures.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    removed.Add(value);
                    values.RemoveAt(i);
                }
            }
        }

        foreach (string culture in cultures)
        {
            if (values.Contains(culture, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            values.Insert(InsertionIndex(values), culture);
            added.Add(culture);
        }

        if (added.Count == addedBefore && removed.Count == removedBefore)
        {
            return;
        }

        JsonArray replacement = [];
        foreach (string value in values)
        {
            // Add(JsonNode) rather than Add<T>(T): the generic overload can serialize any type, so it
            // is annotated as unsafe to trim, and this tool is meant to stay AOT-publishable.
            replacement.Add((JsonNode)JsonValue.Create(value));
        }

        settings[key] = replacement;
    }

    /// <summary>
    /// Puts a new culture next to the cultures already there, rather than after <c>file-name</c>,
    /// so the grid keeps its translation columns together.
    /// </summary>
    private static int InsertionIndex(List<string> values)
    {
        for (int i = values.Count - 1; i >= 0; i--)
        {
            if (!FixedColumnKeys.Contains(values[i]))
            {
                return i + 1;
            }
        }

        int afterType = values.FindIndex(v => string.Equals(v, "type", StringComparison.OrdinalIgnoreCase));
        if (afterType >= 0)
        {
            return afterType + 1;
        }

        return values.Count;
    }

    private static string Serialize(JsonObject settings)
    {
        // Two-space indentation matches Visual Studio; the line endings depend on the platform's
        // default, so they are normalized rather than assumed. No trailing newline, again as VS writes it.
        return settings.ToJsonString(WriteOptions).ReplaceLineEndings("\r\n");
    }
}
