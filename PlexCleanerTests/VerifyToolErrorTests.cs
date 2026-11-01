using AwesomeAssertions;
using PlexCleaner;
using Serilog.Events;
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
        FakeTools.WriteScript(Tools.FfMpeg.GetToolPath(), $"kill -{signal} $$\n", true);

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
        FakeTools.WriteScript(Tools.FfMpeg.GetToolPath(), $"exit {exitCode}\n", true);

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
    public void EndedBySignal_FfMpeg_MatchesSignalExitCodes(int exitCode, bool isSignal)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Signal exit codes are not read on Windows");
        _ = Tools.FfMpeg.EndedBySignal(exitCode).Should().Be(isSignal);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(2, false)]
    [InlineData(128, false)]
    [InlineData(129, true)]
    [InlineData(137, true)]
    [InlineData(159, true)]
    [InlineData(160, false)]
    [InlineData(255, false)]
    public void EndedBySignal_OtherTool_MatchesSignalExitCodes(int exitCode, bool isSignal)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Signal exit codes are not read on Windows");
        _ = Tools.MkvMerge.EndedBySignal(exitCode).Should().Be(isSignal);
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
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        string sidecarFile = Path.ChangeExtension(mediaFile, ".PlexCleaner");
        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();

        FakeTools.WriteScript(Tools.MkvMerge.GetToolPath(), "exit 2\n", true);
        FakeTools.WriteScript(Tools.HandBrake.GetToolPath(), "exit 1\n", true);
        FakeTools.WriteScript(
            Tools.FfProbe.GetToolPath(),
            "echo '{ \"streams\": [ { \"closed_captions\": 0 } ] }'\n",
            true
        );
        string ffMpeg = Tools.FfMpeg.GetToolPath();
        FakeTools.WriteScript(
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
        _ = processFile.DeleteFailedFile(nameof(ProcessFile.VerifyAndRepair)).Should().Be(deleted);
        _ = File.ReadAllText(ffMpeg + ".step").Trim().Should().Be(autoRepair ? "2" : "1");

        _ = modified.Should().BeFalse();
        _ = File.Exists(mediaFile).Should().Be(!deleted);
        _ = File.Exists(sidecarFile).Should().Be(!deleted);
    }

    [Theory]
    [InlineData("kill -9 $$\n", "exit 1", "exit 1\n")]
    [InlineData("exit 2\n", "kill -9 $$", "exit 1\n")]
    [InlineData("exit 2\n", "exit 255", "exit 1\n")]
    [InlineData("exit 2\n", "exit 1", "kill -9 $$\n")]
    public void VerifyAndRepair_RepairSignalExit_KeepsStateAndFiles(
        string mkvMerge,
        string reEncode,
        string handBrake
    )
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        string sidecarFile = Path.ChangeExtension(mediaFile, ".PlexCleaner");
        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();

        FakeTools.WriteScript(Tools.MkvMerge.GetToolPath(), mkvMerge, true);
        FakeTools.WriteScript(Tools.HandBrake.GetToolPath(), handBrake, true);
        FakeTools.WriteScript(
            Tools.FfProbe.GetToolPath(),
            "echo '{ \"streams\": [ { \"closed_captions\": 0 } ] }'\n",
            true
        );
        string ffMpeg = Tools.FfMpeg.GetToolPath();
        FakeTools.WriteScript(
            ffMpeg,
            $$"""
            step=$(($(cat "$0.step" 2>/dev/null || echo 0) + 1))
            echo $step > "$0.step"
            [ $step -eq 1 ] && exit 183
            {{reEncode}}

            """,
            true
        );

        bool modified = false;
        _ = processFile.VerifyAndRepair(ref modified).Should().BeFalse();
        _ = ToolErrorScope.Count.Should().Be(1);
        _ = ToolErrorScope.SignalCount.Should().Be(0);
        _ = processFile.State.Should().Be(SidecarFile.StatesType.None);
        _ = processFile.DeleteFailedFile(nameof(ProcessFile.VerifyAndRepair)).Should().BeFalse();
        _ = File.ReadAllText(ffMpeg + ".step").Trim().Should().Be("2");

        _ = modified.Should().BeFalse();
        _ = File.Exists(mediaFile).Should().BeTrue();
        _ = File.Exists(sidecarFile).Should().BeTrue();
    }

    [Fact]
    public void VerifyAndRepair_TimestampReVerifySignalExit_KeepsStateAndFiles()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        string sidecarFile = Path.ChangeExtension(mediaFile, ".PlexCleaner");
        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();

        FakeTools.WriteScript(Tools.FfProbe.GetToolPath(), "exit 1\n", true);
        FakeTools.WriteScript(
            Tools.MkvMerge.GetToolPath(),
            "for a; do out=$a; done\n: > \"$out\"\n",
            true
        );
        FakeTools.WriteScript(Tools.HandBrake.GetToolPath(), "exit 1\n", true);
        string ffMpeg = Tools.FfMpeg.GetToolPath();
        FakeTools.WriteScript(
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
        _ = processFile.DeleteFailedFile(nameof(ProcessFile.VerifyAndRepair)).Should().BeFalse();

        _ = modified.Should().BeFalse();
        _ = File.Exists(mediaFile).Should().BeTrue();
        _ = File.Exists(sidecarFile).Should().BeTrue();
    }

    [Fact]
    public void VerifyAndRepair_PacketAnalysisSignalExit_KeepsStateAndFiles()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        string sidecarFile = Path.ChangeExtension(mediaFile, ".PlexCleaner");
        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();

        FakeTools.WriteScript(Tools.FfProbe.GetToolPath(), "kill -9 $$\n", true);
        FakeTools.WriteScript(Tools.MkvMerge.GetToolPath(), "exit 2\n", true);
        FakeTools.WriteScript(Tools.HandBrake.GetToolPath(), "exit 1\n", true);
        FakeTools.WriteScript(
            Tools.FfMpeg.GetToolPath(),
            """
            step=$(($(cat "$0.step" 2>/dev/null || echo 0) + 1))
            echo $step > "$0.step"
            [ $step -gt 1 ] && exit 1
            echo "non monotonically increasing dts to muxer" >&2
            exit 0

            """,
            true
        );

        bool modified = false;
        _ = processFile.VerifyAndRepair(ref modified).Should().BeFalse();
        _ = ToolErrorScope.Count.Should().BeGreaterThan(0);
        _ = ToolErrorScope.SignalCount.Should().Be(0);
        _ = processFile.State.Should().Be(SidecarFile.StatesType.None);
        _ = processFile.DeleteFailedFile(nameof(ProcessFile.VerifyAndRepair)).Should().BeFalse();

        _ = modified.Should().BeFalse();
        _ = File.Exists(mediaFile).Should().BeTrue();
        _ = File.Exists(sidecarFile).Should().BeTrue();
    }

    [Fact]
    public void VerifyAndRepair_PostDecodeTimestampRepairFails_KeepsFilesOnEveryRun()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        string sidecarFile = Path.ChangeExtension(mediaFile, ".PlexCleaner");
        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();

        FakeTools.WriteScript(Tools.FfProbe.GetToolPath(), "echo '{\"packets\":[]}'\n", true);
        FakeTools.WriteScript(
            Tools.MkvMerge.GetToolPath(),
            "for a; do out=$a; done\n: > \"$out\"\n",
            true
        );
        FakeTools.WriteScript(Tools.HandBrake.GetToolPath(), "exit 1\n", true);
        string ffMpeg = Tools.FfMpeg.GetToolPath();
        FakeTools.WriteScript(
            ffMpeg,
            """
            step=$(($(cat "$0.step" 2>/dev/null || echo 0) + 1))
            echo $step > "$0.step"
            echo "non monotonically increasing dts to muxer" >&2
            exit 0

            """,
            true
        );

        bool modified = false;
        _ = processFile.VerifyAndRepair(ref modified).Should().BeFalse();
        _ = ToolErrorScope.Count.Should().Be(0);
        _ = ToolErrorScope.SignalCount.Should().Be(0);
        _ = processFile
            .State.Should()
            .Be(
                SidecarFile.StatesType.VerifyFailed
                    | SidecarFile.StatesType.RepairFailed
                    | SidecarFile.StatesType.PostDecodeTimestamps
            );
        bool deleted = true;
        IReadOnlyList<LogEvent> events = LogCapture.Capture(() =>
            deleted = processFile.DeleteFailedFile(nameof(ProcessFile.VerifyAndRepair))
        );
        _ = deleted.Should().BeFalse();
        _ = LogCapture
            .Messages(events, LogEventLevel.Information)
            .Should()
            .ContainSingle()
            .Which.Should()
            .StartWith("Keeping failed file : PostDecodeTimestamps : ");
        _ = File.ReadAllText(ffMpeg + ".step").Trim().Should().Be("2");

        ProcessFile nextRun = new(mediaFile);
        _ = nextRun.GetMediaProps().Should().BeTrue();
        _ = nextRun.VerifyAndRepair(ref modified).Should().BeFalse();
        _ = nextRun.State.Should().HaveFlag(SidecarFile.StatesType.PostDecodeTimestamps);
        _ = nextRun.DeleteFailedFile(nameof(ProcessFile.VerifyAndRepair)).Should().BeFalse();

        _ = modified.Should().BeFalse();
        _ = File.Exists(mediaFile).Should().BeTrue();
        _ = File.Exists(sidecarFile).Should().BeTrue();
    }

    [Fact]
    public void RepairTimestamps_CleanVerify_ClearsPostDecode()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        string mediaFile = FakeTools.CopyUnverifiedSample(
            _directory,
            SidecarFile.StatesType.VerifyFailed
                | SidecarFile.StatesType.RepairFailed
                | SidecarFile.StatesType.PostDecodeTimestamps
        );
        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();

        FakeTools.WriteScript(Tools.FfMpeg.GetToolPath(), "exit 0\n", true);

        bool modified = false;
        _ = processFile.RepairTimestamps(ref modified).Should().BeTrue();
        _ = processFile.State.Should().Be(SidecarFile.StatesType.Verified);

        ProcessFile nextRun = new(mediaFile);
        _ = nextRun.GetMediaProps().Should().BeTrue();
        _ = nextRun.State.Should().Be(SidecarFile.StatesType.Verified);
    }

    [Theory]
    [InlineData(true, SidecarFile.StatesType.Verified)]
    [InlineData(false, SidecarFile.StatesType.PostDecodeTimestamps)]
    public void VerifyAndRepair_VerifyPasses_ClearsPostDecodeOnlyWhenVerifyRuns(
        bool verify,
        SidecarFile.StatesType state
    )
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        Program.Config.ProcessOptions.Verify = verify;
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        string mediaFile = FakeTools.CopyUnverifiedSample(
            _directory,
            SidecarFile.StatesType.PostDecodeTimestamps
        );
        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();

        FakeTools.WriteScript(Tools.FfMpeg.GetToolPath(), "exit 0\n", true);

        bool modified = false;
        _ = processFile.VerifyAndRepair(ref modified).Should().BeTrue();
        _ = processFile.State.Should().Be(state);
        _ = modified.Should().BeFalse();
    }

    [Fact]
    public void VerifyAndRepair_VerifyOff_LeavesVerifyFailedAndRunsNoTool()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        Program.Config.ProcessOptions.Verify = false;
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        string mediaFile = FakeTools.CopyUnverifiedSample(
            _directory,
            SidecarFile.StatesType.VerifyFailed
        );
        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();

        string ffMpeg = Tools.FfMpeg.GetToolPath();
        FakeTools.WriteScript(ffMpeg, "touch \"$0.ran\"\nexit 1\n");

        bool modified = false;
        _ = processFile.VerifyAndRepair(ref modified).Should().BeTrue();
        _ = processFile.State.Should().Be(SidecarFile.StatesType.VerifyFailed);
        _ = File.Exists(ffMpeg + ".ran").Should().BeFalse();
        _ = modified.Should().BeFalse();

        ProcessFile nextRun = new(mediaFile);
        _ = nextRun.GetMediaProps().Should().BeTrue();
        _ = nextRun.State.Should().Be(SidecarFile.StatesType.VerifyFailed);
    }

    [Fact]
    public void RepairTimestamps_PacketAnalysisFailsRepairFails_KeepsPostDecodeAndFiles()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        string mediaFile = FakeTools.CopyUnverifiedSample(
            _directory,
            SidecarFile.StatesType.VerifyFailed
                | SidecarFile.StatesType.RepairFailed
                | SidecarFile.StatesType.PostDecodeTimestamps
        );
        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();

        FakeTools.WriteScript(Tools.FfProbe.GetToolPath(), "exit 1\n", true);
        _ = processFile.GetPacketAnalysis(false, out _, out _).Should().BeFalse();
        FakeTools.WriteScript(
            Tools.MkvMerge.GetToolPath(),
            "for a; do out=$a; done\n: > \"$out\"\n",
            true
        );
        FakeTools.WriteScript(Tools.HandBrake.GetToolPath(), "exit 1\n", true);
        string ffMpeg = Tools.FfMpeg.GetToolPath();
        FakeTools.WriteScript(
            ffMpeg,
            """
            step=$(($(cat "$0.step" 2>/dev/null || echo 0) + 1))
            echo $step > "$0.step"
            [ $step -gt 2 ] && exit 1
            echo "non monotonically increasing dts to muxer" >&2
            exit 0

            """,
            true
        );

        bool modified = false;
        _ = processFile.RepairTimestamps(ref modified).Should().BeFalse();
        _ = ToolErrorScope.Count.Should().Be(0);
        _ = ToolErrorScope.SignalCount.Should().Be(0);
        _ = processFile
            .State.Should()
            .Be(
                SidecarFile.StatesType.VerifyFailed
                    | SidecarFile.StatesType.RepairFailed
                    | SidecarFile.StatesType.PostDecodeTimestamps
            );
        _ = File.ReadAllText(ffMpeg + ".step").Trim().Should().Be("3");
        _ = processFile.DeleteFailedFile(nameof(ProcessFile.VerifyAndRepair)).Should().BeFalse();

        _ = modified.Should().BeFalse();
        _ = File.Exists(mediaFile).Should().BeTrue();
        _ = File.Exists(Path.ChangeExtension(mediaFile, ".PlexCleaner")).Should().BeTrue();
    }

    [Fact]
    public void VerifyAndRepair_ToolError_KeepsStateAndFiles()
    {
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        string sidecarFile = Path.ChangeExtension(mediaFile, ".PlexCleaner");
        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();

        bool modified = false;
        _ = processFile.VerifyAndRepair(ref modified).Should().BeFalse();
        _ = processFile.State.Should().Be(SidecarFile.StatesType.None);
        _ = processFile.DeleteFailedFile(nameof(ProcessFile.VerifyAndRepair)).Should().BeFalse();

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
        _ = processFile.DeleteFailedFile(nameof(ProcessFile.VerifyAndRepair)).Should().BeFalse();

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
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        string sidecarFile = Path.ChangeExtension(mediaFile, ".PlexCleaner");
        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();

        // The first verify reads a decode error, then every repair tool fails to launch
        string ffMpeg = Tools.FfMpeg.GetToolPath();
        FakeTools.WriteScript(ffMpeg, "rm -f \"$0\"\nexit 1\n", true);

        bool modified = false;
        _ = processFile.VerifyAndRepair(ref modified).Should().BeFalse();
        _ = File.Exists(ffMpeg).Should().BeFalse();
        _ = processFile.State.Should().Be(SidecarFile.StatesType.None);
        _ = processFile.DeleteFailedFile(nameof(ProcessFile.VerifyAndRepair)).Should().BeFalse();

        _ = modified.Should().BeFalse();
        _ = File.Exists(mediaFile).Should().BeTrue();
        _ = File.Exists(sidecarFile).Should().BeTrue();
    }

    [Fact]
    public void VerifyAndRepair_RepairAfterToolError_SetsRepaired()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();

        // The remux tier's MkvMerge fails to launch, then the re-encode tier makes it executable
        string mkvMerge = Tools.MkvMerge.GetToolPath();
        FakeTools.WriteScript(
            mkvMerge,
            FakeTools.SampleIdentifyCase(_directory) + "for a; do out=$a; done\n: > \"$out\"\n",
            false
        );
        FakeTools.WriteScript(Tools.MkvPropEdit.GetToolPath(), "exit 0\n");
        string ffMpeg = Tools.FfMpeg.GetToolPath();
        FakeTools.WriteScript(
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
