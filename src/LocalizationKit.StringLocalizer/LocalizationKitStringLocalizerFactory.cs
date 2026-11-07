using Microsoft.Extensions.Localization;

namespace LocalizationKit;

/// <summary>
/// Serves <see cref="IStringLocalizerFactory"/> from LocalizationKit's resources.
/// </summary>
/// <remarks>
/// Every localizer this produces reads the same flat key space, because that is what LocalizationKit
/// has: the base name and location a caller asks for select no separate resource file. Returning one
/// shared instance is therefore honest rather than lazy — pretending to partition would only hide
/// that nothing is partitioned.
/// </remarks>
public sealed class LocalizationKitStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly LocalizationKitStringLocalizer _localizer;
    private readonly ILocalizer _source;
    private readonly ILocalizationContext _context;

    /// <summary>Initializes a new instance of the <see cref="LocalizationKitStringLocalizerFactory"/> class.</summary>
    /// <param name="localizer">The localizer to resolve through.</param>
    /// <param name="context">The culture context.</param>
    public LocalizationKitStringLocalizerFactory(ILocalizer localizer, ILocalizationContext context)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(context);

        _source = localizer;
        _context = context;
        _localizer = new LocalizationKitStringLocalizer(localizer, context);
    }

    /// <inheritdoc/>
    public IStringLocalizer Create(Type resourceSource)
    {
        ArgumentNullException.ThrowIfNull(resourceSource);

        // The same rule the generic localizer follows: the type names a key prefix, and a key the
        // prefix does not have falls through to the bare key.
        return new LocalizationKitStringLocalizer(
            LocalizationKitStringLocalizer<object>.Scope(_source, resourceSource),
            _context);
    }

    /// <inheritdoc/>
    public IStringLocalizer Create(string baseName, string location)
    {
        // baseName is a resource name — "Shop.Web.Pages.Checkout" — and its last segment is the
        // prefix, for the same reason a type contributes its name rather than its full name.
        if (string.IsNullOrWhiteSpace(baseName))
        {
            return _localizer;
        }

        int dot = baseName.LastIndexOf('.');
        string prefix = dot < 0 ? baseName : baseName[(dot + 1)..];

        return prefix.Length == 0
            ? _localizer
            : new LocalizationKitStringLocalizer(_source.Scoped(prefix), _context);
    }
}
