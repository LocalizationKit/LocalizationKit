using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;

namespace LocalizationKit.WinUI;

/// <summary>
/// Resolves a translation in XAML: <c>&lt;TextBlock Text="{loc:Text Key=Common.Save}" /&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// Windows Runtime XAML does not support positional arguments in a custom markup extension, so the
/// property has to be named: <c>{loc:Text Key=Common.Save}</c>, not <c>{loc:Text Common.Save}</c>.
/// </para>
/// <para>
/// A markup extension is evaluated once, when the page loads. To survive a culture change, this one
/// records the element and property it was applied to and <see cref="LocalizationKitXaml"/> sets
/// them again afterwards. Where the XAML runtime does not report a target — inside a resource
/// dictionary, for instance — the value is still correct at load time but will not refresh, and
/// <see cref="Loc"/> is the way to get a live value there.
/// </para>
/// </remarks>
[MarkupExtensionReturnType(ReturnType = typeof(string))]
public sealed partial class TextExtension : MarkupExtension
{
    /// <summary>Gets or sets the resource key to resolve.</summary>
    public string Key { get; set; } = string.Empty;

    /// <inheritdoc/>
    protected override object ProvideValue(IXamlServiceProvider serviceProvider)
    {
        string value = LocalizationKitXaml.Resolve(Key);

        if (serviceProvider?.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget target
            && target.TargetObject is not null
            && target.TargetProperty is ProvideValueTargetProperty property
            && !string.IsNullOrEmpty(property.Name))
        {
            LocalizationKitXaml.Track(target.TargetObject, property.Name, Key, property.DeclaringType);
        }

        return value;
    }

    /// <inheritdoc/>
    protected override object ProvideValue() => LocalizationKitXaml.Resolve(Key);
}
