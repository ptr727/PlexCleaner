using PlexCleaner;
using Xunit;
using SidecarFileJsonSchema = PlexCleaner.SidecarFileJsonSchema5;

namespace PlexCleanerTests;

public class SidecarFileTests(PlexCleanerFixture assemblyFixture) : SamplesFixture
{
    [Theory]
    [InlineData("Sidecar.v1.PlexCleaner", SidecarFileJsonSchema1.Version)]
    [InlineData("Sidecar.v2.PlexCleaner", SidecarFileJsonSchema2.Version)]
    [InlineData("Sidecar.v3.PlexCleaner", SidecarFileJsonSchema3.Version)]
    [InlineData("Sidecar.v4.PlexCleaner", SidecarFileJsonSchema4.Version)]
    [InlineData("Sidecar.v5.PlexCleaner", SidecarFileJsonSchema.Version)]
    public void Open_Old_Schema_Open(string fileName, int expectedDeserializedVersion)
    {
        // Load sidecar file schema directly
        SidecarFileJsonSchema sidecarSchema = SidecarFileJsonSchema.FromFile(
            GetSampleFilePath(fileName)
        );
        Assert.NotNull(sidecarSchema);

        // Verify schema was upgraded to current version
        Assert.Equal(SidecarFileJsonSchema.Version, sidecarSchema.SchemaVersion);

        // Verify DeserializedVersion reflects the original version
        Assert.Equal(expectedDeserializedVersion, sidecarSchema.DeserializedVersion);
    }

    [Theory]
    [InlineData("Sidecar.v1.PlexCleaner", SidecarFileJsonSchema1.Version)]
    [InlineData("Sidecar.v2.PlexCleaner", SidecarFileJsonSchema2.Version)]
    [InlineData("Sidecar.v3.PlexCleaner", SidecarFileJsonSchema3.Version)]
    [InlineData("Sidecar.v4.PlexCleaner", SidecarFileJsonSchema4.Version)]
    [InlineData("Sidecar.v5.PlexCleaner", SidecarFileJsonSchema.Version)]
    public void Open_Old_Schema_Upgrade(string fileName, int expectedDeserializedVersion)
    {
        // Load sidecar file schema and upgrade on disk
        SidecarFileJsonSchema sidecarSchema = SidecarFileJsonSchema.OpenAndUpgrade(
            GetSampleFilePath(fileName)
        );
        Assert.NotNull(sidecarSchema);

        // Verify schema was upgraded to current version
        Assert.Equal(SidecarFileJsonSchema.Version, sidecarSchema.SchemaVersion);

        // Verify DeserializedVersion reflects the original version
        Assert.Equal(expectedDeserializedVersion, sidecarSchema.DeserializedVersion);

        // Re-open the file to verify it was saved in current version
        sidecarSchema = SidecarFileJsonSchema.FromFile(GetSampleFilePath(fileName));
        Assert.NotNull(sidecarSchema);

        // Verify schema was upgraded to current version
        Assert.Equal(SidecarFileJsonSchema.Version, sidecarSchema.SchemaVersion);

        // Verify DeserializedVersion reflects the current version
        Assert.Equal(SidecarFileJsonSchema.Version, sidecarSchema.DeserializedVersion);
    }

    [Theory]
    [InlineData("Sidecar.v1.mkv")]
    [InlineData("Sidecar.v2.mkv")]
    [InlineData("Sidecar.v3.mkv")]
    [InlineData("Sidecar.v4.mkv")]
    [InlineData("Sidecar.v5.mkv")]
    public void Open_Old_File_Open(string fileName)
    {
        SidecarFile sidecarFile = new(GetSampleFilePath(fileName));
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
