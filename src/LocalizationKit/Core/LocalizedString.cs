using System.ComponentModel;

namespace LocalizationKit;

/// <summary>
/// The observable translation returned by <see cref="ILocalizer.Observe(string)"/>.
/// </summary>
/// <remarks>
/// Instances are cached per key by the localizer and live as long as it does, so a view can bind to
/// one without leaking a subscription, and a culture change refreshes every live binding at once.
/// <see cref="PropertyChanged"/> is raised on whichever thread completed the culture change; the UI
/// integrations are responsible for marshalling it onto their dispatcher.
/// </remarks>
internal sealed class LocalizedString(ILocalizer localizer, string key) : ILocalizedString
{
    private static readonly PropertyChangedEventArgs ValueChanged = new(nameof(Value));

    public string Key => key;

    public string Value => localizer.Get(key);

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Tells observers to re-read <see cref="Value"/>.</summary>
    public void Refresh() => PropertyChanged?.Invoke(this, ValueChanged);

    public override string ToString() => Value;
}
