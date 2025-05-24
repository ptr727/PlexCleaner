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
    [InlineData(@"D:\Test\Dragons' Den.mkv", @"D\\:/Test/Dragons\\\' Den.mkv")]
    [InlineData(
        @"D:\Test\Dragons' Den, Christmas Special.mkv",
        @"D\\:/Test/Dragons\\\' Den\\\, Christmas Special.mkv"
    )]
    public void Escape_Movie_fileName(string fileName, string escapedName)
    {
        string escapedFileName = FfProbe.EscapeMovieFileName(fileName);
        Assert.Equal(escapedName, escapedFileName);
    }
}
