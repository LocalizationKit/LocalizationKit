using LocalizationKit;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers per-request culture negotiation.</summary>
public static class LocalizationKitAspNetCoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds the options the request culture middleware reads. Call it after <c>AddLocalizationKit</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Changes to the defaults: where a request may state its culture.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddLocalizationKitRequestCulture(
        this IServiceCollection services,
        Action<RequestCultureOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        RequestCultureOptions options = new();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        return services;
    }
}
