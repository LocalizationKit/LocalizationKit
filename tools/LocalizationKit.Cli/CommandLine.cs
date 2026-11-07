namespace LocalizationKit.Cli;

/// <summary>
/// A small hand-rolled argument parser. A dependency would be larger than the thing it parses.
/// </summary>
internal sealed class CommandLine
{
    private readonly Dictionary<string, string?> _options = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _positional = [];

    private CommandLine()
    {
    }

    public string? Command { get; private init; }

    public IReadOnlyList<string> Positional => _positional;

    public static CommandLine Parse(string[] args)
    {
        CommandLine parsed = new()
        {
            Command = args.Length > 0 && !args[0].StartsWith('-') ? args[0] : null,
        };

        for (int i = parsed.Command is null ? 0 : 1; i < args.Length; i++)
        {
            string argument = args[i];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                parsed._positional.Add(argument);
                continue;
            }

            string name = argument[2..];
            int equals = name.IndexOf('=');
            if (equals >= 0)
            {
                parsed._options[name[..equals]] = name[(equals + 1)..];
                continue;
            }

            bool hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
            parsed._options[name] = hasValue ? args[++i] : null;
        }

        return parsed;
    }

    public bool HasFlag(string name) => _options.ContainsKey(name);

    public string? Value(string name) => _options.TryGetValue(name, out string? value) ? value : null;

    /// <summary>Returns the options that are not in the given set, so typos are reported, not ignored.</summary>
    public IReadOnlyList<string> UnknownOptions(params string[] known)
        => [.. _options.Keys.Where(key => !known.Contains(key, StringComparer.OrdinalIgnoreCase))];
}
