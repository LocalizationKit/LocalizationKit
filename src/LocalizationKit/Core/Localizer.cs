using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace LocalizationKit;

/// <summary>The default <see cref="ILocalizer"/>: a lookup against the loaded resource sets, plus message formatting.</summary>
internal sealed class Localizer : ILocalizer, IDisposable
{
    private static readonly IReadOnlyDictionary<string, object?> NoArguments =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    private readonly ILocalizationContext _context;
    private readonly ResourceCatalog _catalog;
    private readonly IMessageFormatter _messageFormatter;
    private readonly LocalizationKitOptions _options;
    private readonly ConcurrentDictionary<string, LocalizedString> _observed = new(StringComparer.OrdinalIgnoreCase);

    public Localizer(
        ILocalizationContext context,
        ResourceCatalog catalog,
        IMessageFormatter messageFormatter,
        LocalizationKitOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(messageFormatter);
        ArgumentNullException.ThrowIfNull(options);

        _context = context;
        _catalog = catalog;
        _messageFormatter = messageFormatter;
        _options = options;
        _context.CultureChanged += OnCultureChanged;
    }

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        EnsureInitialized();

        CultureInfo uiCulture = _context.UICulture;
        if (_catalog.TryGet(key, uiCulture, out string? value))
        {
            return value;
        }

        _options.OnMissingKey?.Invoke(new MissingResourceInfo(key, uiCulture, _catalog.GetChain(uiCulture)));

        return _options.FallbackBehavior switch
        {
            FallbackBehavior.ReturnEmpty => string.Empty,
            FallbackBehavior.Throw => throw new MissingResourceException(key, uiCulture),
            _ => key,
        };
    }

    public bool TryGet(string key, [NotNullWhen(true)] out string? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        EnsureInitialized();

        return _catalog.TryGet(key, _context.UICulture, out value);
    }

    public IReadOnlyDictionary<string, string> GetAll(bool includeFallbacks = true)
    {
        EnsureInitialized();
        return _catalog.GetAll(_context.UICulture, includeFallbacks);
    }

    [RequiresUnreferencedCode(
        "Message arguments are read from the object's public properties by reflection, and trimming can remove the properties of the type passed in. Use the overload that takes an IReadOnlyDictionary<string, object?>.")]
    public string Format(string key, object? arguments = null)
        => Format(key, MessageArguments.From(arguments));

    public string Format(string key, IReadOnlyDictionary<string, object?> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        string message = Get(key);
        try
        {
            return _messageFormatter.Format(message, arguments, _context.Culture);
        }
        catch (UnsupportedMessageConstructException ex)
        {
            // Re-throw with the key, which the formatter has no way of knowing.
            throw new UnsupportedMessageConstructException(
                ex.Construct ?? "unknown",
                ex.ArgumentName ?? "unknown",
                key);
        }
    }

    public string Plural(string key, decimal count)
        => Format(key, new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["count"] = count });

    public ILocalizer Scoped(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        return new ScopedLocalizer(this, prefix);
    }

    public ILocalizedString Observe(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return _observed.GetOrAdd(key, static (k, self) => new LocalizedString(self, k), this);
    }

    public void Dispose() => _context.CultureChanged -= OnCultureChanged;

    private void EnsureInitialized()
    {
        if (!_catalog.IsInitialized)
        {
            throw new InvalidOperationException(
                "LocalizationKit has not been initialized. Await IServiceProvider.InitializeLocalizationKitAsync() "
                + "(or ILocalizationContext.InitializeAsync) during startup, before any resource is resolved.");
        }
    }

    private void OnCultureChanged(object? sender, CultureChangedEventArgs e)
    {
        foreach (LocalizedString observed in _observed.Values)
        {
            observed.Refresh();
        }
    }

    internal static IReadOnlyDictionary<string, object?> EmptyArguments => NoArguments;
}
