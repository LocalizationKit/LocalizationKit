using System.Globalization;

namespace LocalizationKit.Tests;

/// <summary>A provider backed by dictionaries, so engine tests do not need files.</summary>
internal sealed class InMemoryResourceProvider(string name = "memory") : ILocalizationResourceProvider
{
    private readonly Dictionary<string, Dictionary<string, string>> _cultures =
        new(StringComparer.OrdinalIgnoreCase);

    public string Name { get; } = name;

    public int LoadCount { get; private set; }

    public InMemoryResourceProvider With(string culture, params (string Key, string Value)[] entries)
    {
        Dictionary<string, string> values = _cultures.TryGetValue(culture, out Dictionary<string, string>? existing)
            ? existing
            : _cultures[culture] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach ((string key, string value) in entries)
        {
            values[key] = value;
        }

        return this;
    }

    public void Set(string culture, string key, string value) => With(culture, (key, value));

    public ValueTask<IReadOnlyDictionary<string, string>?> LoadAsync(
        CultureInfo culture,
        CancellationToken cancellationToken = default)
    {
        LoadCount++;
        return ValueTask.FromResult(
            _cultures.TryGetValue(culture.Name, out Dictionary<string, string>? values)
                ? (IReadOnlyDictionary<string, string>?)values
                : null);
    }
}
