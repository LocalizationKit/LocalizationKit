using System.ComponentModel;
using System.Windows.Threading;

namespace LocalizationKit.Wpf;

/// <summary>The object every localized value in XAML is bound to.</summary>
/// <remarks>
/// <para>
/// This is the whole of how WPF keeps up with a language change, and it is far less machinery than
/// WinUI needs. A WPF markup extension may return a <see cref="System.Windows.Data.Binding"/>'s
/// value rather than a string, so <c>{loc:Text Key=Common.Save}</c> becomes a binding to this
/// object's indexer. When the culture changes, one <see cref="INotifyPropertyChanged"/> event with
/// the indexer's name re-reads every binding in the application.
/// </para>
/// <para>
/// WinUI's markup extension cannot do that — it has no binding to return — which is why it has to
/// record each target it provided a value to and write to them again later. Here the framework
/// already owns that list.
/// </para>
/// </remarks>
public sealed class LocalizationSource : INotifyPropertyChanged, IDisposable
{
    /// <summary>The property name that tells WPF every indexed value may have changed.</summary>
    private const string Indexer = "Item[]";

    private readonly ILocalizer _localizer;
    private readonly ILocalizationContext _context;
    private readonly Dispatcher? _dispatcher;
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
        _dispatcher = Dispatcher.FromThread(Thread.CurrentThread);

        _context.CultureChanged += OnCultureChanged;
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the one XAML binds to, set by <see cref="LocalizationKitXaml.Initialize(IServiceProvider)"/>.</summary>
    public static LocalizationSource? Current { get; internal set; }

    /// <summary>Gets a translation.</summary>
    /// <param name="key">The resource key.</param>
    /// <returns>The translation, or whatever the configured fallback produces.</returns>
    /// <remarks>
    /// An indexer rather than a method, because a binding path can be written as <c>[Common.Save]</c>
    /// and a method call cannot.
    /// </remarks>
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
        // The culture can be switched from anywhere; a binding has to be told on the thread that owns
        // the elements.
        if (_dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            _ = dispatcher.BeginInvoke(Raise);
            return;
        }

        Raise();
    }

    private void Raise()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Indexer));
    }
}
