using LocalizationKit;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizationKit.Docs.Snippets;

internal static class GettingStarted
{
    internal static IServiceCollection Registration()
    {
        var services = new ServiceCollection();

        // <registration>
        services.AddLocalizationKit(options =>
        {
            options.DefaultCulture = "en-US";
            options.SupportedCultures = ["en-US", "it-IT"];
            options.FallbackBehavior = FallbackBehavior.ReturnKey;
        });

        services.AddReswLocalization(options =>
        {
            options.ResourcesPath = "Strings";       // Strings/<culture>/Resources.resw
            options.FileName = "Resources.resw";
        });

        services.AddIcuMessages();                   // plurals, ordinals, select
        // </registration>

        return services;
    }

    internal static async Task StartupAsync(IServiceProvider provider)
    {
        // <initialize>
        // Resolving a key before this throws, rather than quietly returning key names.
        await provider.InitializeLocalizationKitAsync();
        // </initialize>
    }

    internal static void Resolving(ILocalizer localizer)
    {
        // <resolving>
        string save = localizer.Get("Common.Save");
        string welcome = localizer.Format("Welcome", new { name = "Francesco" });
        string items = localizer.Plural("Cart.Items", 5);
        // </resolving>

        _ = (save, welcome, items);
    }

    internal static void TrimSafeArguments(ILocalizer localizer)
    {
        string name = "Francesco";

        // <trim-safe-arguments>
        // Reading an anonymous type needs reflection, so this warns (IL2026) when trimming.
        string viaAnonymous = localizer.Format("Welcome", new { name });

        // The dictionary overload is reflection-free, and the one to use in a trimmed application.
        string viaDictionary = localizer.Format(
            "Welcome",
            new Dictionary<string, object?> { ["name"] = name });
        // </trim-safe-arguments>

        _ = (viaAnonymous, viaDictionary);
    }

    internal static async Task SwitchingAsync(ILocalizationContext context)
    {
        // <switching>
        await context.SetCultureAsync("it-IT");   // every observed string re-reads itself
        // </switching>
    }

    internal static void Missing(ILocalizer localizer)
    {
        // <try-get>
        // TryGet asks whether the key exists, without the configured fallback standing in for it.
        if (localizer.TryGet("Settings.Experimental.Title", out string? title))
        {
            Console.WriteLine(title);
        }
        // </try-get>
    }
}
