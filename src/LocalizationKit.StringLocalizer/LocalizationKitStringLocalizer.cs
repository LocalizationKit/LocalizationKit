using System.Globalization;
using Microsoft.Extensions.Localization;

namespace LocalizationKit;

/// <summary>
/// Serves <see cref="IStringLocalizer"/> from LocalizationKit's resources.
/// </summary>
/// <remarks>
/// <para>
/// The point is adoption without rewriting: an application already written against
/// <see cref="IStringLocalizer"/> — Razor views, MVC controllers, validation attributes, libraries
/// that take one — keeps its call sites and gets LocalizationKit's resources, fallback chain and
/// culture scopes underneath.
/// </para>
/// <para>
/// Two differences from Microsoft's RESX-backed implementation are worth knowing. Arguments are
/// substituted by the registered <see cref="IMessageFormatter"/>, so a resource may use ICU
/// <c>{name}</c> placeholders as well as <c>string.Format</c>'s <c>{0}</c>. And
/// <see cref="Microsoft.Extensions.Localization.LocalizedString.ResourceNotFound"/> is accurate, because
/// <see cref="ILocalizer.TryGet"/> reports a miss rather than applying the configured fallback.
/// </para>
/// </remarks>
public class LocalizationKitStringLocalizer : IStringLocalizer
{
    private readonly ILocalizer _localizer;
    private readonly ILocalizationContext _context;

    /// <summary>Initializes a new instance of the <see cref="LocalizationKitStringLocalizer"/> class.</summary>
    /// <param name="localizer">The localizer to resolve through.</param>
    /// <param name="context">The culture context, used to report the culture of a lookup.</param>
    public LocalizationKitStringLocalizer(ILocalizer localizer, ILocalizationContext context)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(context);

        _localizer = localizer;
        _context = context;
    }

    /// <inheritdoc/>
    public LocalizedString this[string name]
    {
        get
        {
            ArgumentException.ThrowIfNullOrEmpty(name);

            return _localizer.TryGet(name, out string? value)
                ? new LocalizedString(name, value, resourceNotFound: false, searchedLocation: SearchedLocation)
                : new LocalizedString(name, name, resourceNotFound: true, searchedLocation: SearchedLocation);
        }
    }

    /// <inheritdoc/>
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentNullException.ThrowIfNull(arguments);

            if (!_localizer.TryGet(name, out string? value))
            {
                return new LocalizedString(name, name, resourceNotFound: true, searchedLocation: SearchedLocation);
            }

            return new LocalizedString(
                name,
                Substitute(name, value, arguments),
                resourceNotFound: false,
                searchedLocation: SearchedLocation);
        }
    }

    /// <inheritdoc/>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        => _localizer.GetAll(includeParentCultures)
            .Select(entry => new LocalizedString(
                entry.Key,
                entry.Value,
                resourceNotFound: false,
                searchedLocation: SearchedLocation));

    private string SearchedLocation => $"LocalizationKit ({_context.UICulture.Name})";

    /// <summary>
    /// Substitutes positional arguments the way <see cref="IStringLocalizer"/> callers expect, while
    /// still allowing a resource to use ICU named arguments.
    /// </summary>
    /// <remarks>
    /// <c>string.Format</c> is used when the resource has no named arguments, because that is what
    /// existing call sites were written against. A resource with named arguments goes through the
    /// message engine, with the positional values bound to <c>arg0</c>, <c>arg1</c> and so on as well
    /// as to their own names in order.
    /// </remarks>
    private string Substitute(string name, string value, object[] arguments)
    {
        IReadOnlyList<string> named = IcuMessageSyntax.GetArgumentNames(value);
        if (named.Count == 0)
        {
            return arguments.Length == 0
                ? value
                : string.Format(_context.Culture, value, arguments);
        }

        Dictionary<string, object?> bound = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < arguments.Length; index++)
        {
            bound[$"arg{index}"] = arguments[index];
            bound[index.ToString(CultureInfo.InvariantCulture)] = arguments[index];

            if (index < named.Count)
            {
                bound[named[index]] = arguments[index];
            }
        }

        return _localizer.Format(name, bound);
    }
}

/// <summary>
/// Serves <see cref="IStringLocalizer{T}"/> from LocalizationKit's resources.
/// </summary>
/// <typeparam name="T">The type whose strings are being localized.</typeparam>
/// <remarks>
/// LocalizationKit has one flat key space, so the type parameter selects no separate resource file:
/// <c>IStringLocalizer&lt;HomeController&gt;["Title"]</c> and <c>IStringLocalizer["Title"]</c> resolve
/// the same key. Name keys by area instead, as in <c>Home.Title</c>. The type parameter is where
/// resource namespaces would attach if they are added later.
/// </remarks>
public sealed class LocalizationKitStringLocalizer<T> : LocalizationKitStringLocalizer, IStringLocalizer<T>
{
    /// <summary>Initializes a new instance of the <see cref="LocalizationKitStringLocalizer{T}"/> class.</summary>
    /// <param name="localizer">The localizer to resolve through.</param>
    /// <param name="context">The culture context.</param>
    /// <remarks>
    /// <para>
    /// The type parameter is what Microsoft's own implementation uses to pick a resource file, and
    /// here it names a key prefix instead: an <c>IStringLocalizer&lt;CheckoutPage&gt;</c> asked for
    /// <c>Save</c> looks for <c>CheckoutPage.Save</c>. One flat key space, read through a scope.
    /// </para>
    /// <para>
    /// A key the prefix does not have falls through to the bare key, so this is not a behaviour change
    /// for code that was already resolving flat keys through the generic localizer: <c>Common.Cancel</c>
    /// still resolves, and so does every key that was found before.
    /// </para>
    /// </remarks>
    public LocalizationKitStringLocalizer(ILocalizer localizer, ILocalizationContext context)
        : base(Scope(localizer, typeof(T)), context)
    {
    }

    /// <summary>The prefix a type stands for: its name, without its namespace.</summary>
    /// <param name="localizer">The localizer to scope.</param>
    /// <param name="type">The type the prefix comes from.</param>
    /// <returns>The scoped localizer.</returns>
    /// <remarks>
    /// The name alone rather than the full name, because a key written <c>CheckoutPage.Save</c> is
    /// what anyone would write in the resource file; <c>Shop.Web.Pages.CheckoutPage.Save</c> is not.
    /// A generic type keeps only the part before the backtick.
    /// </remarks>
    internal static ILocalizer Scope(ILocalizer localizer, Type type)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(type);

        string name = type.Name;
        int generic = name.IndexOf('`', StringComparison.Ordinal);

        return localizer.Scoped(generic < 0 ? name : name[..generic]);
    }
}
