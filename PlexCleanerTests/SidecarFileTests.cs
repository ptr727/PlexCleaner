using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

// Read the JSON file but do not verify the MKV media attributes
// TODO: Use media files that match the JSON, currently dummy files

public class SidecarFileTests(PlexCleanerFixture fixture)
{
    [Theory]
    [InlineData("Sidecar.v1.mkv")]
    [InlineData("Sidecar.v2.mkv")]
    [InlineData("Sidecar.v3.mkv")]
    [InlineData("Sidecar.v4.mkv")]
    [InlineData("Sidecar.v5.mkv")]
    public void Open_Old_Schema_Open(string fileName)
    {
        SidecarFile sidecarFile = new(fixture.GetSampleFilePath(fileName));
        Assert.True(sidecarFile.Read(out _, false));

        Assert.True(sidecarFile.FfProbeProps.Audio.Count > 0);
        Assert.True(sidecarFile.FfProbeProps.Audio.Count > 0);
        Assert.Equal(MediaTool.ToolType.FfProbe, sidecarFile.FfProbeProps.Parser);

        Assert.True(sidecarFile.MkvMergeProps.Audio.Count > 0);
        Assert.True(sidecarFile.MkvMergeProps.Video.Count > 0);
        Assert.Equal(MediaTool.ToolType.MkvMerge, sidecarFile.MkvMergeProps.Parser);

        Assert.True(sidecarFile.MediaInfoProps.Audio.Count > 0);
        Assert.True(sidecarFile.MediaInfoProps.Video.Count > 0);
        Assert.Equal(MediaTool.ToolType.MediaInfo, sidecarFile.MediaInfoProps.Parser);
    }
}
