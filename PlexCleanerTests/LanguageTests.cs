using Xunit;

namespace PlexCleanerTests;

public class LanguageTests : IClassFixture<PlexCleanerTests>
{
    [Theory]
    [InlineData("afr", "af")]
    [InlineData("Afrikaans", "af")]
    [InlineData("ger", "de")]
    [InlineData("fre", "fr")]
    [InlineData("eng", "en")]
    [InlineData("", "und")]
    [InlineData("und", "und")]
    [InlineData("zxx", "zxx")]
    [InlineData("chi", "zh")]
    [InlineData("zho", "zh")]
    [InlineData("xxx", "und")]
    public void Convert_Language_Tags(string tag, string ietf)
    {
        Assert.Equal(ietf, PlexCleaner.Language.GetIetfTag(tag, false));
    }

    [Theory]
    [InlineData("en", "en")]
    [InlineData("en", "en-US")]
    [InlineData("en", "en-GB")]
    [InlineData("en-GB", "en-GB")]
    [InlineData("zh", "zh-Hant")]
    [InlineData("sr-Latn", "sr-Latn-RS")]
    public void Match_Language_Tags(string prefix, string tag)
    {
        Assert.True(PlexCleaner.Language.IsMatch(prefix, tag));
    }

    [Theory]
    [InlineData("zh", "en")]
    [InlineData("zha", "zh-Hans")]
    [InlineData("zh-Hant", "zh-Hans")]
    public void NotMatch_Language_Tags(string prefix, string tag)
    {
        Assert.False(PlexCleaner.Language.IsMatch(prefix, tag));
    }
}
