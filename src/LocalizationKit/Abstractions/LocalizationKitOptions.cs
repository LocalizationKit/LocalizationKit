using System.Globalization;

namespace LocalizationKit;

/// <summary>Configuration for the localization engine, supplied to <c>AddLocalizationKit</c>.</summary>
public sealed class LocalizationKitOptions
{
    /// <summary>
    /// Gets or sets the culture every fallback chain ends at. This is the culture your resource keys
    /// are authored in, and it must have a complete set of translations.
    /// </summary>
    /// <remarks>In a WinUI project this should agree with the project's <c>DefaultLanguage</c> property.</remarks>
    public string DefaultCulture { get; set; } = "en";

    /// <summary>
    /// Gets or sets the cultures the application offers. Leave it empty to accept any culture, which
    /// is useful while a provider still discovers what it has on disk.
    /// </summary>
    public IList<string> SupportedCultures { get; set; } = [];

    /// <summary>
    /// Gets or sets the culture to select during
    /// <see cref="ILocalizationContext.InitializeAsync(CancellationToken)"/>. When
    /// <see langword="null"/>, the engine negotiates <see cref="CultureInfo.CurrentUICulture"/>
    /// against <see cref="SupportedCultures"/> and falls back to <see cref="DefaultCulture"/>.
    /// </summary>
    public string? InitialCulture { get; set; }

    /// <summary>Gets or sets what a lookup returns when a key is missing from every culture in the chain.</summary>
    public FallbackBehavior FallbackBehavior { get; set; } = FallbackBehavior.ReturnKey;

    /// <summary>
    /// Gets or sets a value indicating whether initialization loads every supported culture rather
    /// than only the selected one.
    /// </summary>
    /// <remarks>
    /// Turn this on in a server, where any request may ask for any supported culture and
    /// <see cref="ILocalizationContext.BeginCultureScope(System.Globalization.CultureInfo)"/> needs
    /// the resources to be there already. A desktop application loads one culture and switches rarely,
    /// so it can leave this off.
    /// </remarks>
    public bool PreloadSupportedCultures { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether every translation is rewritten to look translated,
    /// for finding the faults that only a second language exposes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With this on, <c>Save</c> arrives as <c>⟦Șàṽé∙∙⟧</c>: longer, so a label that cannot grow is
    /// clipped where it can be seen; accented, so a string that is still plain English is one nobody
    /// externalized; and bracketed, so a sentence built from two resources shows up with brackets in
    /// the middle of it.
    /// </para>
    /// <para>
    /// Arguments, plural categories and link markup are left alone — see
    /// <see cref="Pseudolocalizer"/> — so a pseudolocalized message still formats. Turn it on in a
    /// debug build or behind a developer switch, never in a release.
    /// </para>
    /// </remarks>
    public bool Pseudolocalize { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether selecting a culture also assigns
    /// <see cref="CultureInfo.DefaultThreadCurrentCulture"/> and
    /// <see cref="CultureInfo.DefaultThreadCurrentUICulture"/>.
    /// </summary>
    /// <remarks>
    /// Convenient for a desktop or console application, where the selected culture really is
    /// process-wide. A server application must leave this <see langword="false"/>: concurrent
    /// requests have different cultures, and process-wide state cannot represent that.
    /// </remarks>
    public bool FlowToThreadCulture { get; set; } = true;

    /// <summary>
    /// Gets or sets a callback invoked once per missing key and culture, before
    /// <see cref="FallbackBehavior"/> is applied. Intended for diagnostics and for failing a test run.
    /// </summary>
    public Action<MissingResourceInfo>? OnMissingKey { get; set; }

    /// <summary>Validates the options and throws when they cannot produce a working engine.</summary>
    /// <exception cref="InvalidOperationException">The configuration is contradictory.</exception>
    /// <exception cref="CultureNotFoundException">A configured culture name is not a culture.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DefaultCulture))
        {
            throw new InvalidOperationException(
                $"{nameof(LocalizationKitOptions)}.{nameof(DefaultCulture)} must be set to the culture your resource keys are authored in.");
        }

        _ = CultureInfo.GetCultureInfo(DefaultCulture);

        foreach (string name in SupportedCultures)
        {
            _ = CultureInfo.GetCultureInfo(name);
        }

        if (SupportedCultures.Count > 0
            && !SupportedCultures.Any(c => string.Equals(c, DefaultCulture, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"{nameof(SupportedCultures)} does not contain {nameof(DefaultCulture)} '{DefaultCulture}'. "
                + "The default culture is the end of every fallback chain, so it must be supported.");
        }

        if (InitialCulture is not null)
        {
            _ = CultureInfo.GetCultureInfo(InitialCulture);
        }
    }
}
