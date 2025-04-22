using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class SidecarFileTests(PlexCleanerFixture fixture)
{
    private readonly PlexCleanerFixture _fixture = fixture;

    [Theory]
    [InlineData("Sidecar.v1.mkv")]
    [InlineData("Sidecar.v2.mkv")]
    [InlineData("Sidecar.v3.mkv")]
    [InlineData("Sidecar.v4.mkv")]
    public void Open_Old_Schema_Open(string fileName)
    {
        SidecarFile sidecarFile = new(_fixture.GetSampleFilePath(fileName));
        // Read the JSON file but do not verify the MKV media attributes
        // TODO: Use media files that match the JSON, currently dummy files
        Assert.True(sidecarFile.Read(out _, false));

        // Test for expected config values
        Assert.True(sidecarFile.FfProbeInfo.Audio.Count > 0);
        Assert.True(sidecarFile.FfProbeInfo.Audio.Count > 0);
        Assert.Equal(MediaTool.ToolType.FfProbe, sidecarFile.FfProbeInfo.Parser);

        Assert.True(sidecarFile.MkvMergeInfo.Audio.Count > 0);
        Assert.True(sidecarFile.MkvMergeInfo.Video.Count > 0);
        Assert.Equal(MediaTool.ToolType.MkvMerge, sidecarFile.MkvMergeInfo.Parser);

        Assert.True(sidecarFile.MediaInfoInfo.Audio.Count > 0);
        Assert.True(sidecarFile.MediaInfoInfo.Video.Count > 0);
        Assert.Equal(MediaTool.ToolType.MediaInfo, sidecarFile.MediaInfoInfo.Parser);
    }
}
