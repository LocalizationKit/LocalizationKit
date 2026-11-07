using System.Diagnostics.CodeAnalysis;

namespace LocalizationKit;

/// <summary>A view of a localizer whose keys are read relative to a prefix.</summary>
/// <remarks>
/// <para>
/// There is one flat key space, and this does not change that: a scope is a shorter way of writing
/// the same keys, not a second namespace they live in. <c>Scoped("Checkout")["Save"]</c> asks for
/// <c>Checkout.Save</c>.
/// </para>
/// <para>
/// A key the scope does not have falls through to the bare key, which is what makes a scope safe to
/// introduce into code that already works. A page scoped to <c>Checkout</c> still resolves
/// <c>Common.Cancel</c>, so the shared keys stay shared instead of having to be duplicated under
/// every prefix, and adding a scope to an existing page cannot break a lookup that used to succeed.
/// </para>
/// </remarks>
internal sealed class ScopedLocalizer : ILocalizer
{
    private readonly ILocalizer _inner;
    private readonly string _prefix;

    public ScopedLocalizer(ILocalizer inner, string prefix)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        _inner = inner;
        _prefix = prefix.TrimEnd('.');
    }

    /// <inheritdoc/>
    public string this[string key] => Get(key);

    /// <inheritdoc/>
    public string Get(string key)
    {
        return Resolve(key, out string resolved) ? _inner.Get(resolved) : _inner.Get(key);
    }

    /// <inheritdoc/>
    [RequiresUnreferencedCode(
        "Message arguments are read from the object's public properties by reflection, and trimming can remove the properties of the type passed in. Use the overload that takes an IReadOnlyDictionary<string, object?>.")]
    public string Format(string key, object? arguments = null)
    {
        return Resolve(key, out string resolved)
            ? _inner.Format(resolved, arguments)
            : _inner.Format(key, arguments);
    }

    /// <inheritdoc/>
    public string Format(string key, IReadOnlyDictionary<string, object?> arguments)
    {
        return Resolve(key, out string resolved)
            ? _inner.Format(resolved, arguments)
            : _inner.Format(key, arguments);
    }

    /// <inheritdoc/>
    public string Plural(string key, decimal count)
    {
        return Resolve(key, out string resolved) ? _inner.Plural(resolved, count) : _inner.Plural(key, count);
    }

    /// <inheritdoc/>
    public bool TryGet(string key, [NotNullWhen(true)] out string? value)
    {
        return _inner.TryGet(Prefixed(key), out value) || _inner.TryGet(key, out value);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The keys under the scope, with the prefix taken off, so that what comes back can be read with
    /// the same keys this view is asked for.
    /// </remarks>
    public IReadOnlyDictionary<string, string> GetAll(bool includeFallbacks = true)
    {
        string prefix = _prefix + ".";
        Dictionary<string, string> scoped = new(StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, string> entry in _inner.GetAll(includeFallbacks))
        {
            if (entry.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                scoped[entry.Key[prefix.Length..]] = entry.Value;
            }
        }

        return scoped;
    }

    /// <inheritdoc/>
    public ILocalizedString Observe(string key)
    {
        // Which key exists can change when the resources reload, so the observable follows the same
        // rule as a lookup: the scoped key when there is one, the bare key otherwise.
        return _inner.Observe(Resolve(key, out string resolved) ? resolved : key);
    }

    /// <inheritdoc/>
    public ILocalizer Scoped(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        return new ScopedLocalizer(_inner, _prefix + "." + prefix.TrimEnd('.'));
    }

    private string Prefixed(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _prefix + "." + key;
    }

    private bool Resolve(string key, out string resolved)
    {
        resolved = Prefixed(key);
        return _inner.TryGet(resolved, out _);
    }
}
