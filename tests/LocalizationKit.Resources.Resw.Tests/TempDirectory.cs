namespace LocalizationKit.Resources.Resw.Tests;

/// <summary>A scratch directory that deletes itself.</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "localizationkit-tests",
            Guid.NewGuid().ToString("n"));

        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public string WriteResw(string culture, params ReswEntry[] entries)
    {
        string file = Combine("Strings", culture, "Resources.resw");
        ReswFile.Write(file, entries);
        return file;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A locked file in a scratch directory is not worth failing a test over.
        }
    }
}
