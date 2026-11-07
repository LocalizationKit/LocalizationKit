using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;

namespace LocalizationKit.WinUI;

/// <summary>
/// The bridge between XAML and the localization engine.
/// </summary>
/// <remarks>
/// <para>
/// XAML cannot be given services, so a markup extension has to reach the engine through a static
/// hook. Call <see cref="Initialize(IServiceProvider)"/> once at startup, after the resources have
/// been loaded. Everything else in the library takes its dependencies normally; this is the one
/// place where the XAML runtime forces a static.
/// </para>
/// <para>
/// Before initialization, and in the designer, resolving a key returns the key itself rather than
/// throwing, so a page still renders at design time.
/// </para>
/// </remarks>
public static class LocalizationKitXaml
{
    private static ILocalizer? localizer;
    private static ILocalizationContext? context;
    private static DispatcherQueue? dispatcher;

    /// <summary>Gets a value indicating whether XAML can resolve translations.</summary>
    public static bool IsInitialized => localizer is not null;

    /// <summary>
    /// Wires XAML to the engine, taking the dispatcher of the calling thread as the one to raise UI
    /// updates on. Call it from the UI thread once resources are loaded.
    /// </summary>
    /// <param name="services">The application's service provider.</param>
    public static void Initialize(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Initialize(
            services.GetRequiredService<ILocalizer>(),
            services.GetRequiredService<ILocalizationContext>(),
            DispatcherQueue.GetForCurrentThread());
    }

    /// <summary>Wires XAML to the engine explicitly.</summary>
    /// <param name="localizer">The localizer XAML resolves through.</param>
    /// <param name="context">The culture context to follow.</param>
    /// <param name="dispatcher">
    /// The UI dispatcher. When <see langword="null"/>, updates are applied on whichever thread
    /// completed the culture change, which is only safe if that is already the UI thread.
    /// </param>
    public static void Initialize(ILocalizer localizer, ILocalizationContext context, DispatcherQueue? dispatcher)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(context);

        if (LocalizationKitXaml.context is not null)
        {
            LocalizationKitXaml.context.CultureChanged -= OnCultureChanged;
        }

        LocalizationKitXaml.localizer = localizer;
        LocalizationKitXaml.context = context;
        LocalizationKitXaml.dispatcher = dispatcher;
        context.CultureChanged += OnCultureChanged;
    }

    /// <summary>Resolves a key, or returns the key when XAML has not been wired up yet.</summary>
    /// <param name="key">The resource key.</param>
    /// <returns>The translation, or the key.</returns>
    public static string Resolve(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        ILocalizer? current = localizer;
        if (current is null)
        {
            return key;
        }

        try
        {
            return current.Get(key);
        }
        catch (InvalidOperationException)
        {
            // Resources are not loaded yet; a visible key beats a crash in a page constructor.
            return key;
        }
    }

    /// <summary>
    /// Remembers that an element's property came from a key, so it can be re-applied after a culture
    /// change. Held weakly: tracking an element never keeps a page alive.
    /// </summary>
    /// <param name="target">The element whose property was set.</param>
    /// <param name="propertyName">The property that was set, such as <c>Text</c>.</param>
    /// <param name="key">The resource key it was set from.</param>
    /// <param name="declaringType">
    /// The type declaring the property, when known. Needed for an attached property such as
    /// <c>ToolTipService.ToolTip</c>, whose dependency property is not on the element.
    /// </param>
    public static void Track(object target, string propertyName, string key, Type? declaringType = null)
        => LocalizedTargetRegistry.Track(target, propertyName, key, declaringType);

    /// <summary>
    /// Remembers a target whose translated value is not simply assigned to a property, such as a text
    /// block whose inlines are rebuilt from markup.
    /// </summary>
    /// <param name="target">The element to refresh.</param>
    /// <param name="key">The resource key it was built from.</param>
    /// <param name="apply">Applies the resolved string to the target. Runs on the UI dispatcher.</param>
    public static void Track(object target, string key, Action<object, string> apply)
        => LocalizedTargetRegistry.Track(target, key, apply);

    private static void OnCultureChanged(object? sender, CultureChangedEventArgs e)
    {
        DispatcherQueue? queue = dispatcher;
        if (queue is null || queue.HasThreadAccess)
        {
            LocalizedTargetRegistry.ReapplyAll(Resolve);
            return;
        }

        queue.TryEnqueue(() => LocalizedTargetRegistry.ReapplyAll(Resolve));
    }
}
