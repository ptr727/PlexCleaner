using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class FfMpegNalUnitTests
{
    [Theory]
    // Format strings match FfProbe codec_name / MediaInfo format tags
    [InlineData("h264", 6)]
    [InlineData("hevc", 39)]
    [InlineData("mpeg2video", 178)]
    [InlineData("HEVC", 39)] // Case insensitive
    public void GetNalUnit_KnownFormat_ReturnsSeiNalUnit(string format, int expected) =>
        _ = FfMpeg.GetNalUnit(format).Should().Be(expected);

    [Theory]
    [InlineData("h265")] // FfProbe reports HEVC as "hevc", not "h265"
    [InlineData("av1")]
    [InlineData("vp9")]
    [InlineData("")]
    public void GetNalUnit_UnsupportedFormat_ReturnsZero(string format) =>
        _ = FfMpeg.GetNalUnit(format).Should().Be(0);
}
