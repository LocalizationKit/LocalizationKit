using LocalizationKit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace LocalizationKit.Docs.Snippets;

internal static class Features
{
    internal static void Plurals(ILocalizer localizer)
    {
        // <plural>
        // The count lives in the resource, so a translator can move it, and CLDR decides which
        // branch a number lands in. No `if (count == 1)` anywhere.
        string zero = localizer.Plural("Cart.Items", 0);
        string one = localizer.Plural("Cart.Items", 1);
        string many = localizer.Plural("Cart.Items", 5);
        // </plural>

        _ = (zero, one, many);
    }

    internal static void ValidateAMessage()
    {
        // <validate-message>
        // The same check the build and the editor run, available at runtime and without the ICU
        // engine referenced: a misspelled category or a missing `other` branch is rejected here.
        if (!IcuMessageSyntax.TryValidate("{count, plural, one {# item} oen {# items}}", out string? error))
        {
            Console.Error.WriteLine(error);
        }
        // </validate-message>
    }

    internal static void ArgumentsOfAMessage()
    {
        // <message-arguments>
        // What a message asks to be given. A translation whose arguments differ from the default
        // culture's is what LK015 fails the build over.
        IReadOnlyList<string> arguments =
            IcuMessageSyntax.GetArgumentNames("{count, plural, one {# of {total}} other {# of {total}}}");
        // arguments: count, total
        // </message-arguments>

        _ = arguments;
    }

    internal static void Pseudolocalize()
    {
        var services = new ServiceCollection();

        // <pseudolocalize>
        services.AddLocalizationKit(options =>
        {
            // "Save" arrives as "⟦Șàṽé∙∙⟧". Arguments, plural categories and link markup are
            // left alone, so a rewritten message still formats.
            options.Pseudolocalize = true;
        });
        // </pseudolocalize>
    }

    internal static void Providers()
    {
        var services = new ServiceCollection();

        // <providers>
        services.AddReswLocalization();     // Strings/<culture>/Resources.resw
        services.AddJsonLocalization();     // flat or nested JSON
        services.AddYamlLocalization();     // flat or nested YAML
        // </providers>
    }

    internal static void StringLocalizerBridge()
    {
        var services = new ServiceCollection();

        // <stringlocalizer-registration>
        services.AddLocalizationKitStringLocalizer();
        // </stringlocalizer-registration>
    }

    internal static void StringLocalizerUse(
        IStringLocalizer localizer,
        IStringLocalizer<Checkout> typed)
    {
        string name = "Francesco";

        // <stringlocalizer-use>
        LocalizedString items = localizer["Cart.Items", 5];        // renders an ICU plural
        LocalizedString welcome = localizer["Welcome", name];      // {0} or {name}, either works

        // The type argument names a key prefix: this asks for Checkout.Save.
        LocalizedString save = typed["Save"];
        // </stringlocalizer-use>

        _ = (items, welcome, save);
    }

    internal sealed class Checkout;
}
