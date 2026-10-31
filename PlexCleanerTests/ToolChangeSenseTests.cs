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
    private readonly MediaToolInfo _ffProbeInfo = Tools.FfProbe.Info;
    private readonly MediaToolInfo _mkvMergeInfo = Tools.MkvMerge.Info;
    private readonly MediaToolInfo _mediaInfoInfo = Tools.MediaInfo.Info;

    public ToolChangeSenseTests() => _ = Directory.CreateDirectory(_directory);

    public void Dispose()
    {
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
}
