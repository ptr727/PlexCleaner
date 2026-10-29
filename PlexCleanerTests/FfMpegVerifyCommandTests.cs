using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class FfMpegVerifyCommandTests
{
    [Fact]
    public void GetVerifyMediaCommand_EncodesInDemuxTimeBase()
    {
        string arguments = FfMpeg.Tool.GetVerifyMediaCommand("ffmpeg", "media.mkv").Arguments;

        _ = arguments.Should().Contain("-enc_time_base:v demux -enc_time_base:a demux");
        _ = arguments.Should().EndWith("-f null -");
        _ = arguments
            .IndexOf("-enc_time_base:v", StringComparison.Ordinal)
            .Should()
            .BeGreaterThan(arguments.IndexOf("-i ", StringComparison.Ordinal));
    }

    [Fact]
    public void GetVerifyMediaCommand_ExitsOnError()
    {
        string arguments = FfMpeg.Tool.GetVerifyMediaCommand("ffmpeg", "media.mkv").Arguments;

        _ = arguments.Should().Contain("-xerror");
        _ = arguments.Should().Contain("-loglevel error");
    }
}
