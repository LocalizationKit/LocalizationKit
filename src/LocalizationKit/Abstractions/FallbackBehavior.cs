namespace LocalizationKit;

/// <summary>What a lookup returns when a key is missing from every culture in the fallback chain.</summary>
public enum FallbackBehavior
{
    /// <summary>Return the key itself, which keeps the UI readable and makes the gap obvious.</summary>
    ReturnKey = 0,

    /// <summary>Return an empty string, for places where a visible key would be worse than nothing.</summary>
    ReturnEmpty = 1,

    /// <summary>Throw <see cref="MissingResourceException"/>. Appropriate for test runs and CI.</summary>
    Throw = 2,
}
