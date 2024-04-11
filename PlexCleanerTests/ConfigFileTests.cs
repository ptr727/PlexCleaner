using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class ConfigFileTests : IClassFixture<PlexCleanerTests>
{
    [Theory]
    [InlineData("PlexCleaner.v1.json")]
    [InlineData("PlexCleaner.v2.json")]
    [InlineData("PlexCleaner.v3.json")]
    [InlineData("PlexCleaner.v4.json")]
    public void Open_OldSchemas_Opens(string fileName)
    {
        // Deserialize
        var configFileJsonSchema = ConfigFileJsonSchema.FromFile(PlexCleanerTests.GetSampleFilePath(fileName));
        Assert.NotNull(configFileJsonSchema);

        // Test for expected config values
        Assert.Equal(@".\Tools\", configFileJsonSchema.ToolsOptions.RootPath);
        Assert.Equal(@".\Tools\", configFileJsonSchema.ToolsOptions.RootPath);
        Assert.Equal("ac3", configFileJsonSchema.ConvertOptions.FfMpegOptions.Audio);
        Assert.Empty(configFileJsonSchema.ConvertOptions.FfMpegOptions.Global);
        Assert.Equal("copy --audio-fallback ac3", configFileJsonSchema.ConvertOptions.HandBrakeOptions.Audio);
        Assert.Contains("*.nfo", configFileJsonSchema.ProcessOptions.FileIgnoreMasks);
        Assert.Contains(".avi", configFileJsonSchema.ProcessOptions.ReMuxExtensions);
        Assert.Contains(new VideoFormat { Format = "mpeg2video" }, configFileJsonSchema.ProcessOptions.ReEncodeVideo);
        Assert.Contains("flac", configFileJsonSchema.ProcessOptions.ReEncodeAudioFormats);
        Assert.Equal("en", configFileJsonSchema.ProcessOptions.DefaultLanguage);
        Assert.Contains("af", configFileJsonSchema.ProcessOptions.KeepLanguages);
        Assert.Contains("truehd", configFileJsonSchema.ProcessOptions.PreferredAudioFormats);
        Assert.Contains(@"\\server\Share\Series\Fiancé\Season 1\Fiancé - S01E01 - Bar.mkv", configFileJsonSchema.ProcessOptions.FileIgnoreList);
        Assert.Equal(100000000, configFileJsonSchema.VerifyOptions.MaximumBitrate);
        Assert.Equal(60, configFileJsonSchema.MonitorOptions.MonitorWaitTime);
    }
}
