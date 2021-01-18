using InsaneGenius.Utilities;
using System;

namespace PlexCleaner
{
    public class Bitrate
    {
        public Bitrate(int seconds)
        {
            // Set array length to number of seconds
            Rate = new long[seconds];
        }

        public void Calculate()
        {
            Calculate(0);
        }

        // Threshold is in bytes per second
        public void Calculate(int threshold)
        {
            Minimum = 0;
            Maximum = 0;
            Average = 0;
            Exceeded = 0;
            ExceededDuration = 0;
            int exceeded = 0;
            foreach (long bitrate in Rate)
            {
                // Min, max, average
                if (bitrate > Maximum)
                    Maximum = bitrate;
                if (bitrate < Minimum || Minimum == 0)
                    Minimum = bitrate;
                // TODO: Chance of overflow
                Average += bitrate;

                // Thresholds
                if (threshold > 0)
                {
                    // Bitrate exceeds threshold
                    if (bitrate > threshold)
                    {
                        Exceeded ++;
                        exceeded ++;

                        // Maximum exceeded duration
                        if (exceeded > ExceededDuration)
                            ExceededDuration = exceeded;
                    }
                    else
                        // Reset
                        exceeded = 0;
                }
            }
            Average /= Rate.Length;
        }

        public override string ToString()
        {
            return $"Bitrate : Duration : {TimeSpan.FromSeconds(Rate.Length)}, Minimum : {Format.BytesToKilo(Minimum * 8, "bps")}, Maximum : {Format.BytesToKilo(Maximum * 8, "bps")}, Average : {Format.BytesToKilo(Average * 8, "bps")}";
        }

        // Array of bytes per second
        public long[] Rate { get; }
        // Bitrate in bytes per second
        public long Minimum { get; set; }
        public long Maximum { get; set; }
        public long Average { get; set; }
        // Threshold exceeded instance count and duration in seconds
        public int Exceeded { get; set; }
        public int ExceededDuration { get; set; }
    }
}
