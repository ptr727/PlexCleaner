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
        Assert.True(sidecarFile.Read(false));
    }
}
