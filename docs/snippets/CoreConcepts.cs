using System.ComponentModel;
using LocalizationKit;
using Microsoft.Extensions.DependencyInjection;

namespace LocalizationKit.Docs.Snippets;

internal static class CoreConcepts
{
    internal static void Fallback()
    {
        var services = new ServiceCollection();

        // <fallback>
        services.AddLocalizationKit(options =>
        {
            options.DefaultCulture = "en-US";
            options.SupportedCultures = ["en-US", "it-IT"];

            // ReturnKey shows the key itself, which is visible in testing.
            // ReturnEmpty hides the gap. Throw refuses to ship with one.
            options.FallbackBehavior = FallbackBehavior.Throw;
        });
        // </fallback>
    }

    internal static void MissingKeyHook()
    {
        var services = new ServiceCollection();

        // <missing-key>
        services.AddLocalizationKit(options =>
        {
            options.OnMissingKey = missing =>
                Console.Error.WriteLine($"No '{missing.Key}' in {missing.Culture}");
        });
        // </missing-key>
    }

    internal static void Observing(ILocalizer localizer)
    {
        // <observing>
        ILocalizedString save = localizer.Observe("Common.Save");

        save.PropertyChanged += (_, _) => Console.WriteLine(save.Value);

        // save.Value is the current translation, and re-reads itself when the culture changes.
        Console.WriteLine(save.Value);
        // </observing>
    }

    internal static void Prefixes(ILocalizer localizer)
    {
        // <scoped>
        ILocalizer checkout = localizer.Scoped("Checkout");

        // Looks for Checkout.Save, and falls through to the bare Save if the prefix has none.
        string save = checkout.Get("Save");
        // </scoped>

        _ = save;
    }

    internal static async Task ScopesAsync(ILocalizationContext context, ILocalizer localizer)
    {
        // <culture-scope>
        // The selected culture is process-wide. A scope overrides it for one asynchronous flow,
        // which is how two requests are answered in two languages at the same time.
        using (context.BeginCultureScope("it-IT"))
        {
            await Task.Yield();
            Console.WriteLine(localizer.Get("Common.Save"));   // Italian
        }

        Console.WriteLine(localizer.Get("Common.Save"));       // whatever it was before
        // </culture-scope>
    }

    internal static void Negotiate(ILocalizationContext context)
    {
        // <negotiate>
        // "it" resolves to the shipped it-IT. An unknown or malformed name returns null rather
        // than throwing, because the name usually came from a request header.
        var culture = context.NegotiateCulture("it");
        // </negotiate>

        _ = culture;
    }

    internal static void Formatting(ILocalizationFormatter formatter)
    {
        // <formatting>
        string price = formatter.Currency(123.50m, "EUR");
        string count = formatter.Number(1234.5m);
        string share = formatter.Percent(0.42m, decimals: 1);
        string day = formatter.Date(DateTime.Now);

        // Or in a specific culture, regardless of the selected one.
        string italianPrice = formatter.In("it-IT").Currency(123.50m, "EUR");
        // </formatting>

        _ = (price, count, share, day, italianPrice);
    }
}
