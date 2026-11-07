using System.ComponentModel;

namespace LocalizationKit;

/// <summary>
/// A translation that knows when it has changed. This is what makes runtime culture switching work
/// in a binding-based UI: the view binds to <see cref="Value"/> once, and the string itself tells the
/// UI to re-read it after the culture changes.
/// </summary>
public interface ILocalizedString : INotifyPropertyChanged
{
    /// <summary>Gets the resource key this instance observes.</summary>
    string Key { get; }

    /// <summary>Gets the translation of <see cref="Key"/> in the currently selected culture.</summary>
    string Value { get; }
}
