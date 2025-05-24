#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using InsaneGenius.Utilities;
using Serilog;

#endregion

namespace PlexCleaner;

public class Bitrate
{
    // List of bytes processed per second
    private List<long> BytesPerSecond { get; } = [];

    // Length in seconds
    public int Length => BytesPerSecond.Count;

    // Bitrate in bytes per second
    public long Minimum { get; private set; }
    public long Maximum { get; private set; }
    public long Average { get; private set; }

    // Threshold exceeded instance count and duration in seconds
    public int Exceeded { get; private set; }

    public int Duration { get; private set; }

    // Optional max bytes per second
    public void Calculate(int maxBps = 0)
    {
        Minimum = 0;
        Maximum = 0;
        Average = 0;
        Exceeded = 0;
        Duration = 0;
        int exceeded = 0;
        ulong total = 0;
        BytesPerSecond.ForEach(item =>
        {
            // Min, max, average
            if (item > Maximum)
            {
                Maximum = item;
            }

            if (item < Minimum || Minimum == 0)
            {
                Minimum = item;
            }
            total += (ulong)item;

            // Thresholds
            if (maxBps > 0)
            {
                // Bitrate exceeds threshold
                if (item > maxBps)
                {
                    Exceeded++;
                    exceeded++;

                    // Maximum exceeded duration
                    if (exceeded > Duration)
                    {
                        Duration = exceeded;
                    }
                }
                else
                {
                    // Reset
                    exceeded = 0;
                }
            }
        });
        Average = BytesPerSecond.Count == 0 ? 0 : (long)(total / (ulong)BytesPerSecond.Count);
    }

    public void Add(double time, long size)
    {
        Debug.Assert(time >= 0);
        Debug.Assert(size >= 0);

        // Find packet timestamp index entry, round down
        int index = System.Convert.ToInt32(Math.Floor(time));

        // Ensure the list is large enough
        if (index >= BytesPerSecond.Count)
        {
            if (index == BytesPerSecond.Count)
            {
                // Add size at new index
                BytesPerSecond.Add(size);
                return;
            }
            else
            {
                // Add range of new indexes with 0 values
                BytesPerSecond.AddRange(new long[index - BytesPerSecond.Count + 1]);
            }
        }

        // Update size at packet index
        BytesPerSecond[index] += size;
    }

    public void WriteLine(string prefix) =>
        Log.Information(
            "{Prefix} : Length: {Length}, Minimum: {Minimum}, Maximum: {Maximum}, Average: {Average}, Exceeded: {Exceeded}, Duration: {Duration}",
            prefix,
            TimeSpan.FromSeconds(BytesPerSecond.Count),
            ToBitsPerSecond(Minimum),
            ToBitsPerSecond(Maximum),
            ToBitsPerSecond(Average),
            Exceeded,
            TimeSpan.FromSeconds(Duration)
        );

    public static string ToBitsPerSecond(long byteRate) => Format.BytesToKilo(byteRate * 8, "bps");
}
