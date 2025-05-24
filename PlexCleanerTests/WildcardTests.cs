using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class WildcardTests(PlexCleanerFixture fixture)
{
    private readonly PlexCleanerFixture _fixture = fixture;

    [Theory]
    [InlineData("*.fuse_hidden*", "Foo.fuse_hidden1234", true)]
    [InlineData("*.fuse_hidden*", "fuse_hidden.foo", false)]
    [InlineData("*.partial~", "Foo.partial~", true)]
    [InlineData("*.partial~", "partial~.foo", false)]
    [InlineData("*.sample.*", "Foo.sample.foo", true)]
    [InlineData("*.sample.*", "sample.foo", false)]
    [InlineData("*.sample.*", "foo.sample", false)]
    public void Wildcard_Match(string wildCard, string fileName, bool match) =>
        Assert.Equal(match, ProcessOptions4.MaskToRegex(wildCard).IsMatch(fileName));
}
