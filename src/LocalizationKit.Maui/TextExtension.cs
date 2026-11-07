using Microsoft.Maui.Controls.Xaml;

namespace LocalizationKit.Maui;

/// <summary>Puts a translation into XAML: <c>{loc:Text Key=Common.Save}</c>.</summary>
/// <remarks>
/// It returns a binding, which is what makes the value follow a language change without this class
/// keeping track of anything. The key may be given positionally — <c>{loc:Text Common.Save}</c> —
/// because it is the content property.
/// </remarks>
[ContentProperty(nameof(Key))]
[AcceptEmptyServiceProvider]
public sealed class TextExtension : IMarkupExtension<BindingBase>
{
    /// <summary>Gets or sets the resource key.</summary>
    public string? Key { get; set; }

    /// <summary>
    /// Gets or sets a format applied to the translation, where <c>{0}</c> is the translation itself.
    /// </summary>
    /// <remarks>
    /// For punctuation around a string that a translator should not have to carry — a colon after a
    /// label. Text that belongs to the sentence belongs in the resource.
    /// </remarks>
    public string? StringFormat { get; set; }

    /// <summary>Gets or sets what to show where there is no localizer, as in a design-time preview.</summary>
    public string? DesignTimeValue { get; set; }

    /// <inheritdoc/>
    public BindingBase ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            throw new InvalidOperationException(
                "{loc:Text} needs a key: {loc:Text Key=Common.Save}, or {loc:Text Common.Save}.");
        }

        if (LocalizationSource.Current is null)
        {
            // No application, so no localizer: a preview, or a startup that has not called
            // LocalizationKitXaml.Initialize yet. A binding to nothing would show nothing, which
            // reads as a layout bug rather than a missing call.
            return new Binding(nameof(StaticValue.Value))
            {
                Source = new StaticValue(DesignTimeValue ?? Key),
                Mode = BindingMode.OneTime,
            };
        }

        // The key goes to a converter rather than into the path, because MAUI's path parser splits on
        // dots before it reads an indexer: "[Common.Save]" becomes "[Common" and "Save]", and it
        // refuses the binding for a missing bracket. Every key here has dots in it. WPF's parser
        // handles the same path, which is why that integration binds the indexer directly.
        return new Binding(nameof(LocalizationSource.Values))
        {
            Source = LocalizationSource.Current,
            Mode = BindingMode.OneWay,
            Converter = Translation,
            ConverterParameter = Key,
            StringFormat = StringFormat,
        };
    }

    /// <inheritdoc/>
    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
    {
        return ProvideValue(serviceProvider);
    }

    private static readonly TranslationConverter Translation = new();

    /// <summary>A value that never changes, for the case where there is nothing to read from.</summary>
    /// <param name="Value">The value to show.</param>
    private sealed record StaticValue(string Value);

    /// <summary>Reads one key out of the source the binding is pointed at.</summary>
    /// <remarks>
    /// The binding watches <c>Values</c>, which returns the source itself, so what arrives here is
    /// the source and the key comes from the parameter. A culture change raises that property and the
    /// binding re-reads.
    /// </remarks>
    private sealed class TranslationConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            return value is LocalizationSource source && parameter is string key
                ? source[key]
                : string.Empty;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException("A translation is read, not written.");
        }
    }
}
