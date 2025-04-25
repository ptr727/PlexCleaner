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

    // % of interlaced frames vs. progressive frames
    public double InterlacedPercentage =>
        System.Convert.ToDouble(Interlaced) / System.Convert.ToDouble(Interlaced + Progressive);

    public bool IsInterlaced()
    {
        // TODO: Based on experimentation this logic is not completely reliable
        // E.g. When the interlaced frames are > 0, and running deinterlace, the interlaced frame count is still > 0

        // All undetermined
        if (Undetermined == Total)
        {
            return false;
        }

        // Not interlaced
        return Interlaced != 0 && InterlacedPercentage > InterlacedThreshold;
    }

    public static bool GetIdetInfo(
        FileInfo mediaFile,
        out FfMpegIdetInfo idetInfo,
        out string error
    ) => Tools.FfMpeg.GetIdetInfo(mediaFile.FullName, out idetInfo, out error);

    public const double InterlacedThreshold = 5.0 / 100.0;
}
