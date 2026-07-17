using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class DtsInfoTests
{
    private static FfMpegToolJsonSchema.Packet Packet(
        long streamIndex,
        double dtsTime,
        string codecType = "audio"
    ) =>
        new()
        {
            StreamIndex = streamIndex,
            DtsTime = dtsTime,
            CodecType = codecType,
        };

    [Fact]
    public void Add_MonotonicDts_NoDetection()
    {
        DtsInfo dtsInfo = new();
        foreach (double dts in new[] { 0.0, 0.04, 0.08, 0.12 })
        {
            dtsInfo.Add(Packet(1, dts));
        }

        _ = dtsInfo.HasNonMonotonicDts.Should().BeFalse();
        _ = dtsInfo.NonMonotonicByStream.Should().BeEmpty();
    }

    [Fact]
    public void Add_DuplicateDts_Detected()
    {
        // A repeated DTS (X >= X) is the common benign case
        DtsInfo dtsInfo = new();
        dtsInfo.Add(Packet(1, 0.08));
        dtsInfo.Add(Packet(1, 0.08));

        _ = dtsInfo.HasNonMonotonicDts.Should().BeTrue();
        _ = dtsInfo.NonMonotonicByStream[1].Should().Be(1);
    }

    [Fact]
    public void Add_BackwardDts_DetectedPerStream()
    {
        DtsInfo dtsInfo = new();
        dtsInfo.Add(Packet(1, 0.10));
        dtsInfo.Add(Packet(1, 0.05));
        dtsInfo.Add(Packet(2, 0.00));
        dtsInfo.Add(Packet(2, 0.04));

        _ = dtsInfo.HasNonMonotonicDts.Should().BeTrue();
        _ = dtsInfo.NonMonotonicByStream.Should().ContainKey(1);
        _ = dtsInfo.NonMonotonicByStream.Should().NotContainKey(2);
    }

    [Fact]
    public void Add_NanDts_Ignored()
    {
        // Packets with no DTS or PTS are skipped, not counted as breaks
        DtsInfo dtsInfo = new();
        dtsInfo.Add(Packet(1, double.NaN));
        dtsInfo.Add(Packet(1, double.NaN));

        _ = dtsInfo.HasNonMonotonicDts.Should().BeFalse();
    }

    [Fact]
    public void NonMonotonicIsAudioOnly_AudioDts_True()
    {
        // A demux-visible audio DTS is repairable by the audio setts filter
        DtsInfo dtsInfo = new();
        dtsInfo.Add(Packet(1, 0.08, "audio"));
        dtsInfo.Add(Packet(1, 0.08, "audio"));

        _ = dtsInfo.NonMonotonicIsAudioOnly.Should().BeTrue();
    }

    [Fact]
    public void NonMonotonicIsAudioOnly_VideoDts_False()
    {
        // A video DTS is not audio-repairable, a video setts would reorder B-frames
        DtsInfo dtsInfo = new();
        dtsInfo.Add(Packet(0, 0.08, "video"));
        dtsInfo.Add(Packet(0, 0.08, "video"));

        _ = dtsInfo.HasNonMonotonicDts.Should().BeTrue();
        _ = dtsInfo.NonMonotonicIsAudioOnly.Should().BeFalse();
    }

    [Fact]
    public void NonMonotonicIsAudioOnly_MixedAudioAndVideo_False()
    {
        // If any offending stream is non-audio the audio setts cannot fully repair the file
        DtsInfo dtsInfo = new();
        dtsInfo.Add(Packet(0, 0.08, "video"));
        dtsInfo.Add(Packet(0, 0.08, "video"));
        dtsInfo.Add(Packet(1, 0.08, "audio"));
        dtsInfo.Add(Packet(1, 0.08, "audio"));

        _ = dtsInfo.NonMonotonicIsAudioOnly.Should().BeFalse();
    }

    [Fact]
    public void NonMonotonicIsAudioOnly_NoDts_False()
    {
        DtsInfo dtsInfo = new();
        dtsInfo.Add(Packet(1, 0.04, "audio"));
        dtsInfo.Add(Packet(1, 0.08, "audio"));

        _ = dtsInfo.NonMonotonicIsAudioOnly.Should().BeFalse();
    }
}
