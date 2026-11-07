namespace LocalizationKit;

/// <summary>Configuration for per-request culture negotiation.</summary>
public sealed class RequestCultureOptions
{
    /// <summary>
    /// Gets the sources asked for a culture, in order. The first candidate that matches a supported
    /// culture wins.
    /// </summary>
    /// <remarks>
    /// The default order is query string, route value, cookie, then <c>Accept-Language</c>: something
    /// the user just asked for beats something they asked for earlier, which beats what their browser
    /// guessed. Clear the list and add your own to change it.
    /// </remarks>
    public IList<IRequestCultureSource> Sources { get; } =
    [
        new QueryStringCultureSource(),
        new RouteValueCultureSource(),
        new CookieCultureSource(),
        new AcceptLanguageCultureSource(),
    ];

    /// <summary>
    /// Gets or sets the culture used when no source produced a supported one, or
    /// <see langword="null"/> to leave the application's selected culture in place.
    /// </summary>
    public string? FallbackCulture { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether <c>Vary: Accept-Language</c> is added to responses
    /// whose culture came from that header. Defaults to <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Without it a shared cache can serve an Italian response to the next English reader, because
    /// nothing in the response says the language was negotiated. It is only added when the header
    /// actually decided the culture: a response whose language came from the route or a cookie does
    /// not vary by it.
    /// </remarks>
    public bool VaryByAcceptLanguage { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the middleware initializes LocalizationKit and loads
    /// every supported culture when the first request arrives. Defaults to <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// A culture scope cannot load resources itself — it is synchronous, by necessity — so they have
    /// to be in memory before a request can be served in them. Doing it on the first request rather
    /// than in a hosted service keeps it independent of startup ordering, and a failure is retried on
    /// the next request instead of poisoning the pipeline. Turn it off if startup already calls
    /// <see cref="ILocalizationContext.PreloadAsync"/>.
    /// </remarks>
    public bool PrepareOnFirstRequest { get; set; } = true;
}
