using InsaneGenius.Utilities;
using Serilog;
using System;

namespace PlexCleaner;

public class Bitrate
{
    public Bitrate(int seconds)
    {
        // Set array length to number of seconds
        Rate = new long[seconds];
    }

    // Threshold is in bytes per second
    public void Calculate(int threshold = 0)
    {
        Minimum = 0;
        Maximum = 0;
        Average = 0;
        Exceeded = 0;
        Duration = 0;
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
                    Exceeded++;
                    exceeded++;

                    // Maximum exceeded duration
                    if (exceeded > Duration)
                        Duration = exceeded;
                }
                else
                    // Reset
                    exceeded = 0;
            }
        }
        Average /= Rate.Length;
    }

    public void WriteLine(string prefix)
    {
        Log.Logger.Information("{Prefix} : Length: {Length}, Minimum: {Minimum}, Maximum: {Maximum}, Average: {Average}, Exceeded: {Exceeded}, Duration: {Duration}",
            prefix,
            TimeSpan.FromSeconds(Rate.Length),
            ToBitsPerSecond(Minimum),
            ToBitsPerSecond(Maximum),
            ToBitsPerSecond(Average),
            Exceeded,
            TimeSpan.FromSeconds(Duration));
    }

    public static string ToBitsPerSecond(long byterate)
    {
        return Format.BytesToKilo(byterate * 8, "bps");
    }

    // Array of bytes per second
    public long[] Rate { get; }
    // Bitrate in bytes per second
    public long Minimum { get; set; }
    public long Maximum { get; set; }
    public long Average { get; set; }
    // Threshold exceeded instance count and duration in seconds
    public int Exceeded { get; set; }
    public int Duration { get; set; }
}