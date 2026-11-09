using System.Globalization;
using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public sealed class InterlaceRescanTests : IDisposable
{
    private const string InterlacedIdet = """
        echo '[Parsed_idet_0 @ 0] Repeated Fields: Neither: 318 Top: 0 Bottom: 0' >&2
        echo '[Parsed_idet_0 @ 0] Single frame detection: TFF: 300 BFF: 0 Progressive: 10 Undetermined: 8' >&2
        echo '[Parsed_idet_0 @ 0] Multi frame detection: TFF: 300 BFF: 0 Progressive: 10 Undetermined: 8' >&2
        """;

    private const string ProgressiveIdet = """
        echo '[Parsed_idet_0 @ 0] Repeated Fields: Neither: 318 Top: 0 Bottom: 0' >&2
        echo '[Parsed_idet_0 @ 0] Single frame detection: TFF: 0 BFF: 0 Progressive: 310 Undetermined: 8' >&2
        echo '[Parsed_idet_0 @ 0] Multi frame detection: TFF: 0 BFF: 0 Progressive: 310 Undetermined: 8' >&2
        """;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly ToolsOptions1 _toolsOptions = Program.Config.ToolsOptions;
    private readonly bool _verify = Program.Config.ProcessOptions.Verify;
    private readonly bool _useSidecarFiles = Program.Config.ProcessOptions.UseSidecarFiles;
    private readonly bool _deInterlace = Program.Config.ProcessOptions.DeInterlace;
    private readonly bool _autoRepair = Program.Config.VerifyOptions.AutoRepair;
    private readonly bool _deleteInvalidFiles = Program.Config.VerifyOptions.DeleteInvalidFiles;
    private readonly int _maximumBitrate = Program.Config.VerifyOptions.MaximumBitrate;
    private readonly bool _quickScan = Program.Options.QuickScan;
    private readonly List<VideoFormat> _reEncodeVideo = Program.Config.ProcessOptions.ReEncodeVideo;
    private readonly MediaToolInfo _ffProbeInfo = Tools.FfProbe.Info;
    private readonly MediaToolInfo _mkvMergeInfo = Tools.MkvMerge.Info;
    private readonly MediaToolInfo _mediaInfoInfo = Tools.MediaInfo.Info;

    public InterlaceRescanTests()
    {
        _ = Directory.CreateDirectory(_directory);
        Program.Config.ProcessOptions.Verify = true;
        Program.Config.ProcessOptions.UseSidecarFiles = true;
        Program.Config.ProcessOptions.DeInterlace = true;
        Program.Config.VerifyOptions.AutoRepair = true;
        Program.Config.VerifyOptions.MaximumBitrate = 0;
        Program.Options.QuickScan = false;
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
        Program.Config.ProcessOptions.DeInterlace = _deInterlace;
        Program.Config.VerifyOptions.AutoRepair = _autoRepair;
        Program.Config.VerifyOptions.DeleteInvalidFiles = _deleteInvalidFiles;
        Program.Config.VerifyOptions.MaximumBitrate = _maximumBitrate;
        Program.Options.QuickScan = _quickScan;
        Program.Config.ProcessOptions.ReEncodeVideo = _reEncodeVideo;
        Directory.Delete(_directory, true);
    }

    private (
        string FfMpeg,
        string HandBrake,
        string Sidecar,
        string Snapshot,
        int RescanStep
    ) WriteTools(string mediaFile, bool skipped, bool reEncode, string rescanIdet)
    {
        FakeTools.WriteSampleToolData(_directory);
        string mkvMerge = Tools.MkvMerge.GetToolPath();
        FakeTools.WriteScript(
            mkvMerge,
            FakeTools.SampleIdentifyCase(_directory)
                + "for a; do case $a in *.tmp*) echo mkvmerge > \"$a\" ;; esac; done\n"
        );
        FakeTools.WriteScript(Tools.MkvPropEdit.GetToolPath(), "exit 0\n");

        // Each bitrate packet scan snapshots the sidecar, the last one runs right after a re-encode repair saves it
        Program.Config.VerifyOptions.MaximumBitrate = int.MaxValue;
        string sidecar = SidecarFile.GetSidecarName(new FileInfo(mediaFile));
        string snapshot = Path.Combine(_directory, "snapshot.json");
        string packets = FakeTools.WritePackets(_directory, "packets", []);
        string props = Path.Combine(_directory, "FfProbeInfoData.json");
        FakeTools.WriteScript(
            Tools.FfProbe.GetToolPath(),
            $$"""
            case "$*" in
            *-show_packets*) cp "{{sidecar}}" "{{snapshot}}" ; cat "{{packets}}" ;;
            *) cat "{{props}}" ;;
            esac

            """
        );
        string handBrake = Tools.HandBrake.GetToolPath();
        FakeTools.WriteScript(
            handBrake,
            "echo \"$@\" > \"$0.log\"\nfor a; do case $a in *.tmp8) echo handbrake > \"$a\" ;; esac; done\n"
        );

        // Verify fails, then the remux tier or the re-encode verifies clean, and a skipped first scan is scanned again
        string repair = reEncode
            ? "2|3) exit 1 ;;\n4) for a; do case $a in *.tmp12) echo ffmpeg > \"$a\" ;; esac; done ;;"
            : "2) exit 1 ;;";
        int rescanStep = reEncode ? 6 : 4;
        string ffMpeg = Tools.FfMpeg.GetToolPath();
        FakeTools.WriteScript(
            ffMpeg,
            $$"""
            step=$(($(cat "$0.step" 2>/dev/null || echo 0) + 1))
            echo $step > "$0.step"
            case $step in
            1)
            {{(skipped ? InterlacedIdet + "\nexit 69" : ProgressiveIdet)}}
            ;;
            {{repair}}
            {{rescanStep}})
            {{rescanIdet}}
            ;;
            esac
            exit 0

            """
        );

        return (ffMpeg, handBrake, sidecar, snapshot, rescanStep);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    public void DeInterlaceAfterRepair_Repair_RescansOnlyASkippedScan(
        bool skipped,
        bool reEncode,
        bool rescanInterlaced
    )
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        (string ffMpeg, string handBrake, string sidecar, string snapshot, int rescanStep) =
            WriteTools(
                mediaFile,
                skipped,
                reEncode,
                rescanInterlaced ? InterlacedIdet : ProgressiveIdet
            );

        bool modified = false;
        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();
        _ = processFile.DeInterlace(true, ref modified).Should().BeTrue();
        _ = processFile.VerifyAndRepair(ref modified).Should().BeTrue();
        SidecarFile.StatesType saved = (SidecarFile.StatesType)FakeTools.ReadSidecarState(sidecar);
        _ = saved.HasFlag(SidecarFile.StatesType.Verified).Should().Be(!skipped);
        if (reEncode)
        {
            SidecarFile.StatesType measured = (SidecarFile.StatesType)
                FakeTools.ReadSidecarState(snapshot);
            _ = measured.HasFlag(SidecarFile.StatesType.Repaired).Should().BeTrue();
            _ = measured.HasFlag(SidecarFile.StatesType.Verified).Should().Be(!skipped);
        }
        _ = processFile.DeInterlaceAfterRepair(ref modified).Should().BeTrue();

        bool deInterlaced = skipped && rescanInterlaced;
        _ = File.ReadAllText(ffMpeg + ".step")
            .Trim()
            .Should()
            .Be((skipped ? rescanStep : rescanStep - 1).ToString(CultureInfo.InvariantCulture));
        _ = File.Exists(handBrake + ".log").Should().Be(deInterlaced);
        _ = processFile
            .State.HasFlag(SidecarFile.StatesType.DeInterlaced)
            .Should()
            .Be(deInterlaced);
        _ = processFile.State.HasFlag(SidecarFile.StatesType.Verified).Should().Be(!deInterlaced);
        _ = processFile
            .State.HasFlag(SidecarFile.StatesType.ScannedInterlace)
            .Should()
            .Be(!deInterlaced);
        _ = ((SidecarFile.StatesType)FakeTools.ReadSidecarState(sidecar))
            .Should()
            .Be(processFile.State);
        _ = ToolErrorScope.Count.Should().Be(0);
        _ = modified.Should().BeTrue();
    }

    [Fact]
    public void HandleFailedFile_RescanAfterRepairFails_KeepsRepairedFileAndMarksProcessFailed()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        Program.Config.VerifyOptions.DeleteInvalidFiles = true;
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        (_, string handBrake, string sidecar, _, _) = WriteTools(
            mediaFile,
            skipped: true,
            reEncode: true,
            "echo 'no idet output' >&2"
        );

        bool modified = false;
        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();
        _ = processFile.DeInterlace(true, ref modified).Should().BeTrue();
        _ = processFile.VerifyAndRepair(ref modified).Should().BeTrue();
        _ = processFile.DeInterlaceAfterRepair(ref modified).Should().BeFalse();

        _ = processFile
            .HandleFailedFile(nameof(ProcessFile.DeInterlaceAfterRepair))
            .Should()
            .BeFalse();
        _ = File.Exists(mediaFile).Should().BeTrue();
        _ = File.Exists(handBrake + ".log").Should().BeFalse();
        SidecarFile.StatesType saved = (SidecarFile.StatesType)FakeTools.ReadSidecarState(sidecar);
        _ = saved.HasFlag(SidecarFile.StatesType.Repaired).Should().BeTrue();
        _ = saved.HasFlag(SidecarFile.StatesType.Verified).Should().BeFalse();
        _ = saved.HasFlag(SidecarFile.StatesType.ProcessFailed).Should().BeTrue();
    }

    [Fact]
    public void DeInterlaceAfterRepair_RescanSkippedAgain_MarksProcessFailedNotVerified()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        Program.Config.VerifyOptions.DeleteInvalidFiles = true;
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        (_, string handBrake, string sidecar, _, _) = WriteTools(
            mediaFile,
            skipped: true,
            reEncode: false,
            InterlacedIdet + "\nexit 69"
        );

        bool modified = false;
        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();
        _ = processFile.DeInterlace(true, ref modified).Should().BeTrue();
        _ = processFile.VerifyAndRepair(ref modified).Should().BeTrue();
        _ = processFile.DeInterlaceAfterRepair(ref modified).Should().BeFalse();
        _ = processFile
            .HandleFailedFile(nameof(ProcessFile.DeInterlaceAfterRepair))
            .Should()
            .BeFalse();

        _ = File.Exists(mediaFile).Should().BeTrue();
        _ = File.Exists(handBrake + ".log").Should().BeFalse();
        SidecarFile.StatesType saved = (SidecarFile.StatesType)FakeTools.ReadSidecarState(sidecar);
        _ = saved.HasFlag(SidecarFile.StatesType.Repaired).Should().BeTrue();
        _ = saved.HasFlag(SidecarFile.StatesType.Verified).Should().BeFalse();
        _ = saved.HasFlag(SidecarFile.StatesType.ProcessFailed).Should().BeTrue();
        _ = new ProcessFile(mediaFile).GetMediaProps(true).Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReEncode_AfterSkippedScan_RescansBeforeVerified(bool rescanSkipped)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        Program.Config.ProcessOptions.ReEncodeVideo = [new VideoFormat()];
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        (string ffMpeg, _, string sidecar, _, _) = WriteTools(
            mediaFile,
            skipped: true,
            reEncode: false,
            ProgressiveIdet
        );

        // The first idet run is skipped, the re-encode writes its output, and the verify is clean
        string secondIdet = rescanSkipped ? InterlacedIdet + "\nexit 69" : ProgressiveIdet;
        FakeTools.WriteScript(
            ffMpeg,
            $$"""
            case "$*" in
            *idet*)
            idet=$(($(cat "$0.idet" 2>/dev/null || echo 0) + 1))
            echo $idet > "$0.idet"
            if [ $idet -eq 1 ]; then
            {{InterlacedIdet}}
            exit 69
            fi
            {{secondIdet}}
            ;;
            *.tmp1*) for a; do case $a in *.tmp1) echo ffmpeg > "$a" ;; esac; done ;;
            esac
            exit 0

            """,
            true
        );

        bool modified = false;
        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();
        _ = processFile.DeInterlace(true, ref modified).Should().BeTrue();
        _ = processFile.ReEncode(false, ref modified).Should().BeTrue();
        _ = processFile.VerifyAndRepair(ref modified).Should().BeTrue();
        _ = ((SidecarFile.StatesType)FakeTools.ReadSidecarState(sidecar))
            .HasFlag(SidecarFile.StatesType.Verified)
            .Should()
            .BeFalse();

        _ = processFile.DeInterlaceAfterRepair(ref modified).Should().Be(!rescanSkipped);
        _ = File.ReadAllText(ffMpeg + ".idet").Trim().Should().Be("2");
        _ = processFile.State.HasFlag(SidecarFile.StatesType.ReEncoded).Should().BeTrue();
        _ = processFile.State.HasFlag(SidecarFile.StatesType.Verified).Should().Be(!rescanSkipped);
        _ = ((SidecarFile.StatesType)FakeTools.ReadSidecarState(sidecar))
            .Should()
            .Be(processFile.State);
        _ = ToolErrorScope.Count.Should().Be(0);
    }
}
