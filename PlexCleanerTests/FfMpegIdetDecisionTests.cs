using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class FfMpegIdetDecisionTests
{
    // tff, bff, progressive, undetermined, expectedInterlaced (decision is on the MultiFrame pass)
    [Theory]
    // Progressive content
    [InlineData(0, 0, 8992, 0, false)]
    // Cleanly interlaced, single field order
    [InlineData(8991, 0, 0, 0, true)]
    [InlineData(0, 8991, 0, 0, true)]
    // Progressive with idet noise, real-world false-positive samples, not interlaced
    [InlineData(206, 111, 3968, 32, false)] // The American (2010)
    [InlineData(55, 574, 3669, 19, false)] // Top Gun Maverick (2022)
    [InlineData(332, 397, 3760, 12, false)] // Hysteria (2011)
    // Mostly undetermined, cannot decide reliably, not interlaced
    [InlineData(100, 0, 50, 200, false)]
    // Dominant field order outnumbers progressive frames, interlaced
    [InlineData(6000, 0, 4000, 0, true)]
    public void IsInterlaced_MultiFrame(int tff, int bff, int prog, int und, bool expected)
    {
        FfMpegIdetInfo idet = new()
        {
            MultiFrame = new FfMpegIdetInfo.Frames
            {
                Tff = tff,
                Bff = bff,
                Progressive = prog,
                Undetermined = und,
            },
        };
        _ = idet.IsInterlaced().Should().Be(expected);
    }

    [Fact]
    public void IsInterlaced_Uses_MultiFrame_Not_SingleFrame()
    {
        // SingleFrame looks interlaced but MultiFrame is progressive, the decision follows MultiFrame
        FfMpegIdetInfo idet = new()
        {
            SingleFrame = new FfMpegIdetInfo.Frames
            {
                Tff = 8000,
                Bff = 0,
                Progressive = 0,
                Undetermined = 0,
            },
            MultiFrame = new FfMpegIdetInfo.Frames
            {
                Tff = 0,
                Bff = 0,
                Progressive = 8000,
                Undetermined = 0,
            },
        };
        _ = idet.IsInterlaced().Should().BeFalse();
    }
}
