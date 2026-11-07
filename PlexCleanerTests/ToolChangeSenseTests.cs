using System.Text.Json.Nodes;
using AwesomeAssertions;
using PlexCleaner;
using Serilog.Events;
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

    [Theory]
    [InlineData("FfProbe")]
    [InlineData("MkvMerge")]
    [InlineData("MediaInfo")]
    public void IsMediaAndToolsCurrent_ToolChange_LogsTheChangedTool(string tool)
    {
        Program.Config.ProcessOptions.SidecarUpdateOnToolChange = true;
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        switch (tool)
        {
            case "FfProbe":
                Tools.FfProbe.Info = new MediaToolInfo(Tools.FfProbe) { Version = "99.0" };
                break;
            case "MkvMerge":
                Tools.MkvMerge.Info = new MediaToolInfo(Tools.MkvMerge) { Version = "99.0" };
                break;
            default:
                Tools.MediaInfo.Info = new MediaToolInfo(Tools.MediaInfo) { Version = "99.0" };
                break;
        }
        SidecarFile sidecarFile = new(mediaFile);
        _ = sidecarFile.Read(out _).Should().BeTrue();
        bool current = true;

        IReadOnlyList<LogEvent> events = LogCapture.Capture(() =>
            current = sidecarFile.IsMediaAndToolsCurrent(true)
        );

        _ = current.Should().BeFalse();
        _ = LogCapture
            .Messages(events, LogEventLevel.Warning)
            .Should()
            .ContainSingle(message => message.Contains("tool version mismatch"))
            .Which.Should()
            .Contain($"Sidecar {tool} tool version mismatch");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Read_ToolChange_ClearsProcessFailedOnlyWithSidecarUpdateOnToolChange(
        bool updateOnToolChange
    )
    {
        Program.Config.ProcessOptions.SidecarUpdateOnToolChange = updateOnToolChange;
        string mediaFile = FakeTools.CopyUnverifiedSample(
            _directory,
            SidecarFile.StatesType.Repaired | SidecarFile.StatesType.ProcessFailed
        );
        Tools.FfProbe.Info = new MediaToolInfo(Tools.FfProbe) { Version = "99.0" };

        SidecarFile sidecarFile = new(mediaFile);
        _ = sidecarFile.Read(out _).Should().BeTrue();

        _ = sidecarFile
            .State.HasFlag(SidecarFile.StatesType.ProcessFailed)
            .Should()
            .Be(!updateOnToolChange);
        _ = sidecarFile.State.Should().HaveFlag(SidecarFile.StatesType.Repaired);
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

        SidecarFile sidecarFile = new(mediaFile);
        _ = sidecarFile.Read(out _).Should().BeTrue();
        sidecarFile.State = SidecarFile.StatesType.Verified;
        _ = sidecarFile.Open().Should().BeTrue();

        JsonNode written = JsonNode.Parse(File.ReadAllText(sidecarPath))!;
        _ = written["State"]!.GetValue<int>().Should().Be((int)SidecarFile.StatesType.Verified);
        _ = written["FfProbeToolVersion"]!.GetValue<string>().Should().Be(storedVersion);
    }
}
