using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public sealed class SidecarToolInfoFailedTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly ToolsOptions1 _toolsOptions = Program.Config.ToolsOptions;
    private readonly MediaToolInfo _ffProbeInfo = Tools.FfProbe.Info;
    private readonly MediaToolInfo _mkvMergeInfo = Tools.MkvMerge.Info;
    private readonly MediaToolInfo _mediaInfoInfo = Tools.MediaInfo.Info;

    public SidecarToolInfoFailedTests()
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
}
