using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class SidecarFileTests
{
    [Theory]
    [InlineData("Sidecar.v1.mkv")]
    [InlineData("Sidecar.v2.mkv")]
    [InlineData("Sidecar.v3.mkv")]
    [InlineData("Sidecar.v4.mkv")]
    public void Open_OldSchemas_Opens(string fileName)
    {
        SidecarFile sidecarFile = new(SampleFiles.GetSampleFileInfo(fileName));
        // Read the JSON file but do not verify the MKV media attributes
        // TODO: Use a media file that matches the JSON
        Assert.True(sidecarFile.Read(out _, false));
    }
}
