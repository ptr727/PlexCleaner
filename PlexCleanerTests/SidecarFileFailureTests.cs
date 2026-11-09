using System.Text.Json.Nodes;
using AwesomeAssertions;
using PlexCleaner;
using Serilog.Events;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public sealed class SidecarFileFailureTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly ToolsOptions1 _toolsOptions = Program.Config.ToolsOptions;
    private readonly MediaToolInfo _ffProbeInfo = Tools.FfProbe.Info;
    private readonly MediaToolInfo _mkvMergeInfo = Tools.MkvMerge.Info;
    private readonly MediaToolInfo _mediaInfoInfo = Tools.MediaInfo.Info;

    public SidecarFileFailureTests()
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

    private static string SidecarPath(string mediaFile) =>
        Path.ChangeExtension(mediaFile, ".PlexCleaner");

    [Fact]
    public void Read_Unparsable_Sidecar_Fails()
    {
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        File.WriteAllText(SidecarPath(mediaFile), "{ not json");
        SidecarFile sidecarFile = new(mediaFile);
        bool read = true;

        IReadOnlyList<LogEvent> events = LogCapture.Capture(() => read = sidecarFile.Read(out _));

        _ = read.Should().BeFalse();
        _ = LogCapture
            .Messages(events, LogEventLevel.Error)
            .Should()
            .Contain(message => message.Contains("Failed to read JSON from file"));
    }

    [Theory]
    [InlineData("MediaInfoData")]
    [InlineData("MkvMergeInfoData")]
    [InlineData("FfProbeInfoData")]
    public void Read_Sidecar_Missing_Tool_Data_Fails(string field)
    {
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        JsonNode sidecar = JsonNode.Parse(File.ReadAllText(SidecarPath(mediaFile)))!;
        sidecar[field] = string.Empty;
        File.WriteAllText(SidecarPath(mediaFile), sidecar.ToJsonString());
        SidecarFile sidecarFile = new(mediaFile);
        bool read = true;

        IReadOnlyList<LogEvent> events = LogCapture.Capture(() => read = sidecarFile.Read(out _));

        _ = read.Should().BeFalse();
        _ = LogCapture
            .Messages(events, LogEventLevel.Error)
            .Should()
            .ContainSingle(message => message.Contains("Media info tool data is missing"));
    }

    [Fact]
    public void Open_Sidecar_That_Cannot_Be_Written_Fails()
    {
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        SidecarFile sidecarFile = new(mediaFile);
        _ = sidecarFile.Read(out _).Should().BeTrue();
        File.Delete(SidecarPath(mediaFile));
        _ = Directory.CreateDirectory(SidecarPath(mediaFile));
        sidecarFile.State = SidecarFile.StatesType.Verified;
        bool opened = true;

        IReadOnlyList<LogEvent> events = LogCapture.Capture(() => opened = sidecarFile.Open());

        _ = opened.Should().BeFalse();
        _ = LogCapture
            .Messages(events, LogEventLevel.Error)
            .Should()
            .Contain(message => message.Contains("Failed to write JSON to file"));
    }

    [Fact]
    public void Create_AfterFailedToolRead_ClearsToolInfoFailed()
    {
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        FakeTools.WriteScript(Tools.MediaInfo.GetToolPath(), "exit 1\n");
        SidecarFile sidecarFile = new(mediaFile);

        _ = sidecarFile.Create().Should().BeFalse();
        _ = sidecarFile.ToolInfoFailed.Should().BeTrue();

        FakeTools.WriteSampleToolData(_directory);

        _ = sidecarFile.Create().Should().BeTrue();
        _ = sidecarFile.ToolInfoFailed.Should().BeFalse();
    }

    [Fact]
    public void Open_AfterFailedToolRead_WithCurrentSidecar_ClearsToolInfoFailed()
    {
        string mediaFile = FakeTools.CopyUnverifiedSample(_directory);
        FakeTools.WriteScript(Tools.MediaInfo.GetToolPath(), "exit 1\n");
        SidecarFile sidecarFile = new(mediaFile);
        _ = sidecarFile.Create().Should().BeFalse();
        _ = sidecarFile.ToolInfoFailed.Should().BeTrue();

        _ = sidecarFile.Open().Should().BeTrue();
        _ = sidecarFile.ToolInfoFailed.Should().BeFalse();
    }
}
