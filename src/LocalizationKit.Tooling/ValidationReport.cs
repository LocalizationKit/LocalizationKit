namespace LocalizationKit.Tooling;

/// <summary>Formats validation findings for the places that read them.</summary>
/// <remarks>
/// <para>
/// MSBuild recognizes a diagnostic written in its canonical form anywhere in a task's output and
/// re-logs it as a real error or warning, which is what puts a finding in Visual Studio's Error List
/// with a code and a double-click that opens the <c>.resw</c> at the offending key:
/// </para>
/// <code>
/// C:\App\Strings\it-IT\Resources.resw(42): error LK009: [it-IT] 'Cart.Items' is not a valid message: ...
/// </code>
/// <para>
/// This lives here rather than in the CLI so the exact shape of that line is covered by tests. Get it
/// subtly wrong and nothing fails: the line is simply printed as ordinary build output and the finding
/// disappears into the log.
/// </para>
/// </remarks>
public static class ValidationReport
{
    /// <summary>The origin used for a finding that concerns no particular file.</summary>
    public const string ToolOrigin = "localizationkit";

    /// <summary>Formats findings as MSBuild-canonical diagnostics.</summary>
    /// <param name="messages">The findings to format.</param>
    /// <param name="warningsAsErrors">Whether warnings should be emitted as errors.</param>
    /// <returns>One line per finding, in the order given.</returns>
    public static IEnumerable<string> ToMsBuildLines(
        IEnumerable<ValidationMessage> messages,
        bool warningsAsErrors = false)
    {
        ArgumentNullException.ThrowIfNull(messages);

        foreach (ValidationMessage message in messages)
        {
            yield return ToMsBuildLine(message, warningsAsErrors);
        }
    }

    /// <summary>Formats one finding as an MSBuild-canonical diagnostic.</summary>
    /// <param name="message">The finding.</param>
    /// <param name="warningsAsErrors">Whether a warning should be emitted as an error.</param>
    /// <returns>The line to print.</returns>
    public static string ToMsBuildLine(ValidationMessage message, bool warningsAsErrors = false)
    {
        ArgumentNullException.ThrowIfNull(message);

        // A diagnostic has to be one line: a line break would leave the remainder as unparsed output.
        string text = Flatten(message.Text);

        if (message.Severity == ValidationSeverity.Info)
        {
            // There is no "info" category, and borrowing "warning" would make a note fail a build
            // configured to treat warnings as errors. A note stays ordinary output.
            return $"{ToolOrigin}: {message.Code}: {text}";
        }

        string category = message.Severity == ValidationSeverity.Error || warningsAsErrors
            ? "error"
            : "warning";

        string origin = message.File is { Length: > 0 } file
            ? message.Line is int line && line > 0 ? $"{file}({line})" : file
            : ToolOrigin;

        return $"{origin} : {category} {message.Code}: {text}";
    }

    private static string Flatten(string text)
    {
        return text.ReplaceLineEndings(" ").Trim();
    }
}
