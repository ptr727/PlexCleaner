using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class ToolProgressParsingTests
{
    [Theory]
    [InlineData("out_time_us=5000000", 10000000L, 0.5)]
    [InlineData("out_time_ms=2500000", 10000000L, 0.25)]
    [InlineData("progress=end", 10000000L, 1.0)]
    public void FfMpegProgress_ParsesFraction(string line, long durationUs, double expected)
    {
        double? fraction = FfMpeg.Tool.ParseProgressFraction(line, durationUs);

        _ = fraction.Should().NotBeNull();
        _ = fraction.Value.Should().BeApproximately(expected, 1e-9);
    }

    [Theory]
    [InlineData("frame=100", 10000000L)] // not a position line
    [InlineData("out_time_us=0", 10000000L)] // zero position
    [InlineData("out_time_us=5000000", 0L)] // no duration
    [InlineData("progress=continue", 10000000L)] // continue is not a terminal
    [InlineData("garbage", 10000000L)]
    public void FfMpegProgress_ReturnsNullWithoutPosition(string line, long durationUs) =>
        _ = FfMpeg.Tool.ParseProgressFraction(line, durationUs).Should().BeNull();

    [Theory]
    [InlineData("  \"Progress\": 0.42,", 0.42)]
    [InlineData("\"Progress\":1.0", 1.0)]
    public void HandBrakeProgress_ParsesFraction(string line, double expected)
    {
        double? fraction = HandBrake.Tool.ParseProgressFraction(line);

        _ = fraction.Should().NotBeNull();
        _ = fraction.Value.Should().BeApproximately(expected, 1e-9);
    }

    [Theory]
    [InlineData("\"State\": \"WORKING\"")]
    [InlineData("random line")]
    public void HandBrakeProgress_ReturnsNullWithoutProgress(string line) =>
        _ = HandBrake.Tool.ParseProgressFraction(line).Should().BeNull();
}
