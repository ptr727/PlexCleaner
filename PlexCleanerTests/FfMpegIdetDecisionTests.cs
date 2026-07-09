using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class FfMpegIdetDecisionTests
{
    // tff, bff, progressive, undetermined, expectedInterlaced (decision is on the MultiFrame pass)
    [Theory]
    // Real full-scan idet samples
    [InlineData(1154, 0, 0, 0, true)] // interlaced, MPEG-2
    [InlineData(0, 0, 8085, 0, false)] // progressive, clean
    [InlineData(482, 534, 151645, 23, false)] // progressive feature with idet noise
    // Synthetic boundary cases
    [InlineData(8000, 0, 0, 0, true)] // pure top field interlaced
    [InlineData(0, 8000, 0, 0, true)] // pure bottom field interlaced
    [InlineData(50, 500, 4000, 0, false)] // minority interlaced noise below progressive
    [InlineData(100, 0, 50, 200, false)] // undetermined majority, cannot decide
    [InlineData(6000, 0, 4000, 0, true)] // dominant field order outnumbers progressive
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
        bool interlaced = idet.IsInterlaced(out string reason);
        _ = interlaced.Should().Be(expected);

        // The reason string leads with the verdict and carries the frame counts
        _ = reason.Should().StartWith(expected ? "Interlaced:" : "Progressive:");
        _ = reason.Should().Contain($"TFF: {tff}");
        _ = reason.Should().Contain($"BFF: {bff}");
        _ = reason.Should().Contain($"Progressive: {prog}");
        _ = reason.Should().Contain($"Undetermined: {und}");

        // Dominant is the dominant field order as a percentage of all frames
        double total = tff + bff + prog + und;
        double dominant = total == 0.0 ? 0.0 : Math.Max(tff, bff) / total * 100.0;
        _ = reason.Should().Contain(FormattableString.Invariant($"Dominant: {dominant:F2}%"));
    }

    [Theory]
    // tff, bff, progressive, undetermined, reason fragment describing the decision rationale
    [InlineData(0, 0, 0, 0, "no frames analyzed")]
    [InlineData(100, 0, 50, 200, "undetermined frames (200) outnumber determined frames (150)")]
    [InlineData(
        6000,
        0,
        4000,
        0,
        "dominant field order (6000) outnumbers progressive frames (4000)"
    )]
    [InlineData(
        50,
        500,
        4000,
        0,
        "dominant field order (500) does not outnumber progressive frames (4000)"
    )]
    public void IsInterlaced_ReasonRationale(int tff, int bff, int prog, int und, string fragment)
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
        _ = idet.IsInterlaced(out string reason);
        _ = reason.Should().Contain(fragment);
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
