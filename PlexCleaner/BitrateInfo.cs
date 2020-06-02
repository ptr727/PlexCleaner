using InsaneGenius.Utilities;
using PlexCleaner.FfMpegToolJsonSchema;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace PlexCleaner
{
    public class BitrateInfo
    {
        public void Calculate(List<Packet> packetList)
        {
            if (packetList == null)
                throw new ArgumentNullException(nameof(packetList));

            // Calculating duration from timestamp values
            Duration = 0;
            foreach (Packet packet in packetList)
            {
                if (!ShouldCompute(packet))
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

            // Set the array size to the duration in seconds
            VideoBitrate = new long[Duration];
            AudioBitrate = new long[Duration];
            CombinedBitrate = new long[Duration];

            // Iterate through all the packets
            foreach (Packet packet in packetList)
            {
                if (!ShouldCompute(packet))
                    continue;

                // Round down when calculating index
                int index = System.Convert.ToInt32(Math.Floor(packet.PtsTime));

                // Calculate values
                if (packet.CodecType.Equals("video", StringComparison.OrdinalIgnoreCase))
                {
                    VideoBitrate[index] += packet.Size;
                    CombinedBitrate[index] += packet.Size;
                }
                if (packet.CodecType.Equals("audio", StringComparison.OrdinalIgnoreCase))
                {
                    AudioBitrate[index] += packet.Size;
                    CombinedBitrate[index] += packet.Size;
                }
            }

            // Calculate the averages
            Minimum = 0;
            Maximum = 0;
            Average = 0;
            ThresholdExceeded = 0;
            ThresholdExceededDuration = 0;
            int threshold = 0;
            foreach (long bitrate in CombinedBitrate)
            {
                // Min, max, average
                if (bitrate > Maximum)
                    Maximum = bitrate;
                if (bitrate < Minimum ||
                    Minimum == 0)
                    Minimum = bitrate;
                Average += bitrate;

                // Threshold
                if (Threshold > 0 &&
                    bitrate > Threshold)
                { 
                    ThresholdExceeded ++;
                    threshold ++;
                    if (threshold > ThresholdExceededDuration)
                        ThresholdExceededDuration = threshold;
                }
                else
                    threshold = 0;
            }
            Average /= Duration;
        }

        public override string ToString()
        {
            return $"Bitrate : Duration : {TimeSpan.FromSeconds(Duration)}, Minimum : {Format.BytesToKilo(Minimum * 8, "bps")}, Maximum : {Format.BytesToKilo(Maximum * 8, "bps")}, Average : {Format.BytesToKilo(Average * 8, "bps")}, Threshold : {Format.BytesToKilo(Threshold * 8, "bps")}, ThresholdExceeded : {ThresholdExceeded}, ThresholdExceededDuration : {TimeSpan.FromSeconds(ThresholdExceededDuration)}";
        }

        // Bytes per second
        public long Threshold { get; set; } = 100 * Format.MB / 8;
        // Number of times exceeded
        public int ThresholdExceeded { get; set; }
        // Number of consecutive times exceeded
        public int ThresholdExceededDuration { get; set; }

        public long Minimum { get; set; }
        public long Maximum { get; set; }
        public long Average { get; set; }

        public int Duration { get; set; }

        private static bool ShouldCompute(Packet packet)
        {
            // Must be audio or video tracks
            if (!packet.CodecType.Equals("video", StringComparison.OrdinalIgnoreCase) && 
                !packet.CodecType.Equals("audio", StringComparison.OrdinalIgnoreCase))
                return false;

            // Must be index 0
            return packet.StreamIndex == 0;
        }

        private long[] VideoBitrate = null;
        private long[] AudioBitrate = null;
        private long[] CombinedBitrate = null;
    }
}
