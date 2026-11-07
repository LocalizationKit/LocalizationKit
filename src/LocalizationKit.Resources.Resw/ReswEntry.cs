namespace LocalizationKit;

/// <summary>One string entry in a <c>.resw</c> file.</summary>
/// <param name="Name">The resource name, which is the lookup key.</param>
/// <param name="Value">The translated string, which may be an ICU message pattern.</param>
/// <param name="Comment">The translator note, or <see langword="null"/> when there is none.</param>
/// <param name="Line">
/// The 1-based line the entry starts on when it was read from a file, or <see langword="null"/> when
/// the entry was constructed in memory. Tooling reports findings against it so a build error points
/// at the key rather than at the file.
/// </param>
public sealed record ReswEntry(string Name, string Value, string? Comment = null, int? Line = null)
{
    /// <summary>Compares two entries by what they say, ignoring where they were read from.</summary>
    /// <param name="other">The entry to compare with.</param>
    /// <returns><see langword="true"/> when the name, value and comment are the same.</returns>
    /// <remarks>
    /// <see cref="Line"/> is provenance, not identity: the same entry in two culture files, or before
    /// and after a rewrite that moved it, is still the same entry. Leaving the generated comparison in
    /// place would have made a round trip through <c>ReswFile</c> unequal to what went into it.
    /// </remarks>
    public bool Equals(ReswEntry? other)
    {
        return other is not null
            && string.Equals(Name, other.Name, StringComparison.Ordinal)
            && string.Equals(Value, other.Value, StringComparison.Ordinal)
            && string.Equals(Comment, other.Comment, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Value, Comment);
    }
}
