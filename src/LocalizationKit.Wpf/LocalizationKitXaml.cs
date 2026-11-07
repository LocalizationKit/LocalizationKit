using Microsoft.Extensions.DependencyInjection;

namespace LocalizationKit.Wpf;

/// <summary>Hands XAML the localizer, once, during startup.</summary>
public static class LocalizationKitXaml
{
    /// <summary>Gives <c>{loc:Text}</c> something to read.</summary>
    /// <param name="services">The provider holding the localizer and the culture context.</param>
    /// <returns>The source XAML binds to, in case an application wants to bind to it directly.</returns>
    /// <remarks>
    /// Call it after the resources are loaded — see <c>InitializeLocalizationKitAsync</c> — and
    /// before the first window is shown. A markup extension is resolved while XAML is being parsed,
    /// which is too early to be given a service provider, so this is the one static handover.
    /// </remarks>
    public static LocalizationSource Initialize(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return Initialize(
            services.GetRequiredService<ILocalizer>(),
            services.GetRequiredService<ILocalizationContext>());
    }

    /// <summary>Gives <c>{loc:Text}</c> something to read.</summary>
    /// <param name="localizer">The localizer to read through.</param>
    /// <param name="context">The culture context to follow.</param>
    /// <returns>The source XAML binds to.</returns>
    public static LocalizationSource Initialize(ILocalizer localizer, ILocalizationContext context)
    {
        LocalizationSource source = new(localizer, context);

        LocalizationSource.Current?.Dispose();
        LocalizationSource.Current = source;

        return source;
    }

    /// <summary>Gets a value indicating whether XAML has been given a localizer.</summary>
    public static bool IsInitialized => LocalizationSource.Current is not null;

    /// <summary>Takes the localizer away again, unsubscribing from the culture it was following.</summary>
    /// <remarks>
    /// For an application that tears down its services while it keeps running — a host restarted
    /// between sign-ins, a test — where leaving a disposed provider's localizer in a static field
    /// would outlive it.
    /// </remarks>
    public static void Reset()
    {
        LocalizationSource.Current?.Dispose();
        LocalizationSource.Current = null;
    }
}
