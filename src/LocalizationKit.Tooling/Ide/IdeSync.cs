using System.Diagnostics;

namespace LocalizationKit.Tooling;

/// <summary>The planned result of a sync: what to write, and what the developer should know.</summary>
/// <param name="Changes">The files to write.</param>
/// <param name="Notes">Lines worth printing even when nothing changed.</param>
public sealed record IdeSyncResult(IReadOnlyList<FileChange> Changes, IReadOnlyList<string> Notes)
{
    /// <summary>Gets a value indicating whether anything needs writing.</summary>
    public bool HasChanges => Changes.Count > 0;

    /// <summary>Writes every planned change.</summary>
    public void Apply()
    {
        foreach (FileChange change in Changes)
        {
            change.Apply();
        }
    }
}

/// <summary>
/// Reconciles the developer-local files that describe a project's resources to Visual Studio: the
/// <c>.csproj.user</c> designer entries and the Resource Explorer column layout.
/// </summary>
/// <remarks>
/// Both files are gitignored, so they do not arrive with a clone and drift as soon as a language is
/// added. Running this is how a fresh checkout catches up.
/// </remarks>
public static class IdeSync
{
    /// <summary>Plans the changes needed to make the local IDE files match the project.</summary>
    /// <param name="project">The project to reconcile.</param>
    /// <param name="prune">Whether to drop Resource Explorer columns for cultures that are gone.</param>
    /// <returns>The planned changes and any notes for the developer.</returns>
    public static IdeSyncResult Plan(ProjectContext project, bool prune = false)
    {
        ArgumentNullException.ThrowIfNull(project);
        return Plan(project, project.Cultures, prune, pendingResourceFiles: null);
    }

    /// <summary>Plans the changes for an explicit culture list, including cultures not yet on disk.</summary>
    /// <param name="project">The project to reconcile.</param>
    /// <param name="cultures">The cultures to account for.</param>
    /// <param name="prune">Whether to drop Resource Explorer columns for cultures that are gone.</param>
    /// <param name="pendingResourceFiles">
    /// Resource files that do not exist yet but are about to be written in the same run, so a culture
    /// being added right now still gets its designer entry.
    /// </param>
    /// <returns>The planned changes and any notes for the developer.</returns>
    public static IdeSyncResult Plan(
        ProjectContext project,
        IReadOnlyList<string> cultures,
        bool prune,
        IEnumerable<string>? pendingResourceFiles)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(cultures);

        List<FileChange> changes = [];
        List<string> notes = [];
        HashSet<string> pending = new(pendingResourceFiles ?? [], StringComparer.OrdinalIgnoreCase);

        bool WillExist(string culture)
        {
            string path = project.ResourceFileFor(culture);
            return File.Exists(path) || pending.Contains(path);
        }

        if (cultures.Count == 0)
        {
            notes.Add(
                $"No cultures found: set {ProjectContext.CulturesProperty} in the project, or add a culture folder "
                + $"under '{project.ResourcesPath}'.");
            return new IdeSyncResult(changes, notes);
        }

        // Only reference files that exist: an entry for a missing file would show up as a broken item.
        string[] present = [.. cultures.Where(WillExist)];
        foreach (string missing in cultures.Where(c => !WillExist(c)))
        {
            notes.Add($"Culture '{missing}' is declared but has no '{project.ResourceFileName}'; run add-culture {missing}.");
        }

        if (present.Length > 0)
        {
            FileChange? userFile = CsprojUserFile.Plan(
                project.CsprojUserFile,
                present.Select(project.RelativeResourceFileFor));

            if (userFile is not null)
            {
                changes.Add(userFile);
            }
        }

        if (project.ResourceExplorerSettingsFiles.Count == 0)
        {
            notes.Add(
                "No Resource Explorer state found under '.vs'. Visual Studio writes it the first time the tool "
                + "window is opened; run this again afterwards.");
        }

        foreach (string settings in project.ResourceExplorerSettingsFiles)
        {
            FileChange? change = ResourceExplorerSettings.Plan(settings, cultures, prune);
            if (change is not null)
            {
                changes.Add(change);
            }
        }

        if (changes.Count > 0 && IsVisualStudioRunning())
        {
            notes.Add(
                "Visual Studio is running. It holds this state in memory and rewrites it when the solution closes, "
                + "which can revert these edits; re-run after closing it if the columns do not appear.");
        }

        if (!IsIgnoredByGit(project.CsprojUserFile))
        {
            notes.Add(
                $"'{Path.GetFileName(project.CsprojUserFile)}' is not ignored by git. It holds developer-local state "
                + "and is normally gitignored; check it is not about to be committed.");
        }

        return new IdeSyncResult(changes, notes);
    }

    private static bool IsVisualStudioRunning()
    {
        try
        {
            return Process.GetProcessesByName("devenv").Length > 0;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Asks git whether a path is ignored. Anything other than a clear "not ignored" answer, including
    /// no git at all, is treated as ignored, so the tool never nags on a guess.
    /// </summary>
    private static bool IsIgnoredByGit(string path)
    {
        try
        {
            ProcessStartInfo startInfo = new("git", ["check-ignore", "-q", "--no-index", path])
            {
                WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                return true;
            }

            if (!process.WaitForExit(2000))
            {
                return true;
            }

            // 0 = ignored, 1 = not ignored, 128 = not a repository.
            return process.ExitCode != 1;
        }
        catch (Exception)
        {
            return true;
        }
    }
}
