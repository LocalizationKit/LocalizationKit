using System.Globalization;

namespace LocalizationKit;

/// <summary>
/// The application-wide culture context: one selected culture for the whole process, which is what a
/// desktop or console application wants. A request-scoped implementation belongs in the ASP.NET Core
/// integration, not here.
/// </summary>
internal sealed class LocalizationContext : ILocalizationContext
{
    private readonly LocalizationKitOptions _options;
    private readonly ResourceCatalog _catalog;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CultureInfo _defaultCulture;
    private readonly CultureInfo[] _supportedCultures;

    private readonly AsyncLocal<CultureInfo?> _scopedCulture = new();

    private volatile State _state;

    public LocalizationContext(LocalizationKitOptions options, ResourceCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(catalog);

        _options = options;
        _catalog = catalog;
        _defaultCulture = CultureInfo.GetCultureInfo(options.DefaultCulture);
        _supportedCultures = [.. options.SupportedCultures.Select(CultureInfo.GetCultureInfo)];
        _state = new State(_defaultCulture, _defaultCulture, Initialized: false);
    }

    public CultureInfo Culture => _scopedCulture.Value ?? _state.Culture;

    public CultureInfo UICulture => _scopedCulture.Value ?? _state.UICulture;

    public IReadOnlyList<CultureInfo> SupportedCultures => _supportedCultures;

    public CultureInfo DefaultCulture => _defaultCulture;

    public bool IsInitialized => _state.Initialized;

    public event EventHandler<CultureChangedEventArgs>? CultureChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_state.Initialized)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_state.Initialized)
            {
                return;
            }

            CultureInfo requested = _options.InitialCulture is { Length: > 0 } configured
                ? CultureInfo.GetCultureInfo(configured)
                : CultureInfo.CurrentUICulture;

            CultureInfo selected = CultureNegotiator.Negotiate(requested, _supportedCultures) ?? _defaultCulture;

            await ApplyAsync(selected, CultureChangeReason.Initialized, cancellationToken).ConfigureAwait(false);

            if (_options.PreloadSupportedCultures)
            {
                await PreloadAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task SetCultureAsync(string cultureName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);
        return SetCultureAsync(CultureInfo.GetCultureInfo(cultureName), cancellationToken);
    }

    public async Task SetCultureAsync(CultureInfo culture, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(culture);

        CultureInfo selected = CultureNegotiator.Negotiate(culture, _supportedCultures)
            ?? throw new CultureNotSupportedException(culture.Name, _supportedCultures.Select(c => c.Name));

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            State current = _state;
            if (current.Initialized
                && string.Equals(current.UICulture.Name, selected.Name, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await ApplyAsync(selected, CultureChangeReason.CultureSelected, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task PreloadAsync(CancellationToken cancellationToken = default)
    {
        foreach (CultureInfo culture in _supportedCultures)
        {
            await _catalog.LoadAsync(culture, cancellationToken).ConfigureAwait(false);
        }

        // With no declared cultures there is nothing to enumerate; the selected one is already loaded.
        await _catalog.LoadAsync(_defaultCulture, cancellationToken).ConfigureAwait(false);
    }

    public IDisposable BeginCultureScope(string cultureName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);
        return BeginCultureScope(CultureInfo.GetCultureInfo(cultureName));
    }

    public IDisposable BeginCultureScope(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        CultureInfo selected = CultureNegotiator.Negotiate(culture, _supportedCultures)
            ?? throw new CultureNotSupportedException(culture.Name, _supportedCultures.Select(c => c.Name));

        if (!_catalog.IsLoaded(selected))
        {
            throw new InvalidOperationException(
                $"The resources for '{selected.Name}' are not loaded, so a scope cannot use them. A scope is "
                + "synchronous by design, so it cannot load them itself: await ILocalizationContext.PreloadAsync() "
                + "during startup, or set LocalizationKitOptions.PreloadSupportedCultures.");
        }

        return new CultureScope(this, selected);
    }

    public CultureInfo? NegotiateCulture(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return null;
        }

        CultureInfo requested;
        try
        {
            // predefinedOnly, because Windows invents a culture for any well-formed tag, and a tag
            // from a request header is not evidence that the culture exists.
            requested = CultureInfo.GetCultureInfo(cultureName, predefinedOnly: true);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }

        return CultureNegotiator.Negotiate(requested, _supportedCultures);
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _catalog.Clear();
            await ApplyAsync(_state.UICulture, CultureChangeReason.ResourcesReloaded, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Loads the resources first and only then publishes the new culture, so no observer can ever
    /// read a culture whose resources are still loading.
    /// </summary>
    private async Task ApplyAsync(CultureInfo selected, CultureChangeReason reason, CancellationToken cancellationToken)
    {
        await _catalog.LoadAsync(selected, cancellationToken).ConfigureAwait(false);

        State previous = _state;
        _state = new State(selected, selected, Initialized: true);

        if (_options.FlowToThreadCulture)
        {
            CultureInfo.DefaultThreadCurrentCulture = selected;
            CultureInfo.DefaultThreadCurrentUICulture = selected;
            CultureInfo.CurrentCulture = selected;
            CultureInfo.CurrentUICulture = selected;
        }

        CultureChanged?.Invoke(
            this,
            new CultureChangedEventArgs(
                previous.Initialized ? previous.Culture : null,
                selected,
                selected,
                reason));
    }

    private sealed record State(CultureInfo Culture, CultureInfo UICulture, bool Initialized);

    /// <summary>
    /// Restores the previous culture when disposed, so scopes nest. Written in the caller's frame,
    /// which is what makes the override visible to the rest of that flow.
    /// </summary>
    private sealed class CultureScope : IDisposable
    {
        private readonly LocalizationContext _context;
        private readonly CultureInfo? _previousScope;
        private readonly CultureInfo? _previousThreadCulture;
        private readonly CultureInfo? _previousThreadUICulture;
        private bool _disposed;

        public CultureScope(LocalizationContext context, CultureInfo culture)
        {
            _context = context;
            _previousScope = context._scopedCulture.Value;
            context._scopedCulture.Value = culture;

            if (context._options.FlowToThreadCulture)
            {
                // CurrentCulture flows with the execution context, so this reaches the awaited work
                // below this frame and nothing above it.
                _previousThreadCulture = CultureInfo.CurrentCulture;
                _previousThreadUICulture = CultureInfo.CurrentUICulture;
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _context._scopedCulture.Value = _previousScope;

            if (_previousThreadCulture is not null)
            {
                CultureInfo.CurrentCulture = _previousThreadCulture;
            }

            if (_previousThreadUICulture is not null)
            {
                CultureInfo.CurrentUICulture = _previousThreadUICulture;
            }
        }
    }
}
