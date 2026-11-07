using LocalizationKit;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the <c>.resw</c> resource provider.</summary>
public static class ReswServiceCollectionExtensions
{
    /// <summary>
    /// Adds a provider that reads <c>Strings/&lt;culture&gt;/Resources.resw</c>. Call it after
    /// <c>AddLocalizationKit</c>; registering several providers is allowed, and the first one
    /// registered wins for a key they share.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the resource path, file name and search roots.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddReswLocalization(
        this IServiceCollection services,
        Action<ReswLocalizationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        ReswLocalizationOptions options = new();
        configure?.Invoke(options);

        services.AddSingleton<ILocalizationResourceProvider>(new ReswResourceProvider(options));
        return services;
    }
}
