using System.Text.RegularExpressions;

namespace LocalizationKit.Tooling.Tests;

/// <summary>
/// The shape of an MSBuild diagnostic, which is load-bearing: a line that does not match MSBuild's
/// canonical form is printed as ordinary build output and the finding is lost. Nothing fails loudly
/// when that happens, so it is asserted here against the same pattern MSBuild matches with.
/// </summary>
public sealed class ValidationReportTests
{
    /// <summary>
    /// A simplification of MSBuild's own canonical-error expression: an optional origin with an
    /// optional line, a category, a code, and the text.
    /// </summary>
    private static readonly Regex Canonical = new(
        @"^(?<file>.+?)(\((?<line>\d+)\))?\s:\s(?<category>error|warning)\s(?<code>[^:\s]+):\s(?<text>.+)$",
        RegexOptions.ExplicitCapture);

    [Fact]
    public void An_error_about_one_key_points_at_its_line()
    {
        string line = ValidationReport.ToMsBuildLine(new ValidationMessage(
            ValidationSeverity.Error,
            "LK009",
            "[it-IT] 'Cart.Items' is not a valid message.",
            @"C:\App\Strings\it-IT\Resources.resw",
            42));

        Match match = Canonical.Match(line);

        Assert.True(match.Success, line);
        Assert.Equal(@"C:\App\Strings\it-IT\Resources.resw", match.Groups["file"].Value);
        Assert.Equal("42", match.Groups["line"].Value);
        Assert.Equal("error", match.Groups["category"].Value);
        Assert.Equal("LK009", match.Groups["code"].Value);
    }

    [Fact]
    public void A_finding_about_a_whole_file_names_the_file_and_no_line()
    {
        string line = ValidationReport.ToMsBuildLine(new ValidationMessage(
            ValidationSeverity.Warning,
            "LK006",
            "[it-IT] 2 keys are missing: A, B",
            @"C:\App\Strings\it-IT\Resources.resw"));

        Match match = Canonical.Match(line);

        Assert.True(match.Success, line);
        Assert.Equal("warning", match.Groups["category"].Value);
        Assert.Empty(match.Groups["line"].Value);
    }

    [Fact]
    public void A_finding_about_no_file_is_attributed_to_the_tool()
    {
        string line = ValidationReport.ToMsBuildLine(new ValidationMessage(
            ValidationSeverity.Error,
            "LK001",
            "No cultures found."));

        Assert.True(Canonical.IsMatch(line), line);
        Assert.StartsWith(ValidationReport.ToolOrigin, line, StringComparison.Ordinal);
    }

    [Fact]
    public void Warnings_as_errors_changes_the_category_and_nothing_else()
    {
        ValidationMessage message = new(
            ValidationSeverity.Warning,
            "LK008",
            "[it-IT] 1 untranslated key: Common.Save",
            @"C:\App\Strings\it-IT\Resources.resw");

        Assert.Contains(": warning LK008:", ValidationReport.ToMsBuildLine(message), StringComparison.Ordinal);
        Assert.Contains(
            ": error LK008:",
            ValidationReport.ToMsBuildLine(message, warningsAsErrors: true),
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_note_stays_ordinary_output()
    {
        // Borrowing "warning" for a note would fail a build that treats warnings as errors over
        // something that was never a problem.
        string line = ValidationReport.ToMsBuildLine(new ValidationMessage(
            ValidationSeverity.Info,
            "LK900",
            "Visual Studio is running; the sync may be reverted."));

        Assert.False(Canonical.IsMatch(line), line);
        Assert.Contains("LK900", line, StringComparison.Ordinal);
    }

    [Fact]
    public void A_message_that_spans_lines_is_flattened()
    {
        // Only the first line of a multi-line diagnostic would be parsed; the rest would arrive as
        // unattributed build output.
        string line = ValidationReport.ToMsBuildLine(new ValidationMessage(
            ValidationSeverity.Error,
            "LK005",
            "could not be parsed:\r\nunexpected end of file\nat line 3",
            @"C:\App\Strings\it-IT\Resources.resw"));

        Assert.DoesNotContain('\n', line);
        Assert.DoesNotContain('\r', line);
        Assert.True(Canonical.IsMatch(line), line);
    }

    [Fact]
    public void Every_finding_is_formatted_in_order()
    {
        ValidationMessage[] messages =
        [
            new(ValidationSeverity.Error, "LK009", "first"),
            new(ValidationSeverity.Warning, "LK006", "second"),
        ];

        IReadOnlyList<string> lines = [.. ValidationReport.ToMsBuildLines(messages)];

        Assert.Equal(2, lines.Count);
        Assert.Contains("LK009", lines[0], StringComparison.Ordinal);
        Assert.Contains("LK006", lines[1], StringComparison.Ordinal);
    }
}
