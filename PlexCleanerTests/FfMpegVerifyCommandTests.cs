using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class FfMpegVerifyCommandTests
{
    [Theory]
    [InlineData("8.1.2", "demux")]
    [InlineData("7.1.1", "demux")]
    [InlineData("6.1", "demux")]
    [InlineData("6.0", "-1")]
    [InlineData("5.1.6", "-1")]
    [InlineData("4.4.2", "-1")]
    [InlineData("", "demux")]
    [InlineData("N-121234-g0123456789", "demux")]
    public void GetDemuxTimeBase_SelectsSpellingByVersion(string version, string expected) =>
        _ = FfMpeg.Tool.GetDemuxTimeBase(version).Should().Be(expected);

    [Fact]
    public void GetVerifyMediaCommand_EncodesVideoInDemuxTimeBase()
    {
        string arguments = FfMpeg
            .Tool.GetVerifyMediaCommand("ffmpeg", "8.1.2", "media.mkv")
            .Arguments;

        _ = arguments.Should().Contain("-enc_time_base:v demux -f null -");
        _ = arguments.Should().NotContain("-enc_time_base:a");
        _ = arguments
            .IndexOf("-enc_time_base:v", StringComparison.Ordinal)
            .Should()
            .BeGreaterThan(arguments.IndexOf("-i ", StringComparison.Ordinal));
    }

    [Fact]
    public void GetVerifyMediaCommand_OlderFfMpeg_UsesInputTimeBase() =>
        _ = FfMpeg
            .Tool.GetVerifyMediaCommand("ffmpeg", "5.1.6", "media.mkv")
            .Arguments.Should()
            .Contain("-enc_time_base:v -1 -f null -");

    [Fact]
    public void GetVerifyMediaCommand_ExitsOnError()
    {
        string arguments = FfMpeg
            .Tool.GetVerifyMediaCommand("ffmpeg", "8.1.2", "media.mkv")
            .Arguments;

        _ = arguments.Should().Contain("-xerror");
        _ = arguments.Should().Contain("-loglevel error");
    }
}
