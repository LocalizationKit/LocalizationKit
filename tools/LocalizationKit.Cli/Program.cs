using System.Globalization;
using System.Text;
using LocalizationKit.Cli;
using LocalizationKit.Tooling;

return Run(args);

static int Run(string[] args)
{
    CommandLine command = CommandLine.Parse(args);

    if (command.Command is null or "help" || command.HasFlag("help"))
    {
        PrintUsage();
        return command.Command is null && !command.HasFlag("help") ? 2 : 0;
    }

    try
    {
        return command.Command switch
        {
            "add-culture" => AddCulture(command),
            "sync-ide" => SyncIde(command),
            "validate" => Validate(command),
            "export" => Export(command),
            "import" => Import(command),
            "info" => Info(command),
            _ => Unknown(command.Command),
        };
    }
    catch (CultureNotFoundException ex)
    {
        Error(ex.Message);
        return 1;
    }
    catch (FileNotFoundException ex)
    {
        Error(ex.Message);
        return 1;
    }
    catch (InvalidOperationException ex)
    {
        Error(ex.Message);
        return 1;
    }
    catch (IOException ex)
    {
        Error(ex.Message);
        return 1;
    }
}

static ProjectContext Discover(CommandLine command) => ProjectContext.Discover(
    Directory.GetCurrentDirectory(),
    command.Value("project"),
    command.Value("resources-path"),
    command.Value("file-name"));

static int AddCulture(CommandLine command)
{
    IReadOnlyList<string> unknown = command.UnknownOptions(
        "project", "resources-path", "file-name", "empty", "prune", "dry-run");

    if (unknown.Count > 0)
    {
        return UnknownOptions(unknown);
    }

    if (command.Positional.Count != 1)
    {
        Error("add-culture takes exactly one culture, for example: localizationkit add-culture it-IT");
        return 2;
    }

    ProjectContext project = Discover(command);
    AddCultureResult result = CultureScaffolder.Plan(
        project,
        command.Positional[0],
        seedFromDefault: !command.HasFlag("empty"),
        prune: command.HasFlag("prune"));

    Console.WriteLine($"Project: {project.ProjectFile}");
    return Report(result.Changes, result.Notes, command.HasFlag("dry-run"));
}

static int SyncIde(CommandLine command)
{
    IReadOnlyList<string> unknown = command.UnknownOptions(
        "project", "resources-path", "file-name", "prune", "dry-run");

    if (unknown.Count > 0)
    {
        return UnknownOptions(unknown);
    }

    ProjectContext project = Discover(command);
    IdeSyncResult result = IdeSync.Plan(project, command.HasFlag("prune"));

    Console.WriteLine($"Project: {project.ProjectFile}");
    Console.WriteLine($"Cultures: {(project.Cultures.Count == 0 ? "(none)" : string.Join(", ", project.Cultures))}");
    return Report(result.Changes, result.Notes, command.HasFlag("dry-run"));
}

