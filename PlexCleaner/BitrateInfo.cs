using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Serilog;

namespace PlexCleaner;

public class BitrateInfo
{
    public void Calculate(
        List<FfMpegToolJsonSchema.Packet> packetList,
        int videoStream,
        int audioStream,
        int threshold
    )
    {
        // Calculate the media playback duration from timestamps
        Duration = 0;
        foreach (
            FfMpegToolJsonSchema.Packet packet in packetList.Where(packet =>
                ShouldCompute(packet, videoStream, audioStream)
            )
        )
        {
            // Use DTS if PTS not set
            if (double.IsNaN(packet.PtsTime))
            {
                packet.PtsTime = packet.DtsTime;
            }

            // Timestamp must be set, and not be zero, and not negative
            Debug.Assert(!double.IsNaN(packet.PtsTime));
            Debug.Assert(packet.PtsTime != 0.0);
            Debug.Assert(!double.IsNegative(packet.PtsTime));

            // Update duration
            int packetTime = System.Convert.ToInt32(Math.Floor(packet.PtsTime));
            if (packetTime > Duration)
            {
                Duration = packetTime;
            }
        }

        // Add 1 for index offset
        Duration++;

        // Set the bitrate array size to the duration in seconds
        VideoBitrate = new Bitrate(Duration);
        AudioBitrate = new Bitrate(Duration);
        CombinedBitrate = new Bitrate(Duration);

        // Iterate through all the packets and calculate the bitrate
        long videoPackets = 0;
        long audioPackets = 0;
        foreach (
            FfMpegToolJsonSchema.Packet packet in packetList.Where(packet =>
                ShouldCompute(packet, videoStream, audioStream)
            )
        )
        {
            // Find packet timestamp index entry, round down
            int index = System.Convert.ToInt32(Math.Floor(packet.PtsTime));
            Debug.Assert(index >= 0 && index < VideoBitrate.Rate.Length);

            // Stream must match expected types
            Debug.Assert(
                (
                    packet.StreamIndex == videoStream
                    && packet.CodecType.Equals("video", StringComparison.OrdinalIgnoreCase)
                )
                    || (
                        packet.StreamIndex == audioStream
                        && packet.CodecType.Equals("audio", StringComparison.OrdinalIgnoreCase)
                    )
            );

            // Update byte count at packet index
            if (packet.StreamIndex == videoStream)
            {
                videoPackets++;
                VideoBitrate.Rate[index] += packet.Size;
            }
            if (packet.StreamIndex == audioStream)
            {
                audioPackets++;
                AudioBitrate.Rate[index] += packet.Size;
            }
            CombinedBitrate.Rate[index] += packet.Size;
        }

        // If there are no packets the stream is empty
        if (videoPackets == 0 || audioPackets == 0)
        {
            Log.Error(
                "Empty stream detected : VideoPackets: {VideoPackets}, AudioPackets: {AudioPackets}",
                videoPackets,
                audioPackets
            );
        }

        // Calculate the stream bitrate
        VideoBitrate.Calculate(threshold);
        AudioBitrate.Calculate(threshold);
        CombinedBitrate.Calculate(threshold);
    }

    public void WriteLine()
    {
        VideoBitrate.WriteLine("Video");
        AudioBitrate.WriteLine("Audio");
        CombinedBitrate.WriteLine("Combined");
    }

    public Bitrate VideoBitrate { get; set; }
    public Bitrate AudioBitrate { get; set; }
    public Bitrate CombinedBitrate { get; set; }

    public int Duration { get; set; }

    private static bool ShouldCompute(
        FfMpegToolJsonSchema.Packet packet,
        int videoStream,
        int audioStream
    )
    {
        // Stream index must match the audio or video stream index
        if (packet.StreamIndex != videoStream && packet.StreamIndex != audioStream)
        {
            return false;
        }

        // Must have PTS or DTS timestamps
        if (double.IsNaN(packet.PtsTime) && double.IsNaN(packet.DtsTime))
        {
            return false;
        }

        // If PTS or DTS is set, it must not be zero and not negative
        if (
            !double.IsNaN(packet.PtsTime)
            && (double.IsNegative(packet.PtsTime) || packet.PtsTime == 0.0)
        )
        {
            return false;
        }
        if (
            !double.IsNaN(packet.DtsTime)
            && (double.IsNegative(packet.DtsTime) || packet.DtsTime == 0.0)
        )
        {
            return false;
        }

        // If duration is set it must not be more than 1 second
        if (!double.IsNaN(packet.DurationTime) && packet.DurationTime > 1.0)
        {
            return false;
        }

        // Must have size
        return packet.Size > 0;
    }
}
