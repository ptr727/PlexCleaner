using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class LanguageTests(PlexCleanerTests fixture) : IClassFixture<PlexCleanerTests>
{
#pragma warning disable IDE0052 // Remove unread private members
    private readonly PlexCleanerTests _fixture = fixture;
#pragma warning restore IDE0052 // Remove unread private members

    [Theory]
    [InlineData("afr", "af")]
    [InlineData("ger", "de")]
    [InlineData("fre", "fr")]
    [InlineData("eng", "en")]
    [InlineData("dan", "da")]
    [InlineData("cpe", "cpe")]
    [InlineData("chi", "zh")]
    [InlineData("zho", "zh")]
    [InlineData("zxx", "zxx")]
    [InlineData("und", "und")]
    [InlineData("", "und")]
    [InlineData("xxx", "und")]
    public void ConvertIsoToIetf(string tag, string ietf) => Assert.Equal(ietf, Language.Singleton.GetIetfTag(tag, false));

    [Theory]
    [InlineData("en", "en")]
    [InlineData("en", "en-US")]
    [InlineData("en", "en-GB")]
    [InlineData("en-GB", "en-GB")]
    [InlineData("zh", "zh-cmn-Hant")]
    [InlineData("zh", "cmn-Hant")]
    [InlineData("sr-Latn", "sr-Latn-RS")]
    public void MatchLanguageTags(string prefix, string tag) => Assert.True(Language.Singleton.IsMatch(prefix, tag));

    [Theory]
    [InlineData("zh", "en")]
    [InlineData("zha", "zh-Hans")]
    [InlineData("zh-Hant", "zh-Hans")]
    public void NotMatchLanguageTags(string prefix, string tag) => Assert.False(Language.Singleton.IsMatch(prefix, tag));

    [Theory]
    [InlineData("af", "afr")]
    [InlineData("de", "ger")]
    [InlineData("fr", "fre")]
    [InlineData("en", "eng")]
    [InlineData("cpe", "cpe")]
    [InlineData("zxx", "zxx")]
    [InlineData("zh", "chi")]
    [InlineData("zh-cmn-Hant", "chi")]
    [InlineData("cmn-Hant", "chi")]
    [InlineData("no-NO", "nor")]
    [InlineData("", "und")]
    [InlineData("und", "und")]
    [InlineData("xxx", "und")]
    public void ConvertIetfToIsoTags(string ietf, string iso639) => Assert.Equal(iso639, Language.Singleton.GetIso639Tag(ietf, false));
}
