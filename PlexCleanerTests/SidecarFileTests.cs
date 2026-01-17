using System;
using System.IO;
using PlexCleaner;
using Xunit;
using SidecarFileJsonSchema = PlexCleaner.SidecarFileJsonSchema5;

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
        // Create temp copies of both MKV and sidecar files to avoid modifying originals
        (string tempDirectory, Action cleanup) = fixture.CreateTempSampleFilesCopy(
            fileName,
            Path.ChangeExtension(fileName, ".PlexCleaner")
        );

        try
        {
            string tempMkvPath = Path.Combine(tempDirectory, fileName);

            SidecarFile sidecarFile = new(tempMkvPath);
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
        finally
        {
            cleanup();
        }
    }

    [Theory]
    [InlineData("Sidecar.v1.PlexCleaner", SidecarFileJsonSchema1.Version)]
    [InlineData("Sidecar.v2.PlexCleaner", SidecarFileJsonSchema2.Version)]
    [InlineData("Sidecar.v3.PlexCleaner", SidecarFileJsonSchema3.Version)]
    [InlineData("Sidecar.v4.PlexCleaner", SidecarFileJsonSchema4.Version)]
    [InlineData("Sidecar.v5.PlexCleaner", SidecarFileJsonSchema.Version)]
    public void Open_Old_Schema_Upgrades_To_Current_Version(
        string fileName,
        int expectedDeserializedVersion
    )
    {
        // Load sidecar file schema directly
        SidecarFileJsonSchema sidecarSchema = SidecarFileJsonSchema.FromFile(
            fixture.GetSampleFilePath(fileName)
        );
        Assert.NotNull(sidecarSchema);

        // Verify schema was upgraded to current version
        Assert.Equal(SidecarFileJsonSchema.Version, sidecarSchema.SchemaVersion);

        // Verify DeserializedVersion reflects the original version
        Assert.Equal(expectedDeserializedVersion, sidecarSchema.DeserializedVersion);
    }
}
