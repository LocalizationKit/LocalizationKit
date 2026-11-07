using LocalizationKit;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the ICU MessageFormat engine.</summary>
public static class IcuServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the default message engine with the ICU one, so that <c>plural</c>,
    /// <c>selectordinal</c> and <c>select</c> work and in-message number and date formatting is
    /// available.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <remarks>
    /// Can be called before or after <c>AddLocalizationKit</c>: this removes any engine already
    /// registered, and the core only registers its own if none is present.
    /// </remarks>
    public static IServiceCollection AddIcuMessages(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<IMessageFormatter>();
        services.AddSingleton<IMessageFormatter, IcuMessageFormatter>();

        return services;
    }
}
