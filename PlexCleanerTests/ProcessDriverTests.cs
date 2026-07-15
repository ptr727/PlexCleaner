using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class ProcessDriverTests
{
    [Theory]
    [InlineData(0, 0, 0, 0, "00:00:00.000")]
    [InlineData(0, 0, 12, 345, "00:00:12.345")]
    [InlineData(0, 5, 9, 7, "00:05:09.007")]
    [InlineData(2, 3, 4, 500, "02:03:04.500")]
    public void FormatDuration_MillisecondPrecision(
        int hours,
        int minutes,
        int seconds,
        int milliseconds,
        string expected
    )
    {
        TimeSpan value = new(0, hours, minutes, seconds, milliseconds);

        _ = ProcessDriver.FormatDuration(value).Should().Be(expected);
    }

    [Fact]
    public void FormatDuration_MultiDay_HoursDoNotWrap()
    {
        // Hours come from TotalHours, so a run past 24h reads as accumulated hours, not a day rollover
        TimeSpan value = new(1, 3, 14, 3, 512);

        _ = ProcessDriver.FormatDuration(value).Should().Be("27:14:03.512");
    }
}
