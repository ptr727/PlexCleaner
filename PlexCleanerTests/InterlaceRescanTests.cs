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

    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly ToolsOptions1 _toolsOptions = Program.Config.ToolsOptions;
    private readonly bool _verify = Program.Config.ProcessOptions.Verify;
    private readonly bool _useSidecarFiles = Program.Config.ProcessOptions.UseSidecarFiles;
    private readonly bool _deInterlace = Program.Config.ProcessOptions.DeInterlace;
    private readonly bool _autoRepair = Program.Config.VerifyOptions.AutoRepair;
    private readonly int _maximumBitrate = Program.Config.VerifyOptions.MaximumBitrate;
    private readonly bool _quickScan = Program.Options.QuickScan;
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
        Program.Config.VerifyOptions.MaximumBitrate = _maximumBitrate;
        Program.Options.QuickScan = _quickScan;
        Directory.Delete(_directory, true);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DeInterlaceAfterRepair_ReEncodeRepair_RescansOnlyASkippedScan(bool skipped)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        FakeTools.WriteSampleToolData(_directory);
        string mkvMerge = Tools.MkvMerge.GetToolPath();
        FakeTools.WriteScript(
            mkvMerge,
            FakeTools.SampleIdentifyCase(_directory)
                + "for a; do case $a in *.tmp*) echo mkvmerge > \"$a\" ;; esac; done\n"
        );
        FakeTools.WriteScript(Tools.MkvPropEdit.GetToolPath(), "exit 0\n");
        string handBrake = Tools.HandBrake.GetToolPath();
        FakeTools.WriteScript(
            handBrake,
            "echo \"$@\" > \"$0.log\"\nfor a; do case $a in *.tmp8) echo handbrake > \"$a\" ;; esac; done\n"
        );

        // The first scan is skipped or progressive, verify and the remux tier fail, the re-encode verifies clean, and a rescan reads interlaced
        string firstScan = skipped
            ? InterlacedIdet + "\nexit 69"
            : InterlacedIdet
                .Replace("TFF: 300", "TFF: 0", StringComparison.Ordinal)
                .Replace("Progressive: 10", "Progressive: 310", StringComparison.Ordinal);
        string ffMpeg = Tools.FfMpeg.GetToolPath();
        FakeTools.WriteScript(
            ffMpeg,
            $$"""
            step=$(($(cat "$0.step" 2>/dev/null || echo 0) + 1))
            echo $step > "$0.step"
            case $step in
            1)
            {{firstScan}}
            ;;
            2|3) exit 1 ;;
            4) for a; do case $a in *.tmp12) echo ffmpeg > "$a" ;; esac; done ;;
            6)
            {{InterlacedIdet}}
            ;;
            esac
            exit 0

            """
        );

        bool modified = false;
        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();
        _ = processFile.DeInterlace(true, ref modified).Should().BeTrue();
        _ = processFile.VerifyAndRepair(ref modified).Should().BeTrue();
        _ = processFile.DeInterlaceAfterRepair(ref modified).Should().BeTrue();

        _ = File.ReadAllText(ffMpeg + ".step").Trim().Should().Be(skipped ? "6" : "5");
        _ = File.Exists(handBrake + ".log").Should().Be(skipped);
        _ = processFile.State.HasFlag(SidecarFile.StatesType.DeInterlaced).Should().Be(skipped);
        _ = processFile
            .State.HasFlag(SidecarFile.StatesType.ScannedInterlace)
            .Should()
            .Be(!skipped);
        _ = ToolErrorScope.Count.Should().Be(0);
        _ = modified.Should().BeTrue();
    }
}
