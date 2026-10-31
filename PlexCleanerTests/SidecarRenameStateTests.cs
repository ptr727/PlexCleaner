using System.Text.Json.Nodes;
using AwesomeAssertions;
using PlexCleaner;
using ptr727.Utilities;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public sealed class SidecarRenameStateTests : IDisposable
{
    private const SidecarFile.StatesType Seeded =
        SidecarFile.StatesType.ReMuxed | SidecarFile.StatesType.FileReNamed;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly ToolsOptions1 _toolsOptions = Program.Config.ToolsOptions;
    private readonly MediaToolInfo _ffProbeInfo = Tools.FfProbe.Info;
    private readonly MediaToolInfo _mkvMergeInfo = Tools.MkvMerge.Info;
    private readonly MediaToolInfo _mediaInfoInfo = Tools.MediaInfo.Info;

    public SidecarRenameStateTests()
    {
        _ = Directory.CreateDirectory(_directory);
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
        Directory.Delete(_directory, true);
    }

    private static int ReadState(string sidecarPath) =>
        JsonNode.Parse(File.ReadAllText(sidecarPath))!["State"]!.GetValue<int>();

    [Fact]
    public void Open_SeededStateOverMatchingSidecar_KeepsBothStates()
    {
        // The sidecar name ignores the extension case, so the hash matches
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        string sidecarPath = Path.Combine(_directory, "name.PlexCleaner");
        JsonNode stored = JsonNode.Parse(File.ReadAllText(sidecarPath))!;
        stored["State"] = (int)SidecarFile.StatesType.Verified;
        File.WriteAllText(sidecarPath, stored.ToJsonString());
        string renamed = Path.Combine(_directory, "name.MKV");
        string tempName = Path.ChangeExtension(mediaFile, ".tmp7");
        File.Move(mediaFile, tempName);
        File.Move(tempName, renamed);

        SidecarFile sidecarFile = new(renamed) { State = Seeded };
        _ = sidecarFile.Open().Should().BeTrue();

        SidecarFile.StatesType expected = Seeded | SidecarFile.StatesType.Verified;
        _ = sidecarFile.State.Should().Be(expected);
        _ = ReadState(sidecarPath).Should().Be((int)expected);
    }

    [Fact]
    public void Open_SeededStateOverStaleSidecar_KeepsSeededAndMarksModified()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake tools are shell scripts");
        // A remuxed file meets the stale sidecar of the file it replaced
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        string sidecarPath = Path.Combine(_directory, "name.PlexCleaner");
        JsonNode stored = JsonNode.Parse(File.ReadAllText(sidecarPath))!;
        stored["State"] = (int)SidecarFile.StatesType.Verified;
        File.WriteAllText(sidecarPath, stored.ToJsonString());
        File.AppendAllText(mediaFile, "changed");

        // Tools return the stored sample data so the media update can run
        foreach (
            (MediaTool tool, string key) in new (MediaTool, string)[]
            {
                (Tools.MediaInfo, "MediaInfoData"),
                (Tools.MkvMerge, "MkvMergeInfoData"),
                (Tools.FfProbe, "FfProbeInfoData"),
            }
        )
        {
            string dataFile = Path.Combine(_directory, key + ".json");
            File.WriteAllText(
                dataFile,
                StringCompression.Decompress(stored[key]!.GetValue<string>())
            );
            FakeTools.WriteScript(tool.GetToolPath(), $"cat \"{dataFile}\"\n");
        }

        SidecarFile sidecarFile = new(mediaFile) { State = Seeded };
        _ = sidecarFile.Open(true).Should().BeTrue();

        // The stale Verified flag is cleared, the seeded flags are kept
        SidecarFile.StatesType expected = Seeded | SidecarFile.StatesType.FileModified;
        _ = sidecarFile.State.Should().Be(expected);
        _ = ReadState(sidecarPath).Should().Be((int)expected);
    }
}
