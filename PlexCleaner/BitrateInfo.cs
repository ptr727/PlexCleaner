using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PlexCleaner;

public class BitrateInfo(long videoStream, long audioStream, int maxBps)
{
    public Bitrate VideoBitrate { get; } = new();
    public Bitrate AudioBitrate { get; } = new();
    public Bitrate CombinedBitrate { get; } = new();
    public int Duration => CombinedBitrate.Length;

    public void Calculate(List<FfMpegToolJsonSchema.Packet> packetList)
    {
        // Add all packets
        packetList.ForEach(Add);

        // Calculate the stream bitrate
        Calculate();
    }

    public void Calculate()
    {
        // Calculate the stream bitrate
        VideoBitrate.Calculate(maxBps);
        AudioBitrate.Calculate(maxBps);
        CombinedBitrate.Calculate(maxBps);
    }

    public void Add(FfMpegToolJsonSchema.Packet packet)
    {
        // Check if the packet is valid
        if (!ShouldCompute(packet))
        {
            return;
        }

        // Add the packet
        if (packet.StreamIndex == videoStream)
        {
            VideoBitrate.Add(packet.PtsTime, packet.Size);
        }
        else if (packet.StreamIndex == audioStream)
        {
            AudioBitrate.Add(packet.PtsTime, packet.Size);
        }

        CombinedBitrate.Add(packet.PtsTime, packet.Size);
    }

    public void WriteLine()
    {
        VideoBitrate.WriteLine("Video");
        AudioBitrate.WriteLine("Audio");
        CombinedBitrate.WriteLine("Combined");
    }

    private bool ShouldCompute(FfMpegToolJsonSchema.Packet packet)
    {
        // Epsilon for floating-point comparisons
        const double epsilon = 1e-9;

        // Stream index must match the audio or video stream index
        if (packet.StreamIndex != videoStream && packet.StreamIndex != audioStream)
        {
            return false;
        }

        // Stream must match expected types
        Debug.Assert(
            packet.StreamIndex
                == videoStream
                == packet.CodecType.Equals("video", StringComparison.OrdinalIgnoreCase)
        );
        Debug.Assert(
            packet.StreamIndex
                == audioStream
                == packet.CodecType.Equals("audio", StringComparison.OrdinalIgnoreCase)
        );

        // Must have PTS or DTS timestamps
        if (double.IsNaN(packet.PtsTime) && double.IsNaN(packet.DtsTime))
        {
            return false;
        }

        // If PTS or DTS is set, it must not be zero and not negative
        if (
            !double.IsNaN(packet.PtsTime)
            && (double.IsNegative(packet.PtsTime) || Math.Abs(packet.PtsTime) < epsilon)
        )
        {
            return false;
        }

        if (
            !double.IsNaN(packet.DtsTime)
            && (double.IsNegative(packet.DtsTime) || Math.Abs(packet.DtsTime) < epsilon)
        )
        {
            return false;
        }

        // Use DTS if PTS not set
        if (double.IsNaN(packet.PtsTime))
        {
            packet.PtsTime = packet.DtsTime;
        }

        // Timestamp must be set, and not be zero, and not negative
        Debug.Assert(!double.IsNaN(packet.PtsTime));
        Debug.Assert(Math.Abs(packet.PtsTime) >= epsilon);
        Debug.Assert(!double.IsNegative(packet.PtsTime));

        // If duration is set it must not be more than 1 second
        if (!double.IsNaN(packet.DurationTime) && packet.DurationTime > 1.0)
        {
            return false;
        }

        // Must have size
        return packet.Size > 0;
    }
}
