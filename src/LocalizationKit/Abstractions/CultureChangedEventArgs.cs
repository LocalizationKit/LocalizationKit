using System.Globalization;

namespace LocalizationKit;

/// <summary>Describes a culture change that has already been applied.</summary>
/// <param name="previousCulture">The culture that was selected before, or <see langword="null"/> for the initial selection.</param>
/// <param name="culture">The culture now used for formatting.</param>
/// <param name="uiCulture">The culture now used for resource lookup.</param>
/// <param name="reason">Why the change happened.</param>
public sealed class CultureChangedEventArgs(
    CultureInfo? previousCulture,
    CultureInfo culture,
    CultureInfo uiCulture,
    CultureChangeReason reason) : EventArgs
{
    /// <summary>Gets the culture that was selected before, or <see langword="null"/> for the initial selection.</summary>
    public CultureInfo? PreviousCulture { get; } = previousCulture;

    /// <summary>Gets the culture now used for formatting.</summary>
    public CultureInfo Culture { get; } = culture;

    /// <summary>Gets the culture now used for resource lookup.</summary>
    public CultureInfo UICulture { get; } = uiCulture;

    /// <summary>Gets the reason the event was raised.</summary>
    public CultureChangeReason Reason { get; } = reason;
}

/// <summary>Why <see cref="ILocalizationContext.CultureChanged"/> was raised.</summary>
public enum CultureChangeReason
{
    /// <summary>The initial culture was selected during initialization.</summary>
    Initialized = 0,

    /// <summary>A different culture was selected at runtime.</summary>
    CultureSelected = 1,

    /// <summary>The culture did not change, but resources were reloaded, so every value must be re-read.</summary>
    ResourcesReloaded = 2,
}
