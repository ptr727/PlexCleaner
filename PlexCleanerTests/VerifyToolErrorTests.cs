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
        Directory.Delete(_directory, true);
    }

    private string CreateFile(string name)
    {
        string fileName = Path.Combine(_directory, name);
        File.WriteAllText(fileName, "");
        return fileName;
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

    [Fact]
    public void VerifyAndRepair_ToolError_KeepsStateAndFiles()
    {
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
        string mediaFile = CreateFile("name.mkv");
        string sidecarFile = CreateFile("name.PlexCleaner");
        ProcessFile processFile = new(mediaFile);

        bool modified = false;
        _ = processFile.RepairTimestamps(ref modified).Should().BeFalse();
        _ = processFile.DeleteFailedFile().Should().BeFalse();

        _ = File.Exists(mediaFile).Should().BeTrue();
        _ = File.Exists(sidecarFile).Should().BeTrue();
    }
}
