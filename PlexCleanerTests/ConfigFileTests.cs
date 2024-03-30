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
        ConfigFileJsonSchema4 configFileJsonSchema = ConfigFileJsonSchema4.FromFile(PlexCleanerTests.GetSampleFilePath(fileName));
        Assert.NotNull(configFileJsonSchema);
    }
}
