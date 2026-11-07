using AwesomeAssertions;
using CliWrap;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class SevenZipToolTests
{
    [Fact]
    public void Builder_Orders_Global_Input_And_Output_Options()
    {
        Command command = SevenZip
            .Builder.Create("7za")
            .GlobalOptions(options => options.Add("x").Add("-y"))
            .InputOptions(options => options.InputFile("in.7z"))
            .OutputOptions(options => options.OutputFolder("out"))
            .Build();

        _ = command.TargetFilePath.Should().Be("7za");
        _ = command.Arguments.Should().Be("x -y \"in.7z\" -o\"out\"");
    }

    [Fact]
    public void Builder_Skips_Blank_Options()
    {
        Command command = SevenZip
            .Builder.Create("7za")
            .GlobalOptions(options => options.Add(" "))
            .InputOptions(options => options.Add(string.Empty))
            .OutputOptions(options => options.Add(" "))
            .Build();

        _ = command.Arguments.Should().BeEmpty();
    }

    [Fact]
    public void Builder_Escapes_When_Asked()
    {
        Command command = SevenZip
            .Builder.Create("7za")
            .GlobalOptions(options => options.Add("a b", true))
            .InputOptions(options => options.Add("c d", true))
            .OutputOptions(options => options.Add("e f", true))
            .Build();

        _ = command.Arguments.Should().Be("\"a b\" \"c d\" \"e f\"");
    }

    [Fact]
    public void Version_Builds_Command_Without_Arguments()
    {
        Command command = SevenZip.Builder.Version("7za");

        _ = command.TargetFilePath.Should().Be("7za");
        _ = command.Arguments.Should().BeEmpty();
    }

    [Fact]
    public void GetVersion_Reads_Version_And_File_Info()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, new byte[16]);
            MediaToolInfo toolInfo = new(new SevenZip.Tool()) { FileName = path };
            string text =
                "7-Zip 24.08 (x64) : Copyright (c) 1999-2024 Igor Pavlov : 2024-08-11"
                + Environment.NewLine
                + Environment.NewLine;

            _ = SevenZip.Tool.GetVersion(text, toolInfo).Should().BeTrue();

            _ = toolInfo.Version.Should().Be("24.08");
            _ = toolInfo.Size.Should().Be(16);
            _ = toolInfo.ModifiedTime.Should().Be(File.GetLastWriteTimeUtc(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
