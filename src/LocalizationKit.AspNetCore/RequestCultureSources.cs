using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace LocalizationKit;

/// <summary>Reads the culture from a query string value, such as <c>?culture=it-IT</c>.</summary>
public sealed class QueryStringCultureSource : IRequestCultureSource
{
    /// <summary>Initializes a new instance of the <see cref="QueryStringCultureSource"/> class.</summary>
    /// <param name="key">The query string key to read. Defaults to <c>culture</c>.</param>
    public QueryStringCultureSource(string key = "culture")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
    }

    /// <summary>Gets the query string key.</summary>
    public string Key { get; }

    /// <inheritdoc/>
    public string Name => "query";

    /// <inheritdoc/>
    public IReadOnlyList<string> GetCandidates(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Request.Query.TryGetValue(Key, out Microsoft.Extensions.Primitives.StringValues values)
            && values.Count > 0
            && values[0] is { Length: > 0 } value
            ? [value]
            : [];
    }
}

/// <summary>Reads the culture from a route value, for a path such as <c>/it-IT/orders</c>.</summary>
public sealed class RouteValueCultureSource : IRequestCultureSource
{
    /// <summary>Initializes a new instance of the <see cref="RouteValueCultureSource"/> class.</summary>
    /// <param name="key">The route value to read. Defaults to <c>culture</c>.</param>
    public RouteValueCultureSource(string key = "culture")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
    }

    /// <summary>Gets the route value key.</summary>
    public string Key { get; }

    /// <inheritdoc/>
    public string Name => "route";

    /// <inheritdoc/>
    public IReadOnlyList<string> GetCandidates(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Routing has to have run for this to see anything, which it has by the time the middleware
        // sits after UseRouting. Before that the collection is simply empty.
        return context.Request.RouteValues.TryGetValue(Key, out object? value)
            && value?.ToString() is { Length: > 0 } culture
            ? [culture]
            : [];
    }
}

/// <summary>Reads the culture from a cookie, which is how a choice sticks between requests.</summary>
public sealed class CookieCultureSource : IRequestCultureSource
{
    /// <summary>The cookie name used unless another is given.</summary>
    public const string DefaultCookieName = "localizationkit.culture";

    /// <summary>Initializes a new instance of the <see cref="CookieCultureSource"/> class.</summary>
    /// <param name="cookieName">The cookie to read. Defaults to <see cref="DefaultCookieName"/>.</param>
    public CookieCultureSource(string cookieName = DefaultCookieName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cookieName);
        CookieName = cookieName;
    }

    /// <summary>Gets the cookie name.</summary>
    public string CookieName { get; }

    /// <inheritdoc/>
    public string Name => "cookie";

    /// <inheritdoc/>
    public IReadOnlyList<string> GetCandidates(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Request.Cookies.TryGetValue(CookieName, out string? value) && value is { Length: > 0 }
            ? [value]
            : [];
    }

    /// <summary>Remembers a culture choice in the response, for a language switch endpoint to call.</summary>
    /// <param name="context">The request whose response carries the cookie.</param>
    /// <param name="cultureName">The culture to remember.</param>
    /// <param name="options">Cookie options, or <see langword="null"/> for a year-long site-wide cookie.</param>
    /// <remarks>
    /// The cookie holds a language preference, not a credential, so the default is deliberately not
    /// <c>HttpOnly</c>: client-side code may want to read it too. It is not marked
    /// <see cref="CookieOptions.Secure"/> either, because that would make the choice silently stop
    /// working over plain HTTP in development; pass options to change either.
    /// </remarks>
    public void Append(HttpContext context, string cultureName, CookieOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);

        context.Response.Cookies.Append(
            CookieName,
            cultureName,
            options ?? new CookieOptions
            {
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
            });
    }
}

/// <summary>Reads the cultures from the <c>Accept-Language</c> header, in the order the client ranked them.</summary>
public sealed class AcceptLanguageCultureSource : IRequestCultureSource
{
    private readonly int _maxCandidates;

    /// <summary>Initializes a new instance of the <see cref="AcceptLanguageCultureSource"/> class.</summary>
    /// <param name="maxCandidates">
    /// How many languages to consider. The cap exists because the header is attacker-controlled and a
    /// long list would otherwise mean a long negotiation on every request.
    /// </param>
    public AcceptLanguageCultureSource(int maxCandidates = 6)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCandidates, 1);
        _maxCandidates = maxCandidates;
    }

    /// <inheritdoc/>
    public string Name => "accept-language";

    /// <inheritdoc/>
    public IReadOnlyList<string> GetCandidates(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!StringWithQualityHeaderValue.TryParseList(context.Request.Headers.AcceptLanguage, out var languages)
            || languages is null
            || languages.Count == 0)
        {
            return [];
        }

        return
        [
            .. languages
                // q=0 means "not this one", and "*" says anything, which is the same as saying nothing.
                .Where(language => (language.Quality ?? 1d) > 0d
                    && !string.Equals(language.Value.Value, "*", StringComparison.Ordinal)
                    && language.Value.HasValue)
                .OrderByDescending(language => language.Quality ?? 1d)
                .Take(_maxCandidates)
                .Select(language => language.Value.Value!),
        ];
    }
}
