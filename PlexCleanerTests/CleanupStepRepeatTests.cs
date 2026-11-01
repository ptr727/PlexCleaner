using System.Text.Json.Nodes;
using AwesomeAssertions;
using PlexCleaner;
using ptr727.Utilities;
using Serilog.Events;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public sealed class CleanupStepRepeatTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly ToolsOptions1 _toolsOptions = Program.Config.ToolsOptions;
    private readonly bool _reMux = Program.Config.ProcessOptions.ReMux;
    private readonly bool _setTrackFlags = Program.Config.ProcessOptions.SetTrackFlags;
    private readonly bool _setIetfLanguageTags = Program.Config.ProcessOptions.SetIetfLanguageTags;
    private readonly bool _useSidecarFiles = Program.Config.ProcessOptions.UseSidecarFiles;
    private readonly bool _deInterlace = Program.Config.ProcessOptions.DeInterlace;
    private readonly bool _reEncode = Program.Config.ProcessOptions.ReEncode;
    private readonly bool _removeClosedCaptions = Program
        .Config
        .ProcessOptions
        .RemoveClosedCaptions;
    private readonly bool _verify = Program.Config.ProcessOptions.Verify;
    private readonly bool _autoRepair = Program.Config.VerifyOptions.AutoRepair;
    private readonly MediaToolInfo _ffProbeInfo = Tools.FfProbe.Info;
    private readonly MediaToolInfo _mkvMergeInfo = Tools.MkvMerge.Info;
    private readonly MediaToolInfo _mediaInfoInfo = Tools.MediaInfo.Info;

    public CleanupStepRepeatTests()
    {
        _ = Directory.CreateDirectory(_directory);
        Program.Config.ProcessOptions.ReMux = true;
        Program.Config.ProcessOptions.SetTrackFlags = true;
        Program.Config.ProcessOptions.SetIetfLanguageTags = true;
        Program.Config.ProcessOptions.UseSidecarFiles = true;
        Program.Config.VerifyOptions.AutoRepair = true;
        Program.Config.ProcessOptions.DeInterlace = false;
        Program.Config.ProcessOptions.ReEncode = false;
        Program.Config.ProcessOptions.RemoveClosedCaptions = false;
        Program.Config.ProcessOptions.Verify = false;

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
        Program.Config.ProcessOptions.ReMux = _reMux;
        Program.Config.ProcessOptions.SetTrackFlags = _setTrackFlags;
        Program.Config.ProcessOptions.SetIetfLanguageTags = _setIetfLanguageTags;
        Program.Config.ProcessOptions.UseSidecarFiles = _useSidecarFiles;
        Program.Config.VerifyOptions.AutoRepair = _autoRepair;
        Program.Config.ProcessOptions.DeInterlace = _deInterlace;
        Program.Config.ProcessOptions.ReEncode = _reEncode;
        Program.Config.ProcessOptions.RemoveClosedCaptions = _removeClosedCaptions;
        Program.Config.ProcessOptions.Verify = _verify;
        Directory.Delete(_directory, true);
    }

    private string CallsFile => Path.Combine(_directory, "calls.txt");

    private string[] Calls => File.Exists(CallsFile) ? File.ReadAllLines(CallsFile) : [];

    // The sidecar and the tools report the same edited media, and every edit is logged
    private ProcessFile CreateProcessFile(
        SidecarFile.StatesType state,
        Action<JsonNode>? mkvMerge = null,
        Action<JsonNode>? mediaInfo = null,
        string mkvPropEditBody = "",
        string mkvMergeRemuxBody = ""
    )
    {
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory, state);
        FakeTools.WriteSampleToolData(_directory);
        string sidecarPath = Path.Combine(_directory, "name.PlexCleaner");
        JsonNode sidecar = JsonNode.Parse(File.ReadAllText(sidecarPath))!;
        foreach (
            (string key, Action<JsonNode>? edit) in new (string, Action<JsonNode>?)[]
            {
                ("MkvMergeInfoData", mkvMerge),
                ("MediaInfoData", mediaInfo),
            }
        )
        {
            string dataFile = Path.Combine(_directory, key + ".json");
            File.Copy(dataFile, Path.Combine(_directory, key + ".orig.json"));
            JsonNode data = JsonNode.Parse(File.ReadAllText(dataFile))!;
            edit?.Invoke(data);
            File.WriteAllText(dataFile, data.ToJsonString());
            sidecar[key] = StringCompression.Compress(data.ToJsonString());
        }
        File.WriteAllText(sidecarPath, sidecar.ToJsonString());

        FakeTools.WriteScript(
            Tools.MkvMerge.GetToolPath(),
            $$"""
            case "$*" in
            *--identify*) cat "{{Path.Combine(_directory, "MkvMergeInfoData.json")}}" ;;
            *) echo mkvmerge >> "{{CallsFile}}"
                for a; do case $a in *.tmp*) echo mkvmerge > "$a" ;; esac; done
                {{mkvMergeRemuxBody}} ;;
            esac

            """
        );
        FakeTools.WriteScript(
            Tools.MkvPropEdit.GetToolPath(),
            $"echo mkvpropedit >> \"{CallsFile}\"\n{mkvPropEditBody}\n"
        );

        return Load(mediaFile);
    }

    private static ProcessFile Load(string mediaFile)
    {
        ProcessFile processFile = new(mediaFile);
        _ = processFile.GetMediaProps().Should().BeTrue();
        return processFile;
    }

    // MediaInfo reports an image track that MkvMerge does not list
    private static void AddMediaInfoCoverArt(JsonNode mediaInfo)
    {
        JsonArray tracks = mediaInfo["media"]!["track"]!.AsArray();
        JsonNode coverArt = tracks.First(item => (string?)item!["@type"] == "Video")!.DeepClone();
        coverArt["Format"] = "JPEG";
        coverArt["CodecID"] = "V_MJPEG";
        coverArt["ID"] = "3";
        coverArt["StreamOrder"] = "2";
        tracks.Add(coverArt);
    }

    private static JsonNode TrackProperties(JsonNode mkvMerge, string type) =>
        mkvMerge["tracks"]!.AsArray().First(item => (string?)item!["type"] == type)!["properties"]!;

    // A lone track with the Default flag is reported as redundant
    private static void ClearDefaultFlags(JsonNode mkvMerge)
    {
        foreach (JsonNode? track in mkvMerge["tracks"]!.AsArray())
        {
            track!["properties"]!["default_track"] = false;
        }
    }

    private static JsonNode AudioProperties(JsonNode mkvMerge) =>
        TrackProperties(mkvMerge, "audio");

    [Fact]
    public void RemoveCoverArt_MediaInfoCoverArtRemains_RemuxesOnceAcrossRuns()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        ProcessFile processFile = CreateProcessFile(
            SidecarFile.StatesType.None,
            mediaInfo: AddMediaInfoCoverArt
        );

        bool modified = false;
        _ = processFile.RemoveCoverArt(ref modified).Should().BeTrue();
        _ = modified.Should().BeTrue();
        _ = Calls.Should().Equal("mkvmerge");
        _ = processFile.State.Should().HaveFlag(SidecarFile.StatesType.RemovedCoverArt);

        // The cleanup re-run in the same run does not remux again
        modified = false;
        _ = processFile.RemoveCoverArt(ref modified).Should().BeTrue();
        _ = modified.Should().BeFalse();

        // A later run does not remux again either, and needs no ReMux option for that
        Program.Config.ProcessOptions.ReMux = false;
        ProcessFile nextRun = Load(processFile.FileInfo.FullName);
        _ = nextRun.RemoveCoverArt(ref modified).Should().BeTrue();
        _ = modified.Should().BeFalse();
        _ = Calls.Should().Equal("mkvmerge");
    }

    [Fact]
    public void RemoveCoverArt_MediaInfoCoverArtAttachment_RemovesInPlaceWithoutRemux()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");

        // Clearing the attachment also clears what MediaInfo reports
        ProcessFile processFile = CreateProcessFile(
            SidecarFile.StatesType.None,
            mkvMerge: data =>
                data["attachments"]!
                    .AsArray()
                    .Add(new JsonObject { ["content_type"] = "image/jpeg", ["id"] = 1 }),
            mediaInfo: AddMediaInfoCoverArt,
            mkvPropEditBody: $"cd \"{_directory}\" && for k in MkvMergeInfoData MediaInfoData; do cp $k.orig.json $k.json; done"
        );

        bool modified = false;
        _ = processFile.RemoveCoverArt(ref modified).Should().BeTrue();
        _ = modified.Should().BeTrue();
        _ = Calls.Should().Equal("mkvpropedit");
        _ = processFile.State.Should().HaveFlag(SidecarFile.StatesType.RemovedAttachments);
        _ = processFile.State.Should().NotHaveFlag(SidecarFile.StatesType.ReMuxed);
        _ = processFile.MediaInfoProps.HasCovertArt().Should().BeFalse();
    }

    [Fact]
    public void RepairMetadataErrors_TrackFlagsDoNotStick_SetsOnceAcrossRuns()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        ProcessFile processFile = CreateProcessFile(
            SidecarFile.StatesType.None,
            mkvMerge: data => AudioProperties(data)["track_name"] = "Commentary"
        );

        bool modified = false;
        _ = processFile.RepairMetadataErrors(ref modified).Should().BeTrue();
        _ = modified.Should().BeTrue();
        _ = Calls.Should().Equal("mkvpropedit");
        _ = processFile.State.Should().HaveFlag(SidecarFile.StatesType.SetFlags);

        // The cleanup re-run in the same run does not set them again
        modified = false;
        _ = processFile.RepairMetadataErrors(ref modified).Should().BeTrue();
        _ = modified.Should().BeFalse();

        // A later run does not set them again either
        ProcessFile nextRun = Load(processFile.FileInfo.FullName);
        _ = nextRun.RepairMetadataErrors(ref modified).Should().BeTrue();
        _ = modified.Should().BeFalse();
        _ = Calls.Should().Equal("mkvpropedit");
    }

    [Theory]
    [InlineData("ReMux", LogEventLevel.Warning)]
    [InlineData("SetIetfLanguageTags", LogEventLevel.Information)]
    [InlineData("SetTrackFlags", LogEventLevel.Warning)]
    public void RepairMetadataErrors_RepairOptionOff_ReportsAndContinuesWithoutEditing(
        string option,
        LogEventLevel level
    )
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        Program.Config.ProcessOptions.ReMux = option != "ReMux";
        Program.Config.ProcessOptions.SetIetfLanguageTags = option != "SetIetfLanguageTags";
        Program.Config.ProcessOptions.SetTrackFlags = option != "SetTrackFlags";
        ProcessFile processFile = CreateProcessFile(
            SidecarFile.StatesType.None,
            mkvMerge: data =>
            {
                JsonNode properties = AudioProperties(data);
                switch (option)
                {
                    // A language tag that does not match its IETF tag needs a remux
                    case "ReMux":
                        properties["language"] = "eng";
                        properties["language_ietf"] = "de";
                        break;
                    // A language tag with no IETF tag needs one set
                    case "SetIetfLanguageTags":
                        properties["language"] = "eng";
                        _ = properties.AsObject().Remove("language_ietf");
                        break;
                    default:
                        properties["track_name"] = "Commentary";
                        break;
                }
            }
        );

        bool modified = false;
        IReadOnlyList<LogEvent> events = LogCapture.Capture(() =>
            processFile.RepairMetadataErrors(ref modified).Should().BeTrue()
        );

        _ = modified.Should().BeFalse();
        _ = Calls.Should().BeEmpty();
        _ = processFile.State.Should().NotHaveFlag(SidecarFile.StatesType.VerifyFailed);
        _ = events
            .Should()
            .ContainSingle(item =>
                item.MessageTemplate.Text.Contains(option + " option not enabled")
            )
            .Which.Level.Should()
            .Be(level);
    }

    [Fact]
    public void RepairMetadataErrors_TrackNeedsRemuxAndFlags_ReportsBoth()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        Program.Config.ProcessOptions.ReMux = false;
        Program.Config.ProcessOptions.SetTrackFlags = false;
        ProcessFile processFile = CreateProcessFile(
            SidecarFile.StatesType.None,
            mkvMerge: data =>
            {
                JsonNode properties = AudioProperties(data);
                properties["language"] = "eng";
                properties["language_ietf"] = "de";
                properties["track_name"] = "Commentary";
            }
        );
        _ = processFile
            .MkvMergeProps.Audio.Single()
            .Actions.Should()
            .Be(TrackProps.ActionsType.ReMux | TrackProps.ActionsType.SetFlags);

        bool modified = false;
        IReadOnlyList<LogEvent> events = LogCapture.Capture(() =>
            processFile.RepairMetadataErrors(ref modified).Should().BeTrue()
        );

        _ = modified.Should().BeFalse();
        _ = Calls.Should().BeEmpty();
        _ = events
            .Should()
            .ContainSingle(item => item.MessageTemplate.Text.Contains("ReMux option not enabled"));
        _ = events
            .Should()
            .ContainSingle(item =>
                item.MessageTemplate.Text.Contains("SetTrackFlags option not enabled")
            );
    }

    [Fact]
    public void RepairMetadataErrors_TrackNeedsRemuxAndFlags_RemuxesThenSetsFlags()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");

        // The remux fixes the language tags and keeps the title that asks for a flag
        string remuxed = Path.Combine(_directory, "remuxed.json");
        string flagged = Path.Combine(_directory, "flagged.json");
        ProcessFile processFile = CreateProcessFile(
            SidecarFile.StatesType.None,
            mkvMerge: data =>
            {
                JsonNode properties = AudioProperties(data);
                properties["language"] = "eng";
                properties["language_ietf"] = "de";
                properties["track_name"] = "Commentary";
            },
            mkvMergeRemuxBody: $"cp \"{remuxed}\" \"{_directory}/MkvMergeInfoData.json\"",
            mkvPropEditBody: $"cp \"{flagged}\" \"{_directory}/MkvMergeInfoData.json\""
        );
        JsonNode mkvMerge = JsonNode.Parse(
            File.ReadAllText(Path.Combine(_directory, "MkvMergeInfoData.orig.json"))
        )!;
        AudioProperties(mkvMerge)["track_name"] = "Commentary";
        File.WriteAllText(remuxed, mkvMerge.ToJsonString());
        AudioProperties(mkvMerge)["flag_commentary"] = true;
        File.WriteAllText(flagged, mkvMerge.ToJsonString());

        bool modified = false;
        _ = processFile.RepairMetadataErrors(ref modified).Should().BeTrue();

        _ = modified.Should().BeTrue();
        _ = Calls.Should().Equal("mkvmerge", "mkvpropedit");
        _ = processFile
            .State.Should()
            .HaveFlag(SidecarFile.StatesType.ReMuxed)
            .And.HaveFlag(SidecarFile.StatesType.SetFlags)
            .And.NotHaveFlag(SidecarFile.StatesType.VerifyFailed);
        AudioProps audio = processFile.MkvMergeProps.Audio.Single();
        _ = audio.Flags.Should().HaveFlag(TrackProps.FlagsType.Commentary);
        _ = audio.Actions.Should().Be(TrackProps.ActionsType.None);
    }

    [Fact]
    public void RepairMetadataErrors_RewriteDropsFlagsInRun_SetsThemInCleanupReRun()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");

        // The remux stands in for a rewrite that drops a flag
        string dropped = Path.Combine(_directory, "dropped.json");
        ProcessFile processFile = CreateProcessFile(
            SidecarFile.StatesType.None,
            mediaInfo: AddMediaInfoCoverArt,
            mkvMergeRemuxBody: $"cp \"{dropped}\" \"{_directory}/MkvMergeInfoData.json\"; cp \"{_directory}/MediaInfoData.orig.json\" \"{_directory}/MediaInfoData.json\""
        );
        JsonNode mkvMerge = JsonNode.Parse(
            File.ReadAllText(Path.Combine(_directory, "MkvMergeInfoData.json"))
        )!;
        AudioProperties(mkvMerge)["track_name"] = "Commentary";
        File.WriteAllText(dropped, mkvMerge.ToJsonString());

        bool modified = false;
        _ = processFile.RepairMetadataErrors(ref modified).Should().BeTrue();
        _ = processFile.RemoveCoverArt(ref modified).Should().BeTrue();
        _ = processFile.HasMetadataErrors(TrackProps.ActionsType.SetFlags).Should().BeTrue();
        _ = processFile.RepairMetadataErrors(ref modified).Should().BeTrue();

        _ = Calls.Should().Equal("mkvmerge", "mkvpropedit");
    }

    [Fact]
    public void ProcessFile_RewriteDropsFlagsInRun_SetsThemInCleanupReRun()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");

        // The unknown language edit stands in for a rewrite that drops a flag
        string dropped = Path.Combine(_directory, "dropped.json");
        ProcessFile processFile = CreateProcessFile(
            SidecarFile.StatesType.None,
            mkvMerge: data =>
            {
                JsonNode properties = AudioProperties(data);
                properties["language"] = "und";
                properties["language_ietf"] = "und";
                ClearDefaultFlags(data);
            },
            mkvPropEditBody: $"if [ ! -e \"{_directory}/dropped.done\" ]; then touch \"{_directory}/dropped.done\"; cp \"{dropped}\" \"{_directory}/MkvMergeInfoData.json\"; fi"
        );
        JsonNode mkvMerge = JsonNode.Parse(
            File.ReadAllText(Path.Combine(_directory, "MkvMergeInfoData.json"))
        )!;
        AudioProperties(mkvMerge)["language"] = "eng";
        AudioProperties(mkvMerge)["language_ietf"] = "en";
        AudioProperties(mkvMerge)["track_name"] = "Commentary";
        File.WriteAllText(dropped, mkvMerge.ToJsonString());

        bool result = Process.ProcessFile(
            processFile.FileInfo.FullName,
            out _,
            out _,
            out _,
            out _,
            out string? failedOperation
        );

        _ = result.Should().BeTrue();
        _ = failedOperation.Should().BeNull();
        _ = Calls.Should().Equal("mkvpropedit", "mkvpropedit");
        _ = Load(processFile.FileInfo.FullName)
            .State.Should()
            .HaveFlag(SidecarFile.StatesType.SetFlags);
    }
}
