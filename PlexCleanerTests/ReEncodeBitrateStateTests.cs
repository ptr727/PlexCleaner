using System.Text.Json.Nodes;
using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public sealed class ReEncodeBitrateStateTests : IDisposable
{
    // 8000 bits per second is 1000 bytes per second, so one large packet exceeds it
    private const int MaximumBitrate = 8000;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly ToolsOptions1 _toolsOptions = Program.Config.ToolsOptions;
    private readonly bool _verify = Program.Config.ProcessOptions.Verify;
    private readonly bool _useSidecarFiles = Program.Config.ProcessOptions.UseSidecarFiles;
    private readonly bool _reEncode = Program.Config.ProcessOptions.ReEncode;
    private readonly bool _deInterlace = Program.Config.ProcessOptions.DeInterlace;
    private readonly HashSet<string> _reEncodeAudioFormats = Program
        .Config
        .ProcessOptions
        .ReEncodeAudioFormats;
    private readonly bool _autoRepair = Program.Config.VerifyOptions.AutoRepair;
    private readonly bool _deleteInvalidFiles = Program.Config.VerifyOptions.DeleteInvalidFiles;
    private readonly int _maximumBitrate = Program.Config.VerifyOptions.MaximumBitrate;
    private readonly MediaToolInfo _ffProbeInfo = Tools.FfProbe.Info;
    private readonly MediaToolInfo _mkvMergeInfo = Tools.MkvMerge.Info;
    private readonly MediaToolInfo _mediaInfoInfo = Tools.MediaInfo.Info;

    public ReEncodeBitrateStateTests()
    {
        _ = Directory.CreateDirectory(_directory);
        Program.Config.ProcessOptions.Verify = true;
        Program.Config.ProcessOptions.UseSidecarFiles = true;
        Program.Config.ProcessOptions.ReEncode = true;
        Program.Config.ProcessOptions.DeInterlace = true;
        Program.Config.VerifyOptions.AutoRepair = true;
        Program.Config.VerifyOptions.DeleteInvalidFiles = true;
        Program.Config.VerifyOptions.MaximumBitrate = MaximumBitrate;

        // An empty tools folder makes every tool without a fake fail to launch
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
        Program.Config.ProcessOptions.ReEncode = _reEncode;
        Program.Config.ProcessOptions.DeInterlace = _deInterlace;
        Program.Config.ProcessOptions.ReEncodeAudioFormats = _reEncodeAudioFormats;
        Program.Config.VerifyOptions.AutoRepair = _autoRepair;
        Program.Config.VerifyOptions.DeleteInvalidFiles = _deleteInvalidFiles;
        Program.Config.VerifyOptions.MaximumBitrate = _maximumBitrate;
        Directory.Delete(_directory, true);
    }

    private ProcessFile CreateProcessFile(SidecarFile.StatesType state)
    {
        ProcessFile processFile = new(FakeTools.CopyUnverifiedSample(_directory, state));
        _ = processFile.GetMediaProps().Should().BeTrue();
        return processFile;
    }

    private static ProcessFile NextRun(ProcessFile processFile)
    {
        ProcessFile nextRun = new(processFile.FileInfo.FullName);
        _ = nextRun.GetMediaProps().Should().BeTrue();
        return nextRun;
    }

    private string CatPackets(string name, long streamIndex, bool exceeded)
    {
        JsonArray packets = [];
        if (exceeded)
        {
            packets.Add(
                new JsonObject
                {
                    ["codec_type"] = "video",
                    ["stream_index"] = streamIndex,
                    ["pts_time"] = 0.5,
                    ["dts_time"] = 0.5,
                    ["duration_time"] = 0.5,
                    ["size"] = 1000000,
                }
            );
        }
        string fileName = Path.Combine(_directory, name + ".json");
        File.WriteAllText(fileName, new JsonObject { ["packets"] = packets }.ToJsonString());
        return $"cat \"{fileName}\"";
    }

    // The first packet analysis runs the first command, every later one runs the second
    private void WriteFakeFfProbe(string first, string later)
    {
        string props = Path.Combine(_directory, "FfProbeInfoData.json");
        FakeTools.WriteScript(
            Tools.FfProbe.GetToolPath(),
            $$"""
            case "$*" in
            *-show_packets*)
                step=$(($(cat "$0.step" 2>/dev/null || echo 0) + 1))
                echo $step > "$0.step"
                if [ $step -eq 1 ]; then {{first}}; else {{later}}; fi ;;
            *) cat "{{props}}" ;;
            esac

            """
        );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReEncode_AlreadyReEncoded_ConvertsAgainOnlyWhenExplicit(bool conditional)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        ProcessFile processFile = CreateProcessFile(SidecarFile.StatesType.ReEncoded);
        Program.Config.ProcessOptions.ReEncodeAudioFormats = new HashSet<string>(
            [processFile.FfProbeProps.Audio[0].Format],
            StringComparer.OrdinalIgnoreCase
        );
        _ = processFile.FindNeedReEncode().Selected.Audio.Should().NotBeEmpty();
        string ffMpeg = Tools.FfMpeg.GetToolPath();
        FakeTools.WriteScript(ffMpeg, "touch \"$0.ran\"\nexit 1\n");

        bool modified = false;
        _ = processFile.ReEncode(conditional, ref modified).Should().Be(conditional);

        _ = File.Exists(ffMpeg + ".ran").Should().Be(!conditional);
        _ = modified.Should().BeFalse();
        _ = processFile.State.Should().Be(SidecarFile.StatesType.ReEncoded);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DeInterlace_AlreadyDeInterlaced_ConvertsAgainOnlyWhenExplicit(bool conditional)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        ProcessFile processFile = CreateProcessFile(SidecarFile.StatesType.DeInterlaced);
        processFile.FfProbeProps.Video[0].Interlaced = true;
        string handBrake = Tools.HandBrake.GetToolPath();
        FakeTools.WriteScript(handBrake, "touch \"$0.ran\"\nexit 1\n");

        bool modified = false;
        _ = processFile.DeInterlace(conditional, ref modified).Should().Be(conditional);

        _ = File.Exists(handBrake + ".ran").Should().Be(!conditional);
        _ = modified.Should().BeFalse();
        _ = processFile.State.Should().Be(SidecarFile.StatesType.DeInterlaced);
    }

    [Theory]
    [InlineData(true, SidecarFile.StatesType.None)]
    [InlineData(false, SidecarFile.StatesType.BitrateExceeded)]
    public void VerifyAndRepair_BitrateMeasured_SetsOrClearsBitrateExceeded(
        bool exceeded,
        SidecarFile.StatesType state
    )
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        ProcessFile processFile = CreateProcessFile(state);
        string packets = CatPackets("packets", processFile.FfProbeProps.Video[0].Id, exceeded);
        WriteFakeFfProbe(packets, packets);
        FakeTools.WriteScript(Tools.FfMpeg.GetToolPath(), "exit 0\n");

        bool modified = false;
        _ = processFile.VerifyAndRepair(ref modified).Should().BeTrue();

        SidecarFile.StatesType expected =
            SidecarFile.StatesType.Verified
            | (exceeded ? SidecarFile.StatesType.BitrateExceeded : SidecarFile.StatesType.None);
        _ = processFile.State.Should().Be(expected);
        _ = NextRun(processFile).State.Should().Be(expected);
        _ = modified.Should().BeFalse();
    }

    [Fact]
    public void VerifyAndRepair_BitrateAnalysisFails_KeepsStateAndVerifies()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        ProcessFile processFile = CreateProcessFile(SidecarFile.StatesType.BitrateExceeded);
        FakeTools.WriteScript(Tools.FfProbe.GetToolPath(), "exit 1\n");
        FakeTools.WriteScript(Tools.FfMpeg.GetToolPath(), "exit 0\n");

        bool modified = false;
        _ = processFile.VerifyAndRepair(ref modified).Should().BeTrue();

        _ = ToolErrorScope.Count.Should().Be(0);
        _ = processFile
            .State.Should()
            .Be(SidecarFile.StatesType.Verified | SidecarFile.StatesType.BitrateExceeded);
    }

    [Fact]
    public void VerifyAndRepair_BitrateAnalysisToolError_LeavesVerifiedUnsetForRetry()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        ProcessFile processFile = CreateProcessFile(SidecarFile.StatesType.BitrateExceeded);
        string ffMpeg = Tools.FfMpeg.GetToolPath();
        FakeTools.WriteScript(ffMpeg, "echo >> \"$0.step\"\nexit 0\n");

        // FfProbe has no fake, so the packet analysis fails to launch
        bool modified = false;
        _ = processFile.VerifyAndRepair(ref modified).Should().BeTrue();
        _ = ToolErrorScope.Count.Should().BeGreaterThan(0);
        _ = processFile.State.Should().Be(SidecarFile.StatesType.BitrateExceeded);

        // The next run verifies again
        ProcessFile nextRun = NextRun(processFile);
        _ = nextRun.State.Should().Be(SidecarFile.StatesType.BitrateExceeded);
        _ = nextRun.VerifyAndRepair(ref modified).Should().BeTrue();
        _ = File.ReadAllLines(ffMpeg + ".step").Should().HaveCount(2);
        _ = modified.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, SidecarFile.StatesType.Verified | SidecarFile.StatesType.Repaired)]
    [InlineData(true, SidecarFile.StatesType.Repaired | SidecarFile.StatesType.BitrateExceeded)]
    public void VerifyAndRepair_ReEncodeRepair_MeasuresBitrateAgain(
        bool reMeasureToolError,
        SidecarFile.StatesType expected
    )
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        ProcessFile processFile = CreateProcessFile(SidecarFile.StatesType.None);
        FakeTools.WriteSampleToolData(_directory);
        string mkvPropEdit = Tools.MkvPropEdit.GetToolPath();
        FakeTools.WriteScript(mkvPropEdit, "echo \"$@\" > \"$0.log\"\n");
        WriteFakeFfProbe(
            CatPackets("first", processFile.FfProbeProps.Video[0].Id, true),
            reMeasureToolError ? "kill -9 $$" : CatPackets("later", 0, false)
        );

        // Verify fails, the remux tier's verify fails, then the re-encode tier verifies clean
        string ffMpeg = Tools.FfMpeg.GetToolPath();
        FakeTools.WriteScript(
            ffMpeg,
            """
            step=$(($(cat "$0.step" 2>/dev/null || echo 0) + 1))
            echo $step > "$0.step"
            case $step in
            1|2) exit 1 ;;
            3) for a; do case $a in *.tmp12) echo ffmpeg > "$a" ;; esac; done ;;
            esac
            exit 0

            """
        );

        bool modified = false;
        _ = processFile.VerifyAndRepair(ref modified).Should().BeTrue();

        _ = File.ReadAllText(ffMpeg + ".step").Trim().Should().Be("4");
        _ = File.ReadAllText(Tools.FfProbe.GetToolPath() + ".step").Trim().Should().Be("2");
        _ = File.ReadAllText(mkvPropEdit + ".log").Should().Contain("name.tmp12");
        _ = ToolErrorScope.Count.Should().Be(reMeasureToolError ? 1 : 0);
        _ = processFile.State.Should().Be(expected);
        _ = NextRun(processFile).State.Should().Be(expected);
        _ = modified.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VerifyAndRepair_HandBrakeRepair_MergesOriginalSubtitlesAndRestoresLanguage(
        bool subtitle
    )
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        ProcessFile processFile = CreateProcessFile(SidecarFile.StatesType.None);
        FakeTools.WriteSampleToolData(_directory);
        string mkvPropEdit = Tools.MkvPropEdit.GetToolPath();
        FakeTools.WriteScript(mkvPropEdit, "echo \"$@\" > \"$0.log\"\n");
        string handBrake = Tools.HandBrake.GetToolPath();
        FakeTools.WriteScript(
            handBrake,
            "echo \"$@\" > \"$0.log\"\nfor a; do case $a in *.tmp8) echo handbrake > \"$a\" ;; esac; done\n"
        );
        string mkvMerge = Tools.MkvMerge.GetToolPath();
        FakeTools.WriteScript(
            mkvMerge,
            FakeTools.SampleIdentifyCase(_directory)
                + "echo \"$@\" >> \"$0.log\"\nfor a; do case $a in *.tmp*) echo mkvmerge > \"$a\" ;; esac; done\n"
        );
        if (subtitle)
        {
            AddSubtitleTrack(processFile);
        }
        WriteFakeFfProbe(
            CatPackets("first", processFile.FfProbeProps.Video[0].Id, false),
            CatPackets("later", 0, false)
        );

        // Verify fails, the remux tier's verify fails, the FfMpeg re-encode fails, then HandBrake's output verifies clean
        string ffMpeg = Tools.FfMpeg.GetToolPath();
        FakeTools.WriteScript(
            ffMpeg,
            """
            step=$(($(cat "$0.step" 2>/dev/null || echo 0) + 1))
            echo $step > "$0.step"
            case $step in
            1|2|3) exit 1 ;;
            esac
            exit 0

            """
        );

        bool modified = false;
        _ = processFile.VerifyAndRepair(ref modified).Should().BeTrue();

        _ = File.ReadAllText(ffMpeg + ".step").Trim().Should().Be("4");
        _ = File.ReadAllText(handBrake + ".log")
            .Should()
            .Contain("--subtitle none")
            .And.Contain("name.tmp8");
        _ = File.ReadAllText(mkvPropEdit + ".log").Should().Contain("name.tmp12");
        string merge = File.ReadAllLines(mkvMerge + ".log")
            .First(line => line.Contains("name.tmp8"));
        _ = merge.Contains("name.mkv", StringComparison.Ordinal).Should().Be(subtitle);
        _ = File.ReadAllText(processFile.FileInfo.FullName).Should().StartWith("mkvmerge");
        _ = processFile.State.Should().HaveFlag(SidecarFile.StatesType.Repaired);
        _ = Directory.GetFiles(_directory, "name.tmp*").Should().BeEmpty();
        _ = modified.Should().BeTrue();
    }

    // The original and the rewritten file then both carry a subtitle track after the sample tracks
    private void AddSubtitleTrack(ProcessFile processFile)
    {
        long number = processFile.MkvMergeProps.Count + 1;
        processFile.MkvMergeProps.Subtitle.Add(
            new SubtitleProps(processFile.MkvMergeProps) { Number = number, Language = "eng" }
        );
        string json = Path.Combine(_directory, "MkvMergeInfoData.json");
        JsonNode info = JsonNode.Parse(File.ReadAllText(json))!;
        info["tracks"]!
            .AsArray()
            .Add(
                new JsonObject
                {
                    ["codec"] = "SubRip/SRT",
                    ["id"] = number - 1,
                    ["type"] = "subtitles",
                    ["properties"] = new JsonObject { ["number"] = number, ["language"] = "eng" },
                }
            );
        File.WriteAllText(json, info.ToJsonString());
    }

    [Fact]
    public void VerifyAndRepair_RemuxRepairAfterBitrateToolError_LeavesVerifiedUnset()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        ProcessFile processFile = CreateProcessFile(SidecarFile.StatesType.None);
        FakeTools.WriteSampleToolData(_directory);
        WriteFakeFfProbe("kill -9 $$", "kill -9 $$");

        // Verify fails, then the remux tier verifies clean
        string ffMpeg = Tools.FfMpeg.GetToolPath();
        FakeTools.WriteScript(
            ffMpeg,
            """
            step=$(($(cat "$0.step" 2>/dev/null || echo 0) + 1))
            echo $step > "$0.step"
            [ $step -eq 1 ] && exit 1
            exit 0

            """
        );

        bool modified = false;
        _ = processFile.VerifyAndRepair(ref modified).Should().BeTrue();

        _ = File.ReadAllText(ffMpeg + ".step").Trim().Should().Be("2");
        _ = File.ReadAllText(Tools.FfProbe.GetToolPath() + ".step").Trim().Should().Be("1");
        _ = ToolErrorScope.Count.Should().Be(1);
        _ = processFile.State.Should().Be(SidecarFile.StatesType.Repaired);
        _ = NextRun(processFile).State.Should().Be(SidecarFile.StatesType.Repaired);
        _ = modified.Should().BeTrue();
    }

    [Fact]
    public void RepairTimestamps_PacketAnalysisToolError_SkipsRepairLadder()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        ProcessFile processFile = CreateProcessFile(SidecarFile.StatesType.None);
        string mkvMerge = Tools.MkvMerge.GetToolPath();
        FakeTools.WriteScript(mkvMerge, "touch \"$0.ran\"\nexit 2\n");
        string ffMpeg = Tools.FfMpeg.GetToolPath();
        FakeTools.WriteScript(
            ffMpeg,
            """
            echo >> "$0.step"
            echo "non monotonically increasing dts to muxer" >&2
            exit 0

            """
        );

        // FfProbe has no fake, so the packet analysis fails to launch
        bool modified = false;
        _ = processFile.RepairTimestamps(ref modified).Should().BeFalse();

        _ = ToolErrorScope.Count.Should().BeGreaterThan(0);
        _ = File.ReadAllLines(ffMpeg + ".step").Should().HaveCount(1);
        _ = File.Exists(mkvMerge + ".ran").Should().BeFalse();
        _ = processFile.State.Should().Be(SidecarFile.StatesType.None);
        _ = processFile.DeleteFailedFile().Should().BeFalse();
        _ = modified.Should().BeFalse();
    }
}
