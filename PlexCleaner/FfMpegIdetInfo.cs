using System;
using System.IO;

namespace PlexCleaner;

public class FfMpegIdetInfo
{
    public class Repeated
    {
        public int Neither { get; set; }
        public int Top { get; set; }
        public int Bottom { get; set; }
        public int Total => Neither + Top + Bottom;
    }
    public Repeated RepeatedFields { get; } = new();

    public class Frames
    {
        public int Tff { get; set; }
        public int Bff { get; set; }
        public int Progressive { get; set; }
        public int Undetermined { get; set; }
        public int Interlaced => Tff + Bff;
        public int Total => Tff + Bff + Progressive + Undetermined;
    }
    public Frames SingleFrame { get; } = new();
    public Frames MultiFrame { get; } = new();

    public int Progressive => SingleFrame.Progressive + MultiFrame.Progressive;
    public int Undetermined => SingleFrame.Undetermined + MultiFrame.Undetermined;
    public int Interlaced => SingleFrame.Interlaced + MultiFrame.Interlaced;
    public int Total => SingleFrame.Total + MultiFrame.Total;

    public bool IsInterlaced()
    {
        // TODO: Based on experimentation this logic is not reliable
        // E.g. When the interlaced frames are > 0, and running deinterlace, the interlaced frame count is still > 0

        // All undetermined
        if (Undetermined == Total)
        {
            return false;
        }

        // No interlaced
        if (Interlaced == 0)
        {
            return false;
        }

        // Calculate the % of interlaced frames vs. progressive frames
        double percentage = 100.0 * System.Convert.ToDouble(Interlaced) / System.Convert.ToDouble(Interlaced + Progressive);
        return percentage > InterlacedThreshold;
    }

    public static bool GetIdetInfo(FileInfo mediaFile, out FfMpegIdetInfo idetInfo)
    {
        if (mediaFile == null)
        {
            throw new ArgumentNullException(nameof(mediaFile));
        }

        return Tools.FfMpeg.GetIdetInfo(mediaFile.FullName, out idetInfo);
    }

    // TODO : Figure out what reliable threshold would be
    private const double InterlacedThreshold = 5.0;
}