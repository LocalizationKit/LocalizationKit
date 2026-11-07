using LocalizationKit;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the <see cref="IStringLocalizer"/> compatibility layer.</summary>
public static class StringLocalizerServiceCollectionExtensions
{
    /// <summary>
    /// Serves <see cref="IStringLocalizer"/>, <see cref="IStringLocalizer{T}"/> and
    /// <see cref="IStringLocalizerFactory"/> from LocalizationKit, so code written against
    /// Microsoft's localization abstractions resolves through it unchanged.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <remarks>
    /// Replaces any existing registration, including the one <c>AddLocalization()</c> makes, so the
    /// two cannot both be live and silently disagree about where a string came from.
    /// </remarks>
    public static IServiceCollection AddLocalizationKitStringLocalizer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<IStringLocalizer>();
        services.RemoveAll(typeof(IStringLocalizer<>));
        services.RemoveAll<IStringLocalizerFactory>();

        services.AddSingleton<IStringLocalizer, LocalizationKitStringLocalizer>();
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(LocalizationKitStringLocalizer<>));
        services.AddSingleton<IStringLocalizerFactory, LocalizationKitStringLocalizerFactory>();

        return services;
    }
}
