using PlexCleaner;
using Xunit;
using ConfigFileJsonSchema = PlexCleaner.ConfigFileJsonSchema4;

namespace PlexCleanerTests;

public class ConfigFileTests(PlexCleanerFixture assemblyFixture) : SamplesFixture
{
    [Theory]
    [InlineData("PlexCleaner.v1.json", ConfigFileJsonSchema1.Version)]
    [InlineData("PlexCleaner.v2.json", ConfigFileJsonSchema2.Version)]
    [InlineData("PlexCleaner.v3.json", ConfigFileJsonSchema3.Version)]
    [InlineData("PlexCleaner.v4.json", ConfigFileJsonSchema.Version)]
    public void Open_Old_Schema_Open(string fileName, int expectedDeserializedVersion)
    {
        // Deserialize
        ConfigFileJsonSchema configFileJsonSchema = ConfigFileJsonSchema.FromFile(
            assemblyFixture.GetSampleFilePath(fileName)
        );
        Assert.NotNull(configFileJsonSchema);

        // Verify schema was upgraded to current version
        Assert.Equal(ConfigFileJsonSchema.Version, configFileJsonSchema.SchemaVersion);

        // Verify DeserializedVersion reflects the original version
        Assert.Equal(expectedDeserializedVersion, configFileJsonSchema.DeserializedVersion);

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
    }

    [Theory]
    [InlineData("PlexCleaner.v1.json", ConfigFileJsonSchema1.Version)]
    [InlineData("PlexCleaner.v2.json", ConfigFileJsonSchema2.Version)]
    [InlineData("PlexCleaner.v3.json", ConfigFileJsonSchema3.Version)]
    [InlineData("PlexCleaner.v4.json", ConfigFileJsonSchema.Version)]
    public void Open_Old_Schema_Upgrade(string fileName, int expectedDeserializedVersion)
    {
        // Load config file schema and upgrade on disk
        ConfigFileJsonSchema configSchema = ConfigFileJsonSchema.OpenAndUpgrade(
            GetSampleFilePath(fileName)
        );
        Assert.NotNull(configSchema);

        // Verify schema was upgraded to current version
        Assert.Equal(ConfigFileJsonSchema.Version, configSchema.SchemaVersion);

        // Verify DeserializedVersion reflects the original version
        Assert.Equal(expectedDeserializedVersion, configSchema.DeserializedVersion);

        // Re-open the file to verify it was saved in current version
        configSchema = ConfigFileJsonSchema.FromFile(GetSampleFilePath(fileName));
        Assert.NotNull(configSchema);

        // Verify schema was upgraded to current version
        Assert.Equal(ConfigFileJsonSchema.Version, configSchema.SchemaVersion);

        // Verify DeserializedVersion reflects the current version
        Assert.Equal(ConfigFileJsonSchema.Version, configSchema.DeserializedVersion);
    }
}