static int Validate(CommandLine command)
{
    IReadOnlyList<string> unknown = command.UnknownOptions(
        "project", "resources-path", "file-name", "warnings-as-errors", "format", "unused");

    if (unknown.Count > 0)
    {
        return UnknownOptions(unknown);
    }

    string format = command.Value("format") ?? "text";
    if (format is not ("text" or "msbuild"))
    {
        Error($"Unknown format '{format}'. Use 'text' or 'msbuild'.");
        return 2;
    }

    ProjectContext project = Discover(command);
    List<ValidationMessage> messages = [.. ResourceValidator.Validate(project)];

    if (command.HasFlag("unused"))
    {
        // Opt-in: it reads every source file in the project, which is not something to do on each
        // build, and what it finds is a list to read rather than a verdict.
        messages.AddRange(UnusedKeyScanner.Scan(project));
    }

    if (format == "msbuild")
    {
        // MSBuild re-logs each canonical line as a real error or warning and fails the build on its
        // own, so the exit code carries no message here: 1 only tells the target not to write its
        // up-to-date stamp, which would otherwise let the next build skip a project that still has
        // broken resources. Anything above 1 means the tool itself could not run.
        bool warningsAsErrors = command.HasFlag("warnings-as-errors");

        foreach (string line in ValidationReport.ToMsBuildLines(messages, warningsAsErrors))
        {
            Console.WriteLine(line);
        }

        return messages.Any(m => m.Severity == ValidationSeverity.Error
            || (warningsAsErrors && m.Severity == ValidationSeverity.Warning))
            ? 1
            : 0;
    }

    Console.WriteLine($"Project: {project.ProjectFile}");

    foreach (ValidationMessage message in messages)
    {
        string line = message.Line is int number ? $":{number}" : string.Empty;
        string location = message.File is null ? string.Empty : $" ({Relative(project, message.File)}{line})";
        Console.WriteLine($"  {message.Severity.ToString().ToLowerInvariant(),-7} {message.Code}  {message.Text}{location}");
    }

    int errors = messages.Count(m => m.Severity == ValidationSeverity.Error);
    int warnings = messages.Count(m => m.Severity == ValidationSeverity.Warning);

    Console.WriteLine(messages.Count == 0
        ? "  Nothing to report."
        : $"  {errors} error(s), {warnings} warning(s).");

    return errors > 0 || (warnings > 0 && command.HasFlag("warnings-as-errors")) ? 1 : 0;
}

static int Export(CommandLine command)
{
    IReadOnlyList<string> unknown = command.UnknownOptions(
        "project", "resources-path", "file-name", "culture", "format", "out");

    if (unknown.Count > 0)
    {
        return UnknownOptions(unknown);
    }

    if (command.Value("culture") is not { Length: > 0 } culture)
    {
        Error("export needs a culture, for example: localizationkit export --culture it-IT");
        return 2;
    }

    if (!TryReadFormat(command, command.Value("out"), out TransferFormat format))
    {
        return 2;
    }

    ProjectContext project = Discover(command);
    string text = TranslationTransfer.Export(project, culture, format);

    if (command.Value("out") is { Length: > 0 } path)
    {
        // A BOM would be wrong in both formats: gettext tools read PO as plain UTF-8, and an XML
        // declaration already says what the encoding is.
        File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Console.WriteLine($"Wrote {path}");
        return 0;
    }

    Console.Out.Write(text);
    return 0;
}

static int Import(CommandLine command)
{
    IReadOnlyList<string> unknown = command.UnknownOptions(
        "project", "resources-path", "file-name", "culture", "format", "dry-run");

    if (unknown.Count > 0)
    {
        return UnknownOptions(unknown);
    }

    if (command.Positional.Count != 1)
    {
        Error("import takes exactly one file, for example: localizationkit import it-IT.po --culture it-IT");
        return 2;
    }

    string file = command.Positional[0];

    if (!File.Exists(file))
    {
        Error($"There is no file at {file}.");
        return 1;
    }

    // The culture is normally in the file name the translator sent back.
    string culture = command.Value("culture")
        ?? Path.GetFileNameWithoutExtension(file);

    if (culture.Length == 0)
    {
        Error("import needs a culture, for example: localizationkit import translated.po --culture it-IT");
        return 2;
    }

    if (!TryReadFormat(command, file, out TransferFormat format))
    {
        return 2;
    }

    ProjectContext project = Discover(command);

    ImportResult result;
    try
    {
        result = TranslationTransfer.Import(project, culture, File.ReadAllText(file), format);
    }
    catch (FormatException exception)
    {
        Error(exception.Message);
        return 1;
    }

    Console.WriteLine($"Project: {project.ProjectFile}");
    Console.WriteLine($"Culture: {culture}");

    return Report(result.Changes, result.Notes, command.HasFlag("dry-run"));
}

static bool TryReadFormat(CommandLine command, string? path, out TransferFormat format)
{
    string? asked = command.Value("format");

    if (asked is null && path is { Length: > 0 })
    {
        // The extension says which format it is, so --format is only needed when writing to stdout.
        asked = Path.GetExtension(path).TrimStart('.').ToLowerInvariant() switch
        {
            "po" or "pot" => "po",
            "xlf" or "xliff" => "xliff",
            _ => null,
        };
    }

    switch (asked)
    {
        case null:
        case "po":
            format = TransferFormat.Po;
            return true;

        case "xliff":
        case "xlf":
            format = TransferFormat.Xliff;
            return true;

        default:
            Error($"Unknown format '{asked}'. Use 'po' or 'xliff'.");
            format = TransferFormat.Po;
            return false;
    }
}

