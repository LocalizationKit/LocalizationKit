using LocalizationKit;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the JSON resource provider.</summary>
public static class JsonServiceCollectionExtensions
{
    /// <summary>
    /// Adds a provider that reads <c>Localization/&lt;culture&gt;.json</c>. Call it after
    /// <c>AddLocalizationKit</c>; several providers may be registered, and the first one registered
    /// wins for a key they share.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the resource path, file name pattern and search roots.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddJsonLocalization(
        this IServiceCollection services,
        Action<JsonLocalizationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        JsonLocalizationOptions options = new();
        configure?.Invoke(options);

        services.AddSingleton<ILocalizationResourceProvider>(new JsonResourceProvider(options));
        return services;
    }
}
