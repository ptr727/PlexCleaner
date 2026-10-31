using System.Text.Json.Nodes;
using AwesomeAssertions;
using PlexCleaner;
using ptr727.Utilities;
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
        Directory.Delete(_directory, true);
    }

    private string CallsFile => Path.Combine(_directory, "calls.txt");

    private string[] Calls => File.Exists(CallsFile) ? File.ReadAllLines(CallsFile) : [];

    // The sidecar and the tools report the same edited media, and every edit is logged
    private ProcessFile CreateProcessFile(
        SidecarFile.StatesType state,
        Action<JsonNode>? mkvMerge = null,
        Action<JsonNode>? mediaInfo = null,
        string mkvPropEditBody = ""
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
                for a; do case $a in *.tmp*) echo mkvmerge > "$a" ;; esac; done ;;
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

        // A later run does not remux again either
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

    [Fact]
    public void RepairMetadataErrors_RepairOptionsOff_ContinuesWithoutEditing()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        Program.Config.ProcessOptions.ReMux = false;
        Program.Config.ProcessOptions.SetTrackFlags = false;
        ProcessFile processFile = CreateProcessFile(
            SidecarFile.StatesType.None,
            mkvMerge: data =>
            {
                // A language tag that does not match its IETF tag needs a remux
                JsonNode properties = AudioProperties(data);
                properties["language"] = "eng";
                properties["language_ietf"] = "de";
                TrackProperties(data, "video")["track_name"] = "Commentary";
            }
        );
        _ = processFile.HasMetadataErrors(TrackProps.StateType.ReMux).Should().BeTrue();
        _ = processFile.HasMetadataErrors(TrackProps.StateType.SetFlags).Should().BeTrue();

        bool modified = false;
        _ = processFile.RepairMetadataErrors(ref modified).Should().BeTrue();
        _ = modified.Should().BeFalse();
        _ = Calls.Should().BeEmpty();
        _ = processFile.State.Should().NotHaveFlag(SidecarFile.StatesType.VerifyFailed);
    }
}
