using PlexCleaner;
using Xunit;
using ConfigFileJsonSchema = PlexCleaner.ConfigFileJsonSchema4;

namespace PlexCleanerTests;

public class ConfigFileTests(PlexCleanerFixture fixture)
{
    [Theory]
    [InlineData("PlexCleaner.v1.json")]
    [InlineData("PlexCleaner.v2.json")]
    [InlineData("PlexCleaner.v3.json")]
    [InlineData("PlexCleaner.v4.json")]
    public void Open_Old_Schemas_Opens(string fileName)
    {
        // Deserialize
        ConfigFileJsonSchema configFileJsonSchema = ConfigFileJsonSchema.FromFile(
            fixture.GetSampleFilePath(fileName)
        );
        Assert.NotNull(configFileJsonSchema);

        // Test for expected config values
        Assert.Equal(@".\Tools\", configFileJsonSchema.ToolsOptions.RootPath);
        Assert.Equal(@".\Tools\", configFileJsonSchema.ToolsOptions.RootPath);
        Assert.Equal("ac3", configFileJsonSchema.ConvertOptions.FfMpegOptions.Audio);
        Assert.Empty(configFileJsonSchema.ConvertOptions.FfMpegOptions.Global);
        Assert.Equal(
            "copy --audio-fallback ac3",
            configFileJsonSchema.ConvertOptions.HandBrakeOptions.Audio
        );
        Assert.Contains("*.nfo", configFileJsonSchema.ProcessOptions.FileIgnoreMasks);
        Assert.Contains(".avi", configFileJsonSchema.ProcessOptions.ReMuxExtensions);
        Assert.Contains(
            new VideoFormat { Format = "mpeg2video" },
            configFileJsonSchema.ProcessOptions.ReEncodeVideo
        );
        Assert.Contains("flac", configFileJsonSchema.ProcessOptions.ReEncodeAudioFormats);
        Assert.Equal("en", configFileJsonSchema.ProcessOptions.DefaultLanguage);
        Assert.Contains("af", configFileJsonSchema.ProcessOptions.KeepLanguages);
        Assert.Contains("truehd", configFileJsonSchema.ProcessOptions.PreferredAudioFormats);
        Assert.Contains(
            @"\\server\Share\Series\Fiancé\Season 1\Fiancé - S01E01 - Bar.mkv",
            configFileJsonSchema.ProcessOptions.FileIgnoreList
        );
        Assert.Equal(100000000, configFileJsonSchema.VerifyOptions.MaximumBitrate);
        Assert.Equal(60, configFileJsonSchema.MonitorOptions.MonitorWaitTime);
    }

    [Theory]
    [InlineData("PlexCleaner.v1.json", ConfigFileJsonSchema1.Version)]
    [InlineData("PlexCleaner.v2.json", ConfigFileJsonSchema2.Version)]
    [InlineData("PlexCleaner.v3.json", ConfigFileJsonSchema3.Version)]
    [InlineData("PlexCleaner.v4.json", ConfigFileJsonSchema.Version)]
    public void Open_Old_Schemas_Upgrades_To_Current_Version(
        string fileName,
        int expectedDeserializedVersion
    )
    {
        // Deserialize
        ConfigFileJsonSchema configFileJsonSchema = ConfigFileJsonSchema.FromFile(
            fixture.GetSampleFilePath(fileName)
        );
        Assert.NotNull(configFileJsonSchema);

        // Verify schema was upgraded to current version
        Assert.Equal(ConfigFileJsonSchema.Version, configFileJsonSchema.SchemaVersion);

        // Verify DeserializedVersion reflects the original version
        Assert.Equal(expectedDeserializedVersion, configFileJsonSchema.DeserializedVersion);
    }
}
