using System.Globalization;

namespace LocalizationKit;

/// <summary>
/// Supplies the translations for one culture from one storage format: <c>.resw</c> files, JSON files,
/// embedded resources, a database, a remote service.
/// </summary>
/// <remarks>
/// A provider loads a whole culture at a time rather than answering key by key. That is what lets
/// <see cref="ILocalizer"/> stay synchronous after initialization, and it means a provider backed by
/// the network is hit once per culture instead of once per binding.
/// </remarks>
public interface ILocalizationResourceProvider
{
    /// <summary>Gets a short name used in diagnostics, such as <c>resw</c>.</summary>
    string Name { get; }

    /// <summary>Loads every entry this provider has for <paramref name="culture"/> exactly.</summary>
    /// <param name="culture">
    /// The culture to load. The provider must not apply any fallback of its own: the engine walks the
    /// fallback chain and asks for each culture in turn.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The entries for that culture, or <see langword="null"/> when this provider has nothing for it,
    /// which is not an error.
    /// </returns>
    ValueTask<IReadOnlyDictionary<string, string>?> LoadAsync(
        CultureInfo culture,
        CancellationToken cancellationToken = default);
}
