using LocalizationKit;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the localization engine.</summary>
public static class LocalizationKitServiceCollectionExtensions
{
    /// <summary>
    /// Adds the core localization services. Register at least one resource provider as well, for
    /// example with <c>AddReswLocalization</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures cultures and fallback behaviour.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The options are contradictory.</exception>
    public static IServiceCollection AddLocalizationKit(
        this IServiceCollection services,
        Action<LocalizationKitOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        LocalizationKitOptions options = new();
        configure?.Invoke(options);
        options.Validate();

        services.TryAddSingleton(options);
        services.TryAddSingleton<ResourceCatalog>();
        services.TryAddSingleton<IMessageFormatter, SimpleMessageFormatter>();
        services.TryAddSingleton<ILocalizationContext, LocalizationContext>();
        services.TryAddSingleton<ILocalizer, Localizer>();
        services.TryAddSingleton<ILocalizationFormatter, LocalizationFormatter>();

        return services;
    }
}
