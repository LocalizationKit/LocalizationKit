using System.Windows.Data;
using System.Windows.Markup;

namespace LocalizationKit.Wpf;

/// <summary>Puts a translation into XAML: <c>{loc:Text Key=Common.Save}</c>.</summary>
/// <remarks>
/// <para>
/// It returns a binding rather than a string, which is what makes the value follow a language change
/// without this class keeping track of anything. Everything else here — the fallback, the format —
/// is about what happens when a key is missing or a value needs arguments.
/// </para>
/// <para>
/// The key may also be given positionally, <c>{loc:Text Common.Save}</c>, which reads better in a
/// file with a hundred of them.
/// </para>
/// </remarks>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TextExtension : MarkupExtension
{
    /// <summary>Initializes a new instance of the <see cref="TextExtension"/> class.</summary>
    public TextExtension()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TextExtension"/> class.</summary>
    /// <param name="key">The resource key.</param>
    public TextExtension(string key)
    {
        Key = key;
    }

    /// <summary>Gets or sets the resource key.</summary>
    [ConstructorArgument("key")]
    public string? Key { get; set; }

    /// <summary>
    /// Gets or sets a format applied to the translation, where <c>{0}</c> is the translation itself.
    /// </summary>
    /// <remarks>
    /// For the punctuation around a string that a translator should not have to carry: a colon after
    /// a label, brackets around a hint. Text that belongs to the sentence belongs in the resource.
    /// </remarks>
    public string? StringFormat { get; set; }

    /// <summary>Gets or sets what to show while the designer has no localizer.</summary>
    /// <remarks>
    /// Visual Studio's designer builds the XAML without the application, so there is no localizer to
    /// ask. Showing the key keeps the layout honest about its width; a fallback is for when the key
    /// is long and the real string is not.
    /// </remarks>
    public string? DesignTimeValue { get; set; }

    /// <inheritdoc/>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            throw new InvalidOperationException(
                "{loc:Text} needs a key: {loc:Text Key=Common.Save}, or {loc:Text Common.Save}.");
        }

        if (LocalizationSource.Current is null)
        {
            // The designer, or an application that has not called LocalizationKitXaml.Initialize yet.
            // A binding to nothing would silently show nothing, which looks like a layout bug rather
            // than a missing call.
            return DesignTimeValue ?? Key;
        }

        Binding binding = new($"[{Key}]")
        {
            Source = LocalizationSource.Current,
            Mode = BindingMode.OneWay,
            StringFormat = StringFormat,
        };

        return binding.ProvideValue(serviceProvider);
    }
}
