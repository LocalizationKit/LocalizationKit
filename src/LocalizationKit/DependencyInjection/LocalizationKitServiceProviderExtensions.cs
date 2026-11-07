using LocalizationKit;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Startup helpers for the localization engine.</summary>
public static class LocalizationKitServiceProviderExtensions
{
    /// <summary>
    /// Selects the initial culture and loads its resources. Call this once during startup, before the
    /// first UI binding or request is served; resolving a resource beforehand throws, rather than
    /// quietly returning keys.
    /// </summary>
    /// <param name="services">The built service provider.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The culture context, so the caller can switch cultures or subscribe to changes.</returns>
    public static async Task<ILocalizationContext> InitializeLocalizationKitAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        ILocalizationContext context = services.GetRequiredService<ILocalizationContext>();
        await context.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return context;
    }
}
