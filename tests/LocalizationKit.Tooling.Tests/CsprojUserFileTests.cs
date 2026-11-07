using System.Text;

namespace LocalizationKit.Tooling.Tests;

public sealed class CsprojUserFileTests
{
    [Fact]
    public void Creates_the_file_when_visual_studio_never_has()
    {
        using SampleSolution solution = new();

        FileChange? change = CsprojUserFile.Plan(solution.UserFile, ["Strings\\it-IT\\Resources.resw"]);

        Assert.NotNull(change);
        Assert.Equal(FileChangeKind.Create, change.Kind);
        Assert.Equal(
            SampleSolution.Crlf(
                """
                <?xml version="1.0" encoding="utf-8"?>
                <Project ToolsVersion="Current" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                  <PropertyGroup />
                  <ItemGroup>
                    <PRIResource Update="Strings\it-IT\Resources.resw">
                      <SubType>Designer</SubType>
                    </PRIResource>
                  </ItemGroup>
                </Project>
                """),
            change.NewContent);
    }

    [Fact]
    public void Adds_an_entry_without_disturbing_what_visual_studio_put_there()
    {
        using SampleSolution solution = new();
        solution.WithUserFile();

        FileChange? change = CsprojUserFile.Plan(
            solution.UserFile,
            ["Strings\\it-IT\\Resources.resw", "Strings\\de-DE\\Resources.resw"]);

        Assert.NotNull(change);
        Assert.Equal(FileChangeKind.Update, change.Kind);
        Assert.Equal(
            SampleSolution.Crlf(
                """
                <?xml version="1.0" encoding="utf-8"?>
                <Project ToolsVersion="Current" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                  <PropertyGroup />
                  <ItemGroup>
                    <Page Update="Views\LoginPage.xaml">
                      <SubType>Designer</SubType>
                    </Page>
                  </ItemGroup>
                  <ItemGroup>
                    <PRIResource Update="Strings\it-IT\Resources.resw">
                      <SubType>Designer</SubType>
                    </PRIResource>
                  </ItemGroup>
                  <ItemGroup>
                    <PRIResource Update="Strings\de-DE\Resources.resw">
                      <SubType>Designer</SubType>
                    </PRIResource>
                  </ItemGroup>
                </Project>
                """),
            change.NewContent);
    }

    [Fact]
    public void Is_idempotent()
    {
        using SampleSolution solution = new();
        solution.WithUserFile();

        CsprojUserFile.Plan(solution.UserFile, ["Strings\\de-DE\\Resources.resw"])!.Apply();

        Assert.Null(CsprojUserFile.Plan(solution.UserFile, ["Strings\\de-DE\\Resources.resw"]));
        Assert.Null(CsprojUserFile.Plan(solution.UserFile, ["Strings\\it-IT\\Resources.resw"]));
    }

    [Fact]
    public void Applying_a_change_keeps_the_byte_order_mark_and_line_endings()
    {
        using SampleSolution solution = new();
        solution.WithUserFile();

        CsprojUserFile.Plan(solution.UserFile, ["Strings\\de-DE\\Resources.resw"])!.Apply();

        byte[] bytes = File.ReadAllBytes(solution.UserFile);
        string text = Encoding.UTF8.GetString(bytes);

        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.EndsWith("</Project>", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', text.Replace("\r\n", string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public void An_entry_a_developer_changed_is_left_alone()
    {
        using SampleSolution solution = new();
        solution.WithUserFile(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="Current" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup />
              <ItemGroup>
                <PRIResource Update="Strings\it-IT\Resources.resw">
                  <SubType>Code</SubType>
                </PRIResource>
              </ItemGroup>
            </Project>
            """);

        Assert.Null(CsprojUserFile.Plan(solution.UserFile, ["Strings\\it-IT\\Resources.resw"]));
    }

    [Fact]
    public void Paths_are_written_the_way_visual_studio_writes_them()
    {
        using SampleSolution solution = new();

        FileChange? change = CsprojUserFile.Plan(solution.UserFile, ["Strings/it-IT/Resources.resw"]);

        Assert.NotNull(change);
        Assert.Contains(@"Update=""Strings\it-IT\Resources.resw""", change.NewContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_to_add_is_not_a_change()
    {
        using SampleSolution solution = new();

        Assert.Null(CsprojUserFile.Plan(solution.UserFile, []));
    }
}
