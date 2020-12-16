using System;
using System.IO;

namespace PlexCleaner
{
    public class FfMpegIdetInfo
    {
        public class Repeated
        {
            public int Neither { get; set; }
            public int Top { get; set; }
            public int Bottom { get; set; }
            public int Total => Neither + Top + Bottom;
        }
        public Repeated RepeatedFields { get; } = new Repeated();

        public class Frames
        {
            public int Tff { get; set; }
            public int Bff { get; set; }
            public int Progressive { get; set; }
            public int Undetermined { get; set; }
            public int Total => Tff + Bff + Progressive + Undetermined;
            public double Percentage => System.Convert.ToDouble(Tff + Bff) / System.Convert.ToDouble(Total);
        }
        public Frames SingleFrame { get; } = new Frames();
        public Frames MultiFrame { get; } = new Frames();

        public bool IsInterlaced()
        {
            return IsInterlaced(out double _, out double _);
        }

        public bool IsInterlaced(out double singleFrame, out double multiFrame)
        {
            // Calculate interlaced frame % of total frames
            singleFrame = SingleFrame.Percentage;
            multiFrame = MultiFrame.Percentage;

            // Are any frames interlaced frames
            // TODO : Figure out what reliable numbers would look like, e.g. at > 0 then decomb the result is still > 0?
            return singleFrame > 0.0 || multiFrame > 0.0;
        }

        public static bool GetIdetInfo(FileInfo mediaFile, out FfMpegIdetInfo idetInfo)
        {
            if (mediaFile == null)
                throw new ArgumentNullException(nameof(mediaFile));
            
            return FfMpegTool.GetIdetInfo(mediaFile.FullName, out idetInfo);
        }
    }
}