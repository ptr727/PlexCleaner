using System.Text.Json.Nodes;
using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public sealed class ReplaceOriginalTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly ToolsOptions1 _toolsOptions = Program.Config.ToolsOptions;
    private readonly bool _useSidecarFiles = Program.Config.ProcessOptions.UseSidecarFiles;
    private readonly HashSet<string> _reEncodeAudioFormats = Program
        .Config
        .ProcessOptions
        .ReEncodeAudioFormats;
    private readonly HashSet<string> _reMuxExtensions = Program
        .Config
        .ProcessOptions
        .ReMuxExtensions;
    private readonly MediaToolInfo _ffProbeInfo = Tools.FfProbe.Info;
    private readonly MediaToolInfo _mkvMergeInfo = Tools.MkvMerge.Info;
    private readonly MediaToolInfo _mediaInfoInfo = Tools.MediaInfo.Info;

    public ReplaceOriginalTests()
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
        Program.Config.ProcessOptions.ReEncodeAudioFormats = _reEncodeAudioFormats;
        Program.Config.ProcessOptions.ReMuxExtensions = _reMuxExtensions;
        Directory.Delete(_directory, true);
    }

    private const SidecarFile.StatesType VerifiedState =
        SidecarFile.StatesType.Verified
        | SidecarFile.StatesType.Repaired
        | SidecarFile.StatesType.BitrateExceeded;

    private ProcessFile CreateProcessFile(
        SidecarFile.StatesType state = SidecarFile.StatesType.None
    )
    {
        ProcessFile processFile = new(FakeTools.CopyUnverifiedSample(_directory, state));
        _ = processFile.GetMediaProps().Should().BeTrue();
        return processFile;
    }

    // Each fake writes its tool name into every argument matching the patterns, MkvPropEdit logs the file it edits
    private string WriteFakeTools(string tempOutputs, int propEditExit, int mkvMergeExit = 0)
    {
        string write =
            $"for a; do a=${{a#*=}}; case $a in {tempOutputs}) echo $(basename \"$0\") > \"$a\" ;; esac; done\n";
        FakeTools.WriteScript(Tools.FfMpeg.GetToolPath(), write);
        FakeTools.WriteScript(
            Tools.MkvMerge.GetToolPath(),
            FakeTools.SampleIdentifyCase(_directory) + write + $"exit {mkvMergeExit}\n"
        );
        FakeTools.WriteScript(Tools.HandBrake.GetToolPath(), write);
        string mkvPropEdit = Tools.MkvPropEdit.GetToolPath();
        FakeTools.WriteScript(mkvPropEdit, $"echo \"$@\" > \"$0.log\"\nexit {propEditExit}\n");
        return mkvPropEdit + ".log";
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, 2, false)]
    [InlineData(2, 0, false)]
    public void ConvertToMkv_ReplacesInputOnlyAfterEveryStep(
        int mkvMergeExit,
        int propEditExit,
        bool success
    )
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        ProcessFile processFile = CreateProcessFile();
        string mediaFile = processFile.FileInfo.FullName;
        byte[] original = File.ReadAllBytes(mediaFile);
        string propEditLog = WriteFakeTools("*.tmp1|*.tmp4", propEditExit, mkvMergeExit);

        bool converted = PlexCleaner.Convert.ConvertToMkv(
            mediaFile,
            new SelectMediaProps(processFile.FfProbeProps, true),
            processFile.MkvMergeProps,
            out string outputName
        );

        _ = converted.Should().Be(success);
        _ = outputName.Should().Be(mediaFile);
        if (mkvMergeExit == 0)
        {
            string[] propEditArgs = File.ReadAllText(propEditLog)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            _ = propEditArgs.Should().Contain(arg => arg.EndsWith("name.tmp1"));
            List<TrackProps> tracks = processFile
                .MkvMergeProps.GetTrackList()
                .FindAll(track => !Language.IsUndefined(track.LanguageAny));
            _ = tracks.Should().NotBeEmpty();
            _ = propEditArgs.Count(arg => arg == "--edit").Should().Be(tracks.Count);
            string joined = $" {string.Join(' ', propEditArgs)} ";
            foreach (TrackProps track in tracks)
            {
                _ = joined
                    .Should()
                    .Contain($" --edit track:@{track.Number} --set language={track.LanguageAny} ");
            }
        }
        else
        {
            _ = File.Exists(propEditLog).Should().BeFalse();
        }
        if (success)
        {
            _ = File.ReadAllText(mediaFile).Should().StartWith("mkvmerge");
        }
        else
        {
            _ = File.ReadAllBytes(mediaFile).Should().Equal(original);
        }
        _ = Directory.GetFiles(_directory, "name.tmp*").Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, false)]
    public void DeInterlace_ReplacesOriginalOnlyAfterLanguageRestore(int propEditExit, bool success)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        ProcessFile processFile = CreateProcessFile(VerifiedState);
        string mediaFile = processFile.FileInfo.FullName;
        byte[] original = File.ReadAllBytes(mediaFile);
        string propEditLog = WriteFakeTools("*.tmp8|*.tmp9", propEditExit);
        FakeTools.WriteSampleToolData(_directory);
        processFile.FfProbeProps.Video[0].Interlaced = true;

        bool modified = false;
        bool deInterlaced = processFile.DeInterlace(false, ref modified);

        _ = deInterlaced.Should().Be(success);
        _ = modified.Should().Be(success);
        _ = File.ReadAllText(propEditLog).Should().Contain("name.tmp9");
        if (success)
        {
            SidecarFile.StatesType expected =
                SidecarFile.StatesType.DeInterlaced
                | SidecarFile.StatesType.ReMuxed
                | SidecarFile.StatesType.Repaired;
            _ = File.ReadAllText(mediaFile).Should().StartWith("mkvmerge");
            _ = processFile.State.Should().Be(expected);
            _ = FakeTools
                .ReadSidecarState(SidecarFile.GetSidecarName(processFile.FileInfo))
                .Should()
                .Be((int)expected);
        }
        else
        {
            _ = File.ReadAllBytes(mediaFile).Should().Equal(original);
            _ = processFile.State.Should().Be(VerifiedState);
        }
        _ = Directory.GetFiles(_directory, "name.tmp*").Should().BeEmpty();
    }

    // The packets carry a non-monotonic DTS on the audio stream only, so the lossless tier runs
    private string WriteTimestampProbeData(long audioId)
    {
        JsonArray packets = [];
        foreach (double dts in new[] { 1.0, 0.5 })
        {
            packets.Add(
                new JsonObject
                {
                    ["codec_type"] = "audio",
                    ["stream_index"] = audioId,
                    ["pts_time"] = dts,
                    ["dts_time"] = dts,
                    ["duration_time"] = 0.5,
                    ["size"] = 100,
                }
            );
        }
        string packetsFile = Path.Combine(_directory, "packets.json");
        File.WriteAllText(packetsFile, new JsonObject { ["packets"] = packets }.ToJsonString());

        JsonArray streams = [];
        foreach ((int index, string type) in new[] { (0, "video"), (1, "audio") })
        {
            streams.Add(
                new JsonObject
                {
                    ["index"] = index,
                    ["codec_type"] = type,
                    ["start_time"] = "0.000000",
                    ["duration"] = "4.000000",
                }
            );
        }
        string timingsFile = Path.Combine(_directory, "timings.json");
        File.WriteAllText(timingsFile, new JsonObject { ["streams"] = streams }.ToJsonString());

        string props = Path.Combine(_directory, "FfProbeInfoData.json");
        return $$"""
            case "$*" in
            *-show_packets*) cat "{{packetsFile}}" ;;
            *start_time*) cat "{{timingsFile}}" ;;
            *) cat "{{props}}" ;;
            esac

            """;
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, false)]
    public void RepairTimestamps_LosslessRepair_ReplacesOriginalOnlyAfterLanguageRestore(
        int propEditExit,
        bool success
    )
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        ProcessFile processFile = CreateProcessFile();
        string mediaFile = processFile.FileInfo.FullName;
        byte[] original = File.ReadAllBytes(mediaFile);
        FakeTools.WriteSampleToolData(_directory);
        FakeTools.WriteScript(
            Tools.FfProbe.GetToolPath(),
            WriteTimestampProbeData(processFile.FfProbeProps.Audio[0].Id)
        );
        FakeTools.WriteScript(
            Tools.MkvMerge.GetToolPath(),
            FakeTools.SampleIdentifyCase(_directory)
                + "for a; do case $a in *.tmp4|*.tmp11) echo mkvmerge > \"$a\" ;; esac; done\n"
        );
        FakeTools.WriteScript(Tools.HandBrake.GetToolPath(), "exit 1\n");

        // Only the setts output verifies clean, so a later tier cannot replace the original
        FakeTools.WriteScript(
            Tools.FfMpeg.GetToolPath(),
            """
            case "$*" in
            *setts*) for a; do case $a in *.tmp14) echo ffmpeg > "$a" ;; esac; done ;;
            *streamhash*) printf '0,video,MD5=00\n1,audio,MD5=11\n' ;;
            *-xerror*name.tmp14*) ;;
            *-xerror*) echo "non monotonically increasing dts to muxer" >&2 ;;
            *) exit 1 ;;
            esac

            """
        );

        // The edit logs the file it edits with its content, which is gone once the move ran
        string mkvPropEdit = Tools.MkvPropEdit.GetToolPath();
        FakeTools.WriteScript(
            mkvPropEdit,
            "for a; do case $a in *.tmp14) echo \"$(basename \"$a\") $(cat \"$a\")\" >> \"$0.log\" ;; esac; done\n"
                + $"exit {propEditExit}\n"
        );

        bool modified = false;
        bool repaired = processFile.RepairTimestamps(ref modified);

        _ = repaired.Should().Be(success);
        _ = modified.Should().Be(success);
        _ = ToolErrorScope.Count.Should().Be(0);
        _ = File.ReadAllLines(mkvPropEdit + ".log").Should().Equal("name.tmp14 mkvmerge");
        SidecarFile.StatesType expected = success
            ? SidecarFile.StatesType.Verified | SidecarFile.StatesType.Repaired
            : SidecarFile.StatesType.VerifyFailed | SidecarFile.StatesType.RepairFailed;
        _ = processFile.State.Should().Be(expected);
        _ = FakeTools
            .ReadSidecarState(SidecarFile.GetSidecarName(processFile.FileInfo))
            .Should()
            .Be((int)expected);
        if (success)
        {
            _ = File.ReadAllText(mediaFile).Should().StartWith("mkvmerge");
        }
        else
        {
            _ = File.ReadAllBytes(mediaFile).Should().Equal(original);
        }
        _ = Directory.GetFiles(_directory, "name.tmp*").Should().BeEmpty();
    }

    [Theory]
    [InlineData(VerifiedState)]
    [InlineData(
        SidecarFile.StatesType.VerifyFailed
            | SidecarFile.StatesType.RepairFailed
            | SidecarFile.StatesType.PostDecodeTimestamps
    )]
    public void ReEncode_ReplacesOriginal_ClearsVerifyStates(SidecarFile.StatesType state)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        ProcessFile processFile = CreateProcessFile(state);
        _ = WriteFakeTools("*.tmp1", 0);
        FakeTools.WriteSampleToolData(_directory);
        Program.Config.ProcessOptions.ReEncodeAudioFormats = new HashSet<string>(
            [processFile.FfProbeProps.Audio[0].Format],
            StringComparer.OrdinalIgnoreCase
        );

        bool modified = false;
        _ = processFile.ReEncode(false, ref modified).Should().BeTrue();

        SidecarFile.StatesType expected =
            SidecarFile.StatesType.ReEncoded
            | SidecarFile.StatesType.ReMuxed
            | (state & SidecarFile.StatesType.Repaired);
        _ = modified.Should().BeTrue();
        _ = processFile.State.Should().Be(expected);
        _ = FakeTools
            .ReadSidecarState(SidecarFile.GetSidecarName(processFile.FileInfo))
            .Should()
            .Be((int)expected);
    }

    [Theory]
    [InlineData("none", true)]
    [InlineData("number", false)]
    [InlineData("type", false)]
    [InlineData("language", false)]
    [InlineData("dropped", false)]
    public void RestoreLanguageTags_EditsOnlyWhenTracksMatch(string change, bool success)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        ProcessFile processFile = CreateProcessFile();
        string propEditLog = WriteFakeTools("*.tmp1", 0);
        string tempName = Path.ChangeExtension(processFile.FileInfo.FullName, ".tmp1");
        File.WriteAllText(tempName, "ffmpeg");
        MediaProps languageProps = processFile.MkvMergeProps;
        VideoProps video = languageProps.Video[0];
        switch (change)
        {
            case "number":
                video.Number += 100;
                break;
            case "type":
                _ = languageProps.Video.Remove(video);
                languageProps.Audio.Add(
                    new AudioProps(languageProps)
                    {
                        Number = video.Number,
                        Language = video.Language,
                    }
                );
                break;
            case "language":
                video.Language = "zxx";
                break;
            case "dropped":
                languageProps.Audio.RemoveAt(0);
                break;
            default:
                break;
        }

        _ = PlexCleaner.Convert.RestoreLanguageTags(tempName, languageProps).Should().Be(success);

        _ = File.Exists(propEditLog).Should().Be(success);
        if (success)
        {
            _ = File.ReadAllText(propEditLog).Should().Contain("name.tmp1");
        }
    }

    [Fact]
    public void GetHandBrakeTrackOrder_NumbersCopiesByType_LeavesOriginal()
    {
        MediaProps original = new(MediaTool.ToolType.MkvMerge, "name.mkv");
        original.Video.Add(new VideoProps(original) { Number = 1, Language = "eng" });
        original.Subtitle.Add(
            new SubtitleProps(original)
            {
                Number = 2,
                Language = "fre",
                LanguageIetf = "fr-CA",
            }
        );
        original.Audio.Add(
            new AudioProps(original)
            {
                Number = 3,
                Language = "ger",
                LanguageIetf = "de-CH",
            }
        );

        MediaProps ordered = PlexCleaner.Convert.GetHandBrakeTrackOrder(original);

        _ = ordered
            .GetTrackList()
            .Select(item => (item.Type, item.Number, item.LanguageAny))
            .Should()
            .Equal(
                (TrackProps.TrackType.Video, 1L, "eng"),
                (TrackProps.TrackType.Audio, 2L, "de-CH"),
                (TrackProps.TrackType.Subtitle, 3L, "fr-CA")
            );
        _ = original.GetTrackList().Select(item => item.Number).Should().Equal(1L, 3L, 2L);
    }

    [Theory]
    [InlineData("exit 2\n")]
    [InlineData("echo '{'\n")]
    public void RestoreLanguageTags_IdentifyFails_ReturnsFalse(string mkvMerge)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        ProcessFile processFile = CreateProcessFile();
        string propEditLog = WriteFakeTools("*.tmp1", 0);
        FakeTools.WriteScript(Tools.MkvMerge.GetToolPath(), mkvMerge);
        string tempName = Path.ChangeExtension(processFile.FileInfo.FullName, ".tmp1");
        File.WriteAllText(tempName, "ffmpeg");

        _ = PlexCleaner
            .Convert.RestoreLanguageTags(tempName, processFile.MkvMergeProps)
            .Should()
            .BeFalse();
        _ = File.Exists(propEditLog).Should().BeFalse();
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, false)]
    public void RemoveClosedCaptions_ReplacesOriginalOnlyAfterLanguageRestore(
        int propEditExit,
        bool success
    )
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        ProcessFile processFile = CreateProcessFile();
        string mediaFile = processFile.FileInfo.FullName;
        byte[] original = File.ReadAllBytes(mediaFile);
        string propEditLog = WriteFakeTools("*.tmp10", propEditExit);
        FakeTools.WriteSampleToolData(_directory);
        processFile.FfProbeProps.Video[0].ClosedCaptions = true;
        processFile.FfProbeProps.Video[0].Format = "h264";

        bool modified = false;
        bool removed = processFile.RemoveClosedCaptions(false, ref modified);

        _ = removed.Should().Be(success);
        _ = modified.Should().Be(success);
        _ = File.ReadAllText(propEditLog).Should().Contain("name.tmp10");
        if (success)
        {
            _ = File.ReadAllText(mediaFile).Should().StartWith("mkvmerge");
            _ = processFile.State.Should().HaveFlag(SidecarFile.StatesType.ClearedCaptions);
        }
        else
        {
            _ = File.ReadAllBytes(mediaFile).Should().Equal(original);
            _ = processFile.State.Should().Be(SidecarFile.StatesType.None);
        }
        _ = Directory.GetFiles(_directory, "name.tmp*").Should().BeEmpty();
    }

    [Fact]
    public void RemuxByExtension_FfMpegFallbackBeforeMediaProps_RemuxesWithoutRestore()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
        string mkvFile = FakeTools.CopyUnverifiedSample(_directory);
        File.Delete(Path.ChangeExtension(mkvFile, ".PlexCleaner"));
        string aviFile = Path.ChangeExtension(mkvFile, ".avi");
        File.Move(mkvFile, aviFile);
        Program.Config.ProcessOptions.ReMuxExtensions = new HashSet<string>(
            [".avi"],
            StringComparer.OrdinalIgnoreCase
        );
        string propEditLog = WriteFakeTools("*.tmp2|*.tmp4", 0);
        FakeTools.WriteSampleToolData(_directory);

        // MkvMerge fails to remux the input, then remuxes the FfMpeg output
        FakeTools.WriteScript(
            Tools.MkvMerge.GetToolPath(),
            FakeTools.SampleIdentifyCase(_directory)
                + "case \"$*\" in *.tmp4*) ;; *) exit 2 ;; esac\n"
                + "for a; do case $a in *.tmp4) echo mkvmerge > \"$a\" ;; esac; done\n"
        );

        ProcessFile processFile = new(aviFile);
        bool modified = false;
        _ = processFile.RemuxByExtension(false, ref modified).Should().BeTrue();

        _ = modified.Should().BeTrue();
        _ = processFile.FileInfo.FullName.Should().Be(mkvFile);
        _ = File.ReadAllText(mkvFile).Should().StartWith("mkvmerge");
        _ = File.Exists(aviFile).Should().BeFalse();
        _ = File.Exists(propEditLog).Should().BeFalse();
        _ = Directory.GetFiles(_directory, "name.tmp*").Should().BeEmpty();
    }

    [Theory]
    [InlineData("Matroska", true)]
    [InlineData("AVI", false)]
    public void ReMuxToMkv_FfMpegFallback_RestoresLanguageOnlyForMatroska(
        string container,
        bool restored
    )
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        ProcessFile processFile = CreateProcessFile();
        string mediaFile = processFile.FileInfo.FullName;
        string propEditLog = WriteFakeTools("*.tmp2|*.tmp4", 0);
        processFile.MkvMergeProps.Container = container;

        // MkvMerge fails to remux the input, then remuxes the FfMpeg output
        FakeTools.WriteScript(
            Tools.MkvMerge.GetToolPath(),
            FakeTools.SampleIdentifyCase(_directory)
                + "case \"$*\" in *.tmp4*) ;; *) exit 2 ;; esac\n"
                + "for a; do case $a in *.tmp4) echo mkvmerge > \"$a\" ;; esac; done\n"
        );

        _ = PlexCleaner
            .Convert.ReMuxToMkv(mediaFile, processFile.MkvMergeProps, out string outputName)
            .Should()
            .BeTrue();

        _ = outputName.Should().Be(mediaFile);
        _ = File.ReadAllText(mediaFile).Should().StartWith("mkvmerge");
        _ = File.Exists(propEditLog).Should().Be(restored);
        if (restored)
        {
            _ = File.ReadAllText(propEditLog).Should().Contain("name.tmp2");
        }
        _ = Directory.GetFiles(_directory, "name.tmp*").Should().BeEmpty();
    }
}
