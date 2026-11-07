using System.Diagnostics.CodeAnalysis;

namespace LocalizationKit;

/// <summary>
/// The developer-facing entry point for looking up translated strings in the culture that is
/// currently selected on the <see cref="ILocalizationContext"/>.
/// </summary>
/// <remarks>
/// Every member on this interface is synchronous by design. Resources are loaded asynchronously
/// once, by <see cref="ILocalizationContext.InitializeAsync(System.Threading.CancellationToken)"/>
/// and by <see cref="ILocalizationContext.SetCultureAsync(string, System.Threading.CancellationToken)"/>,
/// so a property getter bound to the UI never performs disk or network I/O.
/// </remarks>
public interface ILocalizer
{
    /// <summary>Gets the translation for <paramref name="key"/>. Equivalent to <see cref="Get(string)"/>.</summary>
    /// <param name="key">The resource key.</param>
    /// <returns>The translation, or whatever <see cref="LocalizationKitOptions.FallbackBehavior"/> dictates when the key is missing.</returns>
    string this[string key] { get; }

    /// <summary>Gets the translation for <paramref name="key"/> without any argument substitution.</summary>
    /// <param name="key">The resource key.</param>
    /// <returns>The translation, or whatever <see cref="LocalizationKitOptions.FallbackBehavior"/> dictates when the key is missing.</returns>
    /// <exception cref="MissingResourceException">
    /// The key was not found and <see cref="LocalizationKitOptions.FallbackBehavior"/> is <see cref="FallbackBehavior.Throw"/>.
    /// </exception>
    string Get(string key);

    /// <summary>Gets the translation for <paramref name="key"/> and substitutes its message arguments.</summary>
    /// <param name="key">The resource key.</param>
    /// <param name="arguments">
    /// An object whose public properties supply the message arguments, such as
    /// <c>new { name = "Francesco" }</c>, or a dictionary. Reading properties off an anonymous type
    /// uses reflection; prefer <see cref="Format(string, IReadOnlyDictionary{string, object?})"/>
    /// in trimmed or native-AOT applications.
    /// </param>
    /// <returns>The formatted translation.</returns>
    [RequiresUnreferencedCode(
        "Message arguments are read from the object's public properties by reflection, and trimming can remove the properties of the type passed in. Use the overload that takes an IReadOnlyDictionary<string, object?>.")]
    string Format(string key, object? arguments = null);

    /// <summary>Gets the translation for <paramref name="key"/> and substitutes the supplied message arguments.</summary>
    /// <param name="key">The resource key.</param>
    /// <param name="arguments">The message arguments, keyed by name.</param>
    /// <returns>The formatted translation.</returns>
    string Format(string key, IReadOnlyDictionary<string, object?> arguments);

    /// <summary>
    /// Gets the translation for <paramref name="key"/> with <paramref name="count"/> supplied as the
    /// <c>count</c> message argument.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <param name="count">The number that selects the plural category.</param>
    /// <returns>The formatted translation.</returns>
    /// <exception cref="UnsupportedMessageConstructException">
    /// The message uses an ICU <c>plural</c> construct and the registered <see cref="IMessageFormatter"/>
    /// cannot select a CLDR category. Add the <c>LocalizationKit.Messages.Icu</c> package and call
    /// <c>AddIcuMessages()</c>; the default engine does plain argument substitution only.
    /// </exception>
    string Plural(string key, decimal count);

    /// <summary>
    /// Resolves a key without applying <see cref="LocalizationKitOptions.FallbackBehavior"/>, so the
    /// caller can tell a translation apart from a missing one.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <param name="value">The translation, when the key resolves.</param>
    /// <returns><see langword="true"/> when some culture in the fallback chain has the key.</returns>
    bool TryGet(string key, [NotNullWhen(true)] out string? value);

    /// <summary>Gets every entry resolvable in the selected culture.</summary>
    /// <param name="includeFallbacks">
    /// Whether to include entries inherited from the cultures this one falls back to. When
    /// <see langword="false"/>, only the entries authored for the selected culture are returned.
    /// </param>
    /// <returns>The entries, keyed as they are in the resources.</returns>
    /// <remarks>
    /// For diagnostics, for exporting what an application actually ships, and for bridging to APIs
    /// that enumerate, such as <c>IStringLocalizer.GetAllStrings</c>. Not a lookup path: resolving one
    /// key through <see cref="Get"/> is a few dictionary probes, this builds a dictionary.
    /// </remarks>
    IReadOnlyDictionary<string, string> GetAll(bool includeFallbacks = true);

    /// <summary>A view of this localizer whose keys are read relative to a prefix.</summary>
    /// <param name="prefix">The prefix, without a trailing dot: <c>Checkout</c>, not <c>Checkout.</c>.</param>
    /// <returns>A localizer that prepends the prefix to every key it is asked for.</returns>
    /// <remarks>
    /// <para>
    /// For a screen that owns a few dozen keys and spells the same prefix at every call site:
    /// <c>Scoped("Checkout")["Save"]</c> asks for <c>Checkout.Save</c>. There is still one flat key
    /// space, so a scope is a shorter way of writing a key rather than a second namespace it lives in.
    /// </para>
    /// <para>
    /// A key the scope does not have falls through to the bare key, which keeps the shared keys
    /// shared: a page scoped to <c>Checkout</c> still resolves <c>Common.Cancel</c> without it being
    /// duplicated under every prefix, and adding a scope cannot break a lookup that already worked.
    /// </para>
    /// </remarks>
    ILocalizer Scoped(string prefix);

    /// <summary>
    /// Gets an observable view of <paramref name="key"/> that raises
    /// <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/> whenever the
    /// selected culture changes or resources are reloaded.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <returns>
    /// A cached <see cref="ILocalizedString"/>. Calling this twice with the same key returns the same
    /// instance, so binding to it does not accumulate subscriptions.
    /// </returns>
    ILocalizedString Observe(string key);
}