static int Info(CommandLine command)
{
    ProjectContext project = Discover(command);

    Console.WriteLine($"Project            {project.ProjectFile}");
    Console.WriteLine($"Solution directory {project.SolutionDirectory ?? "(not found)"}");
    Console.WriteLine($"Resources          {project.ResourcesDirectory}");
    Console.WriteLine($"Resource file      {project.ResourceFileName}");
    Console.WriteLine($"DefaultLanguage    {project.DefaultLanguage ?? "(unset)"}");
    Console.WriteLine($"Declared cultures  {Join(project.DeclaredCultures)}");
    Console.WriteLine($"Culture folders    {Join(project.CultureFolders)}");
    Console.WriteLine($"User file          {project.CsprojUserFile}{(File.Exists(project.CsprojUserFile) ? string.Empty : "  (does not exist)")}");
    Console.WriteLine($"Resource Explorer  {Join(project.ResourceExplorerSettingsFiles)}");

    return 0;

    static string Join(IReadOnlyList<string> values) => values.Count == 0 ? "(none)" : string.Join(", ", values);
}

static int Report(IReadOnlyList<FileChange> changes, IReadOnlyList<string> notes, bool dryRun)
{
    if (changes.Count == 0)
    {
        Console.WriteLine("Everything is already in sync.");
    }

    foreach (FileChange change in changes)
    {
        string verb = change.Kind == FileChangeKind.Create ? "create" : "update";
        Console.WriteLine($"  {(dryRun ? "would " + verb : verb),-6}  {change.Path}");
        Console.WriteLine($"            {change.Description}");

        if (!dryRun)
        {
            change.Apply();
        }
    }

    foreach (string note in notes)
    {
        Console.WriteLine($"  note    {note}");
    }

    return 0;
}

static string Relative(ProjectContext project, string path)
{
    string root = project.SolutionDirectory ?? project.ProjectDirectory;
    return path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
        ? path[(root.Length + 1)..]
        : path;
}

static int UnknownOptions(IReadOnlyList<string> unknown)
{
    Error($"Unknown option(s): {string.Join(", ", unknown.Select(o => "--" + o))}");
    return 2;
}

static int Unknown(string command)
{
    Error($"Unknown command '{command}'.");
    PrintUsage();
    return 2;
}

static void Error(string message)
{
    Console.Error.WriteLine($"error: {message}");
}

static void PrintUsage()
{
    Console.WriteLine(
        """
        localizationkit — keep .resw resources, project items and local Visual Studio state in sync.

        Usage:
          localizationkit add-culture <culture> [options]   scaffold a language end to end
          localizationkit sync-ide [options]                reconcile the gitignored IDE files
          localizationkit validate [options]                check keys, messages and coverage
          localizationkit export --culture it-IT [options]   write a PO or XLIFF file for a translator
          localizationkit import <file> [options]            take a translated file back in
          localizationkit info [options]                    show what was discovered

        Options:
          --project <path>        the .csproj to act on (discovered when omitted)
          --resources-path <dir>  folder holding culture subfolders (default: Strings)
          --file-name <name>      resource file name (default: Resources.resw)
          --dry-run               print what would change, write nothing
          --culture <name>        export and import: which culture is being translated
          --format <po|xliff>     export and import: inferred from the file name when omitted
          --out <path>            export only: write to a file instead of standard output
          --unused                validate only: also list keys the source never mentions
          --format <text|msbuild>  validate only: msbuild emits diagnostics MSBuild re-logs
          --warnings-as-errors    validate only: treat warnings as errors
          --prune                 also remove Resource Explorer columns for cultures that are gone
          --empty                 add-culture: do not copy keys from the default culture
          --warnings-as-errors    validate: exit non-zero on warnings too

        Exit codes: 0 success, 1 failure, 2 usage error.
        """);
}
