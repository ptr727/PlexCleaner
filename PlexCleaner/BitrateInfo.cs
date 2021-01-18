using PlexCleaner.FfMpegToolJsonSchema;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PlexCleaner
{
    public class BitrateInfo
    {
        public void Calculate(List<Packet> packetList, int videoStream, int audioStream, int threshold)
        {
            if (packetList == null)
                throw new ArgumentNullException(nameof(packetList));

            // Calculating duration from timestamp values
            Duration = 0;
            foreach (Packet packet in packetList)
            {
                if (!ShouldCompute(packet, videoStream, audioStream))
                    continue;

                // Use DTS if PTS not set
                if (double.IsNaN(packet.PtsTime))
                {
                    packet.PtsTime = packet.DtsTime;
                }
                Debug.Assert(!double.IsNaN(packet.PtsTime));

                // Packet duration can't be longer than the sample interval
                Debug.Assert(!double.IsNaN(packet.DurationTime));
                Debug.Assert(packet.DurationTime <= 1.0);

                // Size must be valid
                Debug.Assert(packet.Size > 0);

                int packetTime = System.Convert.ToInt32(Math.Floor(packet.PtsTime));
                if (packetTime > Duration)
                    Duration = packetTime;
            }

            // Add 1 for index offset
            Duration ++;

            // Set the bitrate array size to the duration in seconds
            VideoBitrate = new Bitrate(Duration);
            AudioBitrate = new Bitrate(Duration);
            CombinedBitrate = new Bitrate(Duration);

            // Iterate through all the packets
            foreach (Packet packet in packetList)
            {
                if (!ShouldCompute(packet, videoStream, audioStream))
                    continue;

                // Round down when calculating index
                int index = System.Convert.ToInt32(Math.Floor(packet.PtsTime));

                // Calculate values
                if (packet.StreamIndex == videoStream)
                {
                    VideoBitrate.Rate[index] += packet.Size;
                    CombinedBitrate.Rate[index] += packet.Size;
                }
                if (packet.StreamIndex == audioStream)
                {
                    AudioBitrate.Rate[index] += packet.Size;
                    CombinedBitrate.Rate[index] += packet.Size;
                }
            }

            // Calculate the bitrates
            VideoBitrate.Calculate(threshold);
            AudioBitrate.Calculate(threshold);
            CombinedBitrate.Calculate(threshold);
        }

        public Bitrate VideoBitrate { get; set; }
        public Bitrate AudioBitrate { get; set; }
        public Bitrate CombinedBitrate { get; set; }

        public int Duration { get; set; }

        private static bool ShouldCompute(Packet packet, int videoStream, int audioStream)
        {
            // Must match a stream index
            if (packet.StreamIndex != videoStream &&
                packet.StreamIndex != audioStream)
                return false;

            // Must have PTS or DTS
            if (double.IsNaN(packet.PtsTime) &&
                double.IsNaN(packet.PtsTime))
                return false;

            // Must have duration
            if (double.IsNaN(packet.DurationTime))
                return false;

            // Must have size
            if (packet.Size <= 0)
                return false;

            // Verify streams match expected type
            Debug.Assert((packet.StreamIndex == videoStream && packet.CodecType.Equals("video", StringComparison.OrdinalIgnoreCase)) ||
                         (packet.StreamIndex == audioStream && packet.CodecType.Equals("audio", StringComparison.OrdinalIgnoreCase)));

            return true;
        }
    }
}
