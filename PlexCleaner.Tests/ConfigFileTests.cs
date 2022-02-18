using Xunit;

namespace PlexCleaner.Tests;

public class ConfigFileTests
{
    [Theory]
    [InlineData("PlexCleaner.v1.json")]
    [InlineData("PlexCleaner.v2.json")]
    public void Open_OldSchemas_Opens(string fileName)
    {
        ConfigFileJsonSchema configFileJsonSchema = ConfigFileJsonSchema.FromFile(SampleFiles.GetSampleFilePath(fileName));
        Assert.NotNull(configFileJsonSchema);
    }
}
