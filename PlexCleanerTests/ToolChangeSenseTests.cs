using System.Text.Json.Nodes;
using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public sealed class ToolChangeSenseTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly bool _sidecarUpdateOnToolChange = Program
        .Config
        .ProcessOptions
        .SidecarUpdateOnToolChange;
    private readonly ToolsOptions1 _toolsOptions = Program.Config.ToolsOptions;
    private readonly MediaToolInfo _ffProbeInfo = Tools.FfProbe.Info;
    private readonly MediaToolInfo _mkvMergeInfo = Tools.MkvMerge.Info;
    private readonly MediaToolInfo _mediaInfoInfo = Tools.MediaInfo.Info;

    public ToolChangeSenseTests()
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
        Program.Config.ProcessOptions.SidecarUpdateOnToolChange = _sidecarUpdateOnToolChange;
        Directory.Delete(_directory, true);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void IsMediaAndToolsCurrent_ToolChange_FollowsSidecarUpdateOnToolChange(
        bool toolsChanged,
        bool updateOnToolChange,
        bool expectedCurrent
    )
    {
        Program.Config.ProcessOptions.SidecarUpdateOnToolChange = updateOnToolChange;
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        if (toolsChanged)
        {
            Tools.FfProbe.Info = new MediaToolInfo(Tools.FfProbe) { Version = "99.0" };
        }

        SidecarFile sidecarFile = new(mediaFile);
        _ = sidecarFile.Read(out _).Should().BeTrue();

        _ = sidecarFile.IsMediaAndToolsCurrent(false).Should().Be(expectedCurrent);
    }

    [Fact]
    public void Open_StateChangeAfterToolChange_KeepsStoredToolVersions()
    {
        Program.Config.ProcessOptions.SidecarUpdateOnToolChange = false;
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        string sidecarPath = Path.ChangeExtension(mediaFile, ".PlexCleaner");
        string storedVersion = JsonNode.Parse(File.ReadAllText(sidecarPath))![
            "FfProbeToolVersion"
        ]!.GetValue<string>();
        Tools.FfProbe.Info = new MediaToolInfo(Tools.FfProbe) { Version = "99.0" };

        // No tool is installed, so a tool rerun would fail the open
        SidecarFile sidecarFile = new(mediaFile);
        _ = sidecarFile.Read(out _).Should().BeTrue();
        sidecarFile.State = SidecarFile.StatesType.Verified;
        _ = sidecarFile.Open().Should().BeTrue();

        JsonNode written = JsonNode.Parse(File.ReadAllText(sidecarPath))!;
        _ = written["State"]!.GetValue<int>().Should().Be((int)SidecarFile.StatesType.Verified);
        _ = written["FfProbeToolVersion"]!.GetValue<string>().Should().Be(storedVersion);
    }
}
