namespace LocalizationKit.Tooling;

/// <summary>One string on its way to or from a translator.</summary>
/// <param name="Key">The resource name, which is what makes the unit identifiable in either format.</param>
/// <param name="Source">The text in the culture the keys were authored in.</param>
/// <param name="Target">The translation, empty when there is not one yet.</param>
/// <param name="Comment">The note for the translator, when the resource carries one.</param>
public sealed record TransferUnit(string Key, string Source, string Target, string? Comment = null);

/// <summary>The formats a catalogue can be handed over in.</summary>
public enum TransferFormat
{
    /// <summary>GNU gettext PO, which almost every translation tool reads.</summary>
    Po,

    /// <summary>XLIFF 1.2, which the translation industry's own tools prefer.</summary>
    Xliff,
}
