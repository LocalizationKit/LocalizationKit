using System.Globalization;

namespace LocalizationKit;

/// <summary>
/// The selected application culture, kept deliberately separate from
/// <see cref="CultureInfo.CurrentCulture"/> and <see cref="CultureInfo.CurrentUICulture"/>.
/// </summary>
/// <remarks>
/// <para>
/// The default implementation is application-wide, which is what a desktop application wants. A
/// server application wants the culture scoped to a request instead, so the context is an interface
/// and the hosting integration decides how it is scoped. Nothing in the core resolves resources
/// through ambient static state.
/// </para>
/// </remarks>
public interface ILocalizationContext
{
    /// <summary>Gets the culture used for formatting numbers, dates and currency.</summary>
    CultureInfo Culture { get; }

    /// <summary>Gets the culture used for resource lookup.</summary>
    CultureInfo UICulture { get; }

    /// <summary>
    /// Gets the cultures the application declares support for, or an empty list when
    /// <see cref="LocalizationKitOptions.SupportedCultures"/> was left unset and any culture is accepted.
    /// </summary>
    IReadOnlyList<CultureInfo> SupportedCultures { get; }

    /// <summary>
    /// Gets the culture every fallback chain ends at. Useful as the answer to "what should I select
    /// when the culture I wanted is not available".
    /// </summary>
    CultureInfo DefaultCulture { get; }

    /// <summary>Gets a value indicating whether <see cref="InitializeAsync"/> has completed.</summary>
    bool IsInitialized { get; }

    /// <summary>Raised after a culture change has been applied and its resources are loaded.</summary>
    event EventHandler<CultureChangedEventArgs>? CultureChanged;

    /// <summary>
    /// Selects the initial culture and loads its resources, along with those of every culture in its
    /// fallback chain. Safe to call more than once; subsequent calls do nothing.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes once resources are available for synchronous lookup.</returns>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Switches the selected culture, loading its resources before the change is observable.</summary>
    /// <param name="cultureName">An IETF language tag, such as <c>it-IT</c>.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes once the change has been applied and <see cref="CultureChanged"/> has been raised.</returns>
    /// <exception cref="CultureNotSupportedException">
    /// The culture is not in <see cref="SupportedCultures"/> and that list is not empty.
    /// </exception>
    Task SetCultureAsync(string cultureName, CancellationToken cancellationToken = default);

    /// <summary>Switches the selected culture, loading its resources before the change is observable.</summary>
    /// <param name="culture">The culture to select.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes once the change has been applied and <see cref="CultureChanged"/> has been raised.</returns>
    Task SetCultureAsync(CultureInfo culture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discards every cached resource set and reloads the current culture, then raises
    /// <see cref="CultureChanged"/> so observers re-read their values.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes once resources have been reloaded.</returns>
    Task ReloadAsync(CancellationToken cancellationToken = default);

    /// <summary>Loads the resources of every supported culture, so that any of them can be scoped to later.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes once every supported culture is available.</returns>
    /// <remarks>
    /// A server wants this during startup: the set of cultures is finite and small, and loading them
    /// once means a request never waits on disk. Set
    /// <see cref="LocalizationKitOptions.PreloadSupportedCultures"/> to have initialization do it.
    /// </remarks>
    Task PreloadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Overrides the culture for the current asynchronous flow until the returned scope is disposed.
    /// </summary>
    /// <param name="culture">The culture this flow should use.</param>
    /// <returns>A scope that restores the previous culture when disposed.</returns>
    /// <remarks>
    /// <para>
    /// This is how a server serves two users in two languages at once, and how a background job or a
    /// console tool renders one message in a different language without disturbing anything else. The
    /// override follows the async flow rather than the thread, and nests.
    /// </para>
    /// <para>
    /// It is deliberately synchronous: an <see cref="System.Threading.AsyncLocal{T}"/> written inside
    /// an <c>async</c> method does not escape back to its caller, so a scope that loaded resources
    /// before pushing would silently fail to apply. Resources must already be loaded — see
    /// <see cref="PreloadAsync"/> — and the push happens in the caller's own frame.
    /// </para>
    /// <para>
    /// <see cref="CultureChanged"/> is not raised: a scope belongs to one flow, while that event is
    /// for application-wide changes that observable strings react to.
    /// </para>
    /// </remarks>
    /// <exception cref="CultureNotSupportedException">The culture is not supported.</exception>
    /// <exception cref="InvalidOperationException">The culture's resources have not been loaded.</exception>
    IDisposable BeginCultureScope(CultureInfo culture);

    /// <summary>Overrides the culture for the current asynchronous flow until the returned scope is disposed.</summary>
    /// <param name="cultureName">An IETF language tag, such as <c>it-IT</c>.</param>
    /// <returns>A scope that restores the previous culture when disposed.</returns>
    IDisposable BeginCultureScope(string cultureName);

    /// <summary>Finds the supported culture that best matches a requested one.</summary>
    /// <param name="cultureName">
    /// An IETF language tag, such as <c>it-IT</c>. A name no platform knows, or one that is empty,
    /// returns <see langword="null"/> rather than throwing.
    /// </param>
    /// <returns>The culture to use, or <see langword="null"/> when nothing matches.</returns>
    /// <remarks>
    /// The same matching <see cref="BeginCultureScope(CultureInfo)"/> and
    /// <see cref="SetCultureAsync(CultureInfo, CancellationToken)"/> apply — exact name first, then a
    /// shared language, so a request for <c>it</c> resolves to a shipped <c>it-IT</c> — but as a question rather
    /// than an instruction. Anything choosing a culture from untrusted input wants this: a request
    /// header naming a language the application does not ship is an ordinary occurrence, not an error
    /// worth an exception per request.
    /// </remarks>
    CultureInfo? NegotiateCulture(string? cultureName);
}
