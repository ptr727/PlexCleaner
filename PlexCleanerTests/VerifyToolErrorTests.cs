using System.Text.Json.Nodes;
using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public sealed class VerifyToolErrorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly ToolsOptions1 _toolsOptions = Program.Config.ToolsOptions;
    private readonly bool _verify = Program.Config.ProcessOptions.Verify;
    private readonly bool _useSidecarFiles = Program.Config.ProcessOptions.UseSidecarFiles;
    private readonly bool _autoRepair = Program.Config.VerifyOptions.AutoRepair;
    private readonly bool _deleteInvalidFiles = Program.Config.VerifyOptions.DeleteInvalidFiles;
    private readonly int _maximumBitrate = Program.Config.VerifyOptions.MaximumBitrate;
    private readonly MediaToolInfo _ffProbeInfo = Tools.FfProbe.Info;
    private readonly MediaToolInfo _mkvMergeInfo = Tools.MkvMerge.Info;
    private readonly MediaToolInfo _mediaInfoInfo = Tools.MediaInfo.Info;

    public VerifyToolErrorTests()
    {
        _ = Directory.CreateDirectory(_directory);
        Program.Config.ProcessOptions.Verify = true;
        Program.Config.ProcessOptions.UseSidecarFiles = true;
        Program.Config.VerifyOptions.AutoRepair = true;
        Program.Config.VerifyOptions.DeleteInvalidFiles = true;
        Program.Config.VerifyOptions.MaximumBitrate = 0;

        // An empty tools folder makes every tool fail to launch
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
        Program.Config.ProcessOptions.Verify = _verify;
        Program.Config.ProcessOptions.UseSidecarFiles = _useSidecarFiles;
        Program.Config.VerifyOptions.AutoRepair = _autoRepair;
        Program.Config.VerifyOptions.DeleteInvalidFiles = _deleteInvalidFiles;
        Program.Config.VerifyOptions.MaximumBitrate = _maximumBitrate;
        Directory.Delete(_directory, true);
    }

    private string CreateFile(string name)
    {
        string fileName = Path.Combine(_directory, name);
        File.WriteAllText(fileName, "");
        return fileName;
    }

    private static void WriteScript(string fileName, string body, bool executable)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(fileName)!);
        File.WriteAllText(fileName, "#!/bin/sh\n" + body);
        File.SetUnixFileMode(
            fileName,
            UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | (executable ? UnixFileMode.UserExecute : UnixFileMode.None)
        );
    }

    // The sample sidecar supplies the media props, so only the verify needs a tool
    private string CopyUnverifiedSample()
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
        return mediaFile;
    }

    [Fact]
    public void VerifyMedia_ToolDoesNotRun_IsToolError() =>
        _ = Tools.FfMpeg.VerifyMedia(CreateFile("name.mkv")).Should().Be(VerifyResult.ToolError);

    [Theory]
    [InlineData("KILL")]
    [InlineData("TERM")]
    [InlineData("PROF")]
    public void VerifyMedia_SignalExit_IsDecodeErrorAndRecordsSignal(string signal)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake FfMpeg is a shell script");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        WriteScript(Tools.FfMpeg.GetToolPath(), $"kill -{signal} $$\n", true);

        _ = Tools.FfMpeg.VerifyMedia(CreateFile("name.mkv")).Should().Be(VerifyResult.DecodeError);
        _ = ToolErrorScope.SignalCount.Should().Be(1);
        _ = ToolErrorScope.Count.Should().Be(0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(183)]
    [InlineData(234)]
    public void VerifyMedia_ErrorExit_RecordsNoSignal(int exitCode)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake FfMpeg is a shell script");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        WriteScript(Tools.FfMpeg.GetToolPath(), $"exit {exitCode}\n", true);

        _ = Tools.FfMpeg.VerifyMedia(CreateFile("name.mkv")).Should().Be(VerifyResult.DecodeError);
        _ = ToolErrorScope.SignalCount.Should().Be(0);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(128, false)]
    [InlineData(129, true)]
    [InlineData(137, true)]
    [InlineData(143, true)]
    [InlineData(153, true)]
    [InlineData(159, true)]
    [InlineData(160, false)]
    [InlineData(166, false)]
    [InlineData(183, false)]
    [InlineData(244, false)]
    [InlineData(255, true)]
    public void IsSignalExit_MatchesSignalExitCodes(int exitCode, bool isSignal)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Signal exit codes are not read on Windows");
        _ = FfMpeg.Tool.IsSignalExit(exitCode).Should().Be(isSignal);
    }

    [Theory]
    [InlineData("kill -9 $$", true, SidecarFile.StatesType.None, false)]
    [InlineData("kill -9 $$", false, SidecarFile.StatesType.None, false)]
    [InlineData(
        "exit 183",
        true,
        SidecarFile.StatesType.VerifyFailed | SidecarFile.StatesType.RepairFailed,
        true
    )]
    [InlineData("exit 183", false, SidecarFile.StatesType.VerifyFailed, true)]
    public void VerifyAndRepair_VerifyFails_SetsStateAndDeletesOnlyWithoutSignal(
        string firstVerify,
        bool autoRepair,
        SidecarFile.StatesType state,
        bool deleted
    )
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        Program.Config.VerifyOptions.AutoRepair = autoRepair;
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        string mediaFile = CopyUnverifiedSample();
        string sidecarFile = Path.ChangeExtension(mediaFile, ".PlexCleaner");
        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();

        WriteScript(Tools.MkvMerge.GetToolPath(), "exit 2\n", true);
        WriteScript(Tools.HandBrake.GetToolPath(), "exit 1\n", true);
        string ffMpeg = Tools.FfMpeg.GetToolPath();
        WriteScript(
            ffMpeg,
            $$"""
            step=$(($(cat "$0.step" 2>/dev/null || echo 0) + 1))
            echo $step > "$0.step"
            [ $step -eq 1 ] && {{firstVerify}}
            exit 1

            """,
            true
        );

        bool modified = false;
        _ = processFile.VerifyAndRepair(ref modified).Should().BeFalse();
        _ = ToolErrorScope.SignalCount.Should().Be(deleted ? 0 : 1);
        _ = ToolErrorScope.Count.Should().Be(0);
        _ = processFile.State.Should().Be(state);
        _ = processFile.DeleteFailedFile().Should().Be(deleted);

        _ = modified.Should().BeFalse();
        _ = File.Exists(mediaFile).Should().Be(!deleted);
        _ = File.Exists(sidecarFile).Should().Be(!deleted);
    }

    [Fact]
    public void VerifyAndRepair_TimestampReVerifySignalExit_KeepsStateAndFiles()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        string mediaFile = CopyUnverifiedSample();
        string sidecarFile = Path.ChangeExtension(mediaFile, ".PlexCleaner");
        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();

        WriteScript(Tools.FfProbe.GetToolPath(), "exit 1\n", true);
        WriteScript(Tools.MkvMerge.GetToolPath(), "for a; do out=$a; done\n: > \"$out\"\n", true);
        WriteScript(Tools.HandBrake.GetToolPath(), "exit 1\n", true);
        string ffMpeg = Tools.FfMpeg.GetToolPath();
        WriteScript(
            ffMpeg,
            """
            step=$(($(cat "$0.step" 2>/dev/null || echo 0) + 1))
            echo $step > "$0.step"
            case $step in
            1) echo "non monotonically increasing dts to muxer" >&2 && exit 0 ;;
            2) kill -9 $$ ;;
            esac
            exit 1

            """,
            true
        );

        bool modified = false;
        _ = processFile.VerifyAndRepair(ref modified).Should().BeFalse();
        _ = ToolErrorScope.SignalCount.Should().Be(1);
        _ = ToolErrorScope.Count.Should().Be(0);
        _ = processFile.State.Should().Be(SidecarFile.StatesType.None);
        _ = processFile.DeleteFailedFile().Should().BeFalse();

        _ = modified.Should().BeFalse();
        _ = File.Exists(mediaFile).Should().BeTrue();
        _ = File.Exists(sidecarFile).Should().BeTrue();
    }

    [Fact]
    public void VerifyAndRepair_ToolError_KeepsStateAndFiles()
    {
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        string mediaFile = CopyUnverifiedSample();
        string sidecarFile = Path.ChangeExtension(mediaFile, ".PlexCleaner");
        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();

        bool modified = false;
        _ = processFile.VerifyAndRepair(ref modified).Should().BeFalse();
        _ = processFile.State.Should().Be(SidecarFile.StatesType.None);
        _ = processFile.DeleteFailedFile().Should().BeFalse();

        _ = modified.Should().BeFalse();
        _ = File.Exists(mediaFile).Should().BeTrue();
        _ = File.Exists(sidecarFile).Should().BeTrue();
    }

    [Fact]
    public void RepairTimestamps_ToolError_KeepsFiles()
    {
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        string mediaFile = CreateFile("name.mkv");
        string sidecarFile = CreateFile("name.PlexCleaner");
        ProcessFile processFile = new(mediaFile);

        bool modified = false;
        _ = processFile.RepairTimestamps(ref modified).Should().BeFalse();
        _ = processFile.DeleteFailedFile().Should().BeFalse();

        _ = File.Exists(mediaFile).Should().BeTrue();
        _ = File.Exists(sidecarFile).Should().BeTrue();
    }

    [Fact]
    public void ProcessFile_MediaPropsToolError_KeepsFiles()
    {
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        string mediaFile = CreateFile("name.mkv");

        _ = Process
            .ProcessFile(mediaFile, out _, out _, out _, out _, out string? failedOperation)
            .Should()
            .BeFalse();

        _ = failedOperation.Should().Be(nameof(ProcessFile.GetMediaProps));
        _ = File.Exists(mediaFile).Should().BeTrue();
    }

    [Fact]
    public void VerifyAndRepair_RepairToolError_KeepsStateAndFiles()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake FfMpeg is a shell script");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        string mediaFile = CopyUnverifiedSample();
        string sidecarFile = Path.ChangeExtension(mediaFile, ".PlexCleaner");
        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();

        // The first verify reads a decode error, then every repair tool fails to launch
        string ffMpeg = Tools.FfMpeg.GetToolPath();
        WriteScript(ffMpeg, "rm -f \"$0\"\nexit 1\n", true);

        bool modified = false;
        _ = processFile.VerifyAndRepair(ref modified).Should().BeFalse();
        _ = File.Exists(ffMpeg).Should().BeFalse();
        _ = processFile.State.Should().Be(SidecarFile.StatesType.None);
        _ = processFile.DeleteFailedFile().Should().BeFalse();

        _ = modified.Should().BeFalse();
        _ = File.Exists(mediaFile).Should().BeTrue();
        _ = File.Exists(sidecarFile).Should().BeTrue();
    }

    [Fact]
    public void VerifyAndRepair_RepairAfterToolError_SetsRepaired()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        string mediaFile = CopyUnverifiedSample();
        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();

        // The remux tier's MkvMerge fails to launch, then the re-encode tier makes it executable
        string mkvMerge = Tools.MkvMerge.GetToolPath();
        WriteScript(mkvMerge, "for a; do out=$a; done\n: > \"$out\"\n", false);
        string ffMpeg = Tools.FfMpeg.GetToolPath();
        WriteScript(
            ffMpeg,
            $$"""
            step=$(($(cat "$0.step" 2>/dev/null || echo 0) + 1))
            echo $step > "$0.step"
            for a; do out=$a; done
            case $step in
            1) exit 1 ;;
            2) chmod u+x "{{mkvMerge}}" && : > "$out" ;;
            esac

            """,
            true
        );

        bool modified = false;
        _ = processFile.VerifyAndRepair(ref modified);
        _ = File.ReadAllText(ffMpeg + ".step").Trim().Should().Be("3");
        // The remux tier's MkvMerge, then the post-repair refresh's MediaInfo, fail to launch
        _ = ToolErrorScope.Count.Should().Be(2);
        _ = processFile
            .State.Should()
            .HaveFlag(SidecarFile.StatesType.Verified)
            .And.HaveFlag(SidecarFile.StatesType.Repaired);
        _ = modified.Should().BeTrue();
    }
}
