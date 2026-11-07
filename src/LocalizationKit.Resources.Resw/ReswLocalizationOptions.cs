using System.Reflection;

namespace LocalizationKit;

/// <summary>Configuration for the <c>.resw</c> resource provider.</summary>
public sealed class ReswLocalizationOptions
{
    /// <summary>
    /// Gets or sets the folder holding one subfolder per culture, relative to each root directory.
    /// <c>Strings</c> is the WinUI convention.
    /// </summary>
    public string ResourcesPath { get; set; } = "Strings";

    /// <summary>Gets or sets the file name inside each culture folder.</summary>
    public string FileName { get; set; } = "Resources.resw";

    /// <summary>
    /// Gets or sets the directory the resource folder is resolved against. Defaults to the
    /// application's base directory, where <c>Content</c> items land at build time.
    /// </summary>
    public string? RootDirectory { get; set; }

    /// <summary>
    /// Gets the directories searched before <see cref="RootDirectory"/>, so a writable location such
    /// as the app data folder can override shipped translations. Useful for translations delivered
    /// after release.
    /// </summary>
    public IList<string> OverrideDirectories { get; } = [];

    /// <summary>
    /// Gets the assemblies whose embedded resources are searched when no file is found on disk. An
    /// embedded <c>.resw</c> is matched on the tail of its manifest name, so the assembly's root
    /// namespace does not have to be configured.
    /// </summary>
    public IList<Assembly> EmbeddedResourceAssemblies { get; } = [];
}
