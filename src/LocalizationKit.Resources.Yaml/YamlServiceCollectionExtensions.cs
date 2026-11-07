using LocalizationKit;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the YAML resource provider.</summary>
public static class YamlServiceCollectionExtensions
{
    /// <summary>
    /// Adds a provider that reads <c>Localization/&lt;culture&gt;.yaml</c>, or <c>.yml</c>. Call it
    /// after <c>AddLocalizationKit</c>; several providers may be registered, and the first one
    /// registered wins for a key they share.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the resource path, file name patterns and search roots.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddYamlLocalization(
        this IServiceCollection services,
        Action<YamlLocalizationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        YamlLocalizationOptions options = new();
        configure?.Invoke(options);

        services.AddSingleton<ILocalizationResourceProvider>(new YamlResourceProvider(options));
        return services;
    }
}
