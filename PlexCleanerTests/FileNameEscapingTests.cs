using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class FileNameEscapingTests(PlexCleanerFixture fixture)
{
    private readonly PlexCleanerFixture _fixture = fixture;

    [Theory]
    [InlineData(@"\", @"/")]
    [InlineData(@":", @"\\:")]
    [InlineData(@"'", @"\\\'")]
    [InlineData(@",", @"\\\,")]
    [InlineData(@";", @"\\\;")]
    [InlineData(@"[", @"\\\[")]
    [InlineData(@"]", @"\\\]")]
    [InlineData(
        @"D:\Test\Naming - movie=,.;{}[out0+subcc] (1234) {abc-123} [aaa][bbb][ccc]-def.mkv",
        @"D\\:/Test/Naming - movie=\\\,.\\\;{}\\\[out0+subcc\\\] (1234) {abc-123} \\\[aaa\\\]\\\[bbb\\\]\\\[ccc\\\]-def.mkv"
    )]
    public void Escape_Movie_fileName(string fileName, string escapedName)
    {
        string escapedFileName = FfProbe.EscapeMovieFileName(fileName);
        Assert.Equal(escapedName, escapedFileName);
    }
}
