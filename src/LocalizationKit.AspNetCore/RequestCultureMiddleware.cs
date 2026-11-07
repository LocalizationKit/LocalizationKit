using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace LocalizationKit;

/// <summary>Serves each request in the language it asked for.</summary>
/// <remarks>
/// <para>
/// The work this does is small, because the hard part is in the core: a culture scope overrides the
/// culture for one asynchronous flow, so one request being answered in Italian says nothing about the
/// others in flight. All that is left is to decide which language was asked for and to open the scope
/// around the rest of the pipeline.
/// </para>
/// <para>
/// The scope is opened in this method's own frame, before the <c>await</c>, which is the only place it
/// works: an <see cref="AsyncLocal{T}"/> written inside an <c>async</c> method flows down to what that
/// method awaits, never back up to its caller.
/// </para>
/// </remarks>
public sealed class RequestCultureMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILocalizationContext _context;
    private readonly RequestCultureOptions _options;
    private readonly object _gate = new();
    private Task? _prepared;

    /// <summary>Initializes a new instance of the <see cref="RequestCultureMiddleware"/> class.</summary>
    /// <param name="next">The rest of the pipeline.</param>
    /// <param name="context">The localization context whose culture is scoped.</param>
    /// <param name="options">Where a request may state its culture.</param>
    public RequestCultureMiddleware(
        RequestDelegate next,
        ILocalizationContext context,
        RequestCultureOptions options)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        _next = next;
        _context = context;
        _options = options;
    }

    /// <summary>Negotiates the culture and runs the rest of the pipeline inside it.</summary>
    /// <param name="httpContext">The request.</param>
    /// <returns>A task that completes when the request has been handled.</returns>
    public async Task InvokeAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (_options.PrepareOnFirstRequest)
        {
            await PrepareAsync().ConfigureAwait(false);
        }

        (CultureInfo? culture, string? source) = Negotiate(httpContext);

        if (culture is null)
        {
            await _next(httpContext).ConfigureAwait(false);
            return;
        }

        if (_options.VaryByAcceptLanguage && source == "accept-language")
        {
            httpContext.Response.Headers.Append(HeaderNames.Vary, HeaderNames.AcceptLanguage);
        }

        using IDisposable scope = _context.BeginCultureScope(culture);
        await _next(httpContext).ConfigureAwait(false);
    }

    private (CultureInfo? Culture, string? Source) Negotiate(HttpContext httpContext)
    {
        foreach (IRequestCultureSource source in _options.Sources)
        {
            foreach (string candidate in source.GetCandidates(httpContext))
            {
                if (_context.NegotiateCulture(candidate) is CultureInfo culture)
                {
                    return (culture, source.Name);
                }
            }
        }

        return _options.FallbackCulture is { Length: > 0 } fallback
            ? (_context.NegotiateCulture(fallback), "fallback")
            : (null, null);
    }

    private Task PrepareAsync()
    {
        Task prepared;

        lock (_gate)
        {
            prepared = _prepared ??= LoadEverythingAsync();
        }

        if (prepared.IsCompletedSuccessfully)
        {
            return prepared;
        }

        return AwaitAndForgetFailureAsync(prepared);
    }

    private async Task AwaitAndForgetFailureAsync(Task prepared)
    {
        try
        {
            await prepared.ConfigureAwait(false);
        }
        catch
        {
            // A provider that was briefly unavailable should not leave every later request failing on
            // a cached exception, so the next one tries again.
            lock (_gate)
            {
                if (_prepared == prepared)
                {
                    _prepared = null;
                }
            }

            throw;
        }
    }

    private async Task LoadEverythingAsync()
    {
        // Deliberately not the request's cancellation token: one client disconnecting must not cancel
        // the loading every other request in flight is waiting on.
        if (!_context.IsInitialized)
        {
            await _context.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
        }

        await _context.PreloadAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
