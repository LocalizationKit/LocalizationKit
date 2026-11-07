using System.Text;

namespace LocalizationKit.Tooling;

/// <summary>
/// A pending edit to one file. Planning and applying are separate so that <c>--dry-run</c> is the
/// same code path as a real run, minus the write.
/// </summary>
/// <param name="Path">The absolute path of the file.</param>
/// <param name="Description">A one-line summary suitable for printing.</param>
/// <param name="NewContent">The complete new content of the file.</param>
/// <param name="Kind">Whether the file is being created or edited.</param>
/// <param name="WriteByteOrderMark">Whether the file is written with a UTF-8 BOM, as Visual Studio writes it.</param>
public sealed record FileChange(
    string Path,
    string Description,
    string NewContent,
    FileChangeKind Kind,
    bool WriteByteOrderMark)
{
    /// <summary>Writes the change to disk, creating the directory if needed.</summary>
    public void Apply()
    {
        string? directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(Path, NewContent, new UTF8Encoding(WriteByteOrderMark));
    }
}

/// <summary>What a <see cref="FileChange"/> does to the file.</summary>
public enum FileChangeKind
{
    /// <summary>The file does not exist yet.</summary>
    Create = 0,

    /// <summary>The file exists and is being edited in place.</summary>
    Update = 1,
}
