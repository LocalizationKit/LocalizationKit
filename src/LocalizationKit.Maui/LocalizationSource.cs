using System.ComponentModel;

namespace LocalizationKit.Maui;

/// <summary>The object every localized value in XAML is bound to.</summary>
/// <remarks>
/// <para>
/// The same shape as the WPF integration, for the same reason: a MAUI markup extension may return a
/// binding, so a localized value is a binding to this object's indexer and a culture change is one
/// notification that re-reads all of them. The framework keeps the list of bindings, so this library
/// does not have to.
/// </para>
/// <para>
/// The notification names <c>Item[]</c>, which is the convention MAUI's binding engine reads as
/// "every indexed value may have changed", and then the empty string, which is the general
/// <see cref="INotifyPropertyChanged"/> convention for the same thing. Raising both costs nothing and
/// means this does not depend on which of the two a given version honours.
/// </para>
/// </remarks>
public sealed class LocalizationSource : INotifyPropertyChanged, IDisposable
{
    private const string Indexer = "Item[]";

    private readonly ILocalizer _localizer;
    private readonly ILocalizationContext _context;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="LocalizationSource"/> class.</summary>
    /// <param name="localizer">The localizer to read through.</param>
    /// <param name="context">The culture context to follow.</param>
    public LocalizationSource(ILocalizer localizer, ILocalizationContext context)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(context);

        _localizer = localizer;
        _context = context;
        _context.CultureChanged += OnCultureChanged;
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the one XAML binds to, set by <see cref="LocalizationKitXaml.Initialize(IServiceProvider)"/>.</summary>
    public static LocalizationSource? Current { get; internal set; }

    /// <summary>Gets this object, as a property a binding can watch.</summary>
    /// <remarks>
    /// A binding whose path is <c>.</c> is never subscribed to its source's notifications by MAUI, so
    /// a self binding reads the right value once and then never changes. Pointing the path at a
    /// property instead gives the binding something to watch, and this is that property: it returns
    /// the source, and the converter takes the key from there.
    /// </remarks>
    public LocalizationSource Values => this;

    /// <summary>Gets a translation.</summary>
    /// <param name="key">The resource key.</param>
    /// <returns>The translation, or whatever the configured fallback produces.</returns>
    public string this[string key] => string.IsNullOrEmpty(key) ? string.Empty : _localizer.Get(key);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _context.CultureChanged -= OnCultureChanged;
    }

    private void OnCultureChanged(object? sender, CultureChangedEventArgs args)
    {
        // The culture can be switched from anywhere — a settings page, a background sync — and a
        // binding has to be told on the thread that owns the elements.
        IDispatcher? dispatcher = Application.Current?.Dispatcher;

        if (dispatcher is not null && !dispatcher.IsDispatchRequired)
        {
            Raise();
            return;
        }

        if (dispatcher is not null)
        {
            _ = dispatcher.Dispatch(Raise);
            return;
        }

        Raise();
    }

    private void Raise()
    {
        // Values is what {loc:Text} watches. The other two are for anyone binding the indexer
        // directly: Item[] is MAUI's convention for "every indexed value", and an empty name is the
        // general one for "everything".
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Values)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Indexer));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
