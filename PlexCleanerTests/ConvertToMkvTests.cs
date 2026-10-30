using System.Text.Json.Nodes;
using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public sealed class ConvertToMkvTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly ToolsOptions1 _toolsOptions = Program.Config.ToolsOptions;
    private readonly bool _useSidecarFiles = Program.Config.ProcessOptions.UseSidecarFiles;
    private readonly MediaToolInfo _ffProbeInfo = Tools.FfProbe.Info;
    private readonly MediaToolInfo _mkvMergeInfo = Tools.MkvMerge.Info;
    private readonly MediaToolInfo _mediaInfoInfo = Tools.MediaInfo.Info;

    public ConvertToMkvTests()
    {
        _ = Directory.CreateDirectory(_directory);
        Program.Config.ProcessOptions.UseSidecarFiles = true;
        Program.Config.ToolsOptions = new ToolsOptions1
        {
            UseSystem = false,
            RootRelative = false,
            RootPath = _directory,
        };
    }

    public void Dispose()
    {
        Program.Config.ToolsOptions = _toolsOptions;
        Tools.FfProbe.Info = _ffProbeInfo;
        Tools.MkvMerge.Info = _mkvMergeInfo;
        Tools.MediaInfo.Info = _mediaInfoInfo;
        Program.Config.ProcessOptions.UseSidecarFiles = _useSidecarFiles;
        Directory.Delete(_directory, true);
    }

    private static void WriteScript(string fileName, string body)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(fileName)!);
        File.WriteAllText(fileName, "#!/bin/sh\n" + body);
        File.SetUnixFileMode(
            fileName,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        );
    }

    // The sample sidecar supplies the media props, so only the conversion needs tools
    private ProcessFile CopySample()
    {
        Tools.FfProbe.Info = new MediaToolInfo(Tools.FfProbe) { Version = "8.0.1" };
        Tools.MkvMerge.Info = new MediaToolInfo(Tools.MkvMerge) { Version = "96.0" };
        Tools.MediaInfo.Info = new MediaToolInfo(Tools.MediaInfo) { Version = "25.10" };

        string sampleDirectory = PlexCleanerFixture.GetSamplesAbsoluteDirectory();
        string mediaFile = Path.Combine(_directory, "name.mkv");
        File.Copy(Path.Combine(sampleDirectory, "Sidecar.v5.mkv"), mediaFile);
        JsonNode sidecar = JsonNode.Parse(
            File.ReadAllText(Path.Combine(sampleDirectory, "Sidecar.v5.PlexCleaner"))
        )!;
        sidecar["State"] = (int)SidecarFile.StatesType.None;
        File.WriteAllText(Path.Combine(_directory, "name.PlexCleaner"), sidecar.ToJsonString());

        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();
        return processFile;
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, false)]
    public void ConvertToMkv_ReplacesInputOnlyAfterLanguageRestore(int propEditExit, bool success)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        ProcessFile processFile = CopySample();
        string mediaFile = processFile.FileInfo.FullName;
        byte[] original = File.ReadAllBytes(mediaFile);

        // Each fake writes its tool name to its temp output, MkvPropEdit logs the file it edits
        WriteScript(
            Tools.FfMpeg.GetToolPath(),
            "for a; do case $a in *.tmp1) echo ffmpeg > \"$a\" ;; esac; done\n"
        );
        WriteScript(
            Tools.MkvMerge.GetToolPath(),
            "for a; do a=${a#*=}; case $a in *.tmp4) echo mkvmerge > \"$a\" ;; esac; done\n"
        );
        string mkvPropEdit = Tools.MkvPropEdit.GetToolPath();
        WriteScript(mkvPropEdit, $"echo \"$@\" > \"$0.log\"\nexit {propEditExit}\n");

        bool converted = PlexCleaner.Convert.ConvertToMkv(
            mediaFile,
            new SelectMediaProps(processFile.FfProbeProps, true),
            processFile.MkvMergeProps,
            out string outputName
        );

        _ = converted.Should().Be(success);
        _ = outputName.Should().Be(mediaFile);
        _ = File.ReadAllText(mkvPropEdit + ".log").Should().Contain("name.tmp1");
        if (success)
        {
            _ = File.ReadAllText(mediaFile).Trim().Should().Be("mkvmerge");
        }
        else
        {
            _ = File.ReadAllBytes(mediaFile).Should().Equal(original);
        }
        _ = Directory.GetFiles(_directory, "name.tmp*").Should().BeEmpty();
    }
}
