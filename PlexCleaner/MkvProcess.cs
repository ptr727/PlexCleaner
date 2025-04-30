using System.Diagnostics;
using System.IO;
using InsaneGenius.Utilities;
using Serilog;

namespace PlexCleaner;

public static class MkvProcess
{
    public static bool ReMux(string fileName)
    {
        ProcessFile processFile = new(fileName);
        if (!processFile.GetMediaInfo())
        {
            return false;
        }
        bool modified = false;
        return processFile.RemuxByExtension(false, ref modified);
    }

    public static bool ReEncode(string fileName)
    {
        ProcessFile processFile = new(fileName);
        if (!processFile.GetMediaInfo())
        {
            return false;
        }
        bool modified = false;
        return processFile.ReEncode(false, ref modified);
    }

    public static bool Verify(string fileName)
    {
        ProcessFile processFile = new(fileName);
        if (!processFile.GetMediaInfo())
        {
            return false;
        }
        return processFile.Verify(false, out _);
    }

    public static bool DeInterlace(string fileName)
    {
        ProcessFile processFile = new(fileName);
        if (!processFile.GetMediaInfo())
        {
            return false;
        }
        bool modified = false;
        return processFile.DeInterlace(false, ref modified);
    }

    public static bool RemoveSubtitles(string fileName)
    {
        ProcessFile processFile = new(fileName);
        if (!processFile.GetMediaInfo())
        {
            return false;
        }
        bool modified = false;
        return processFile.RemoveSubtitles(ref modified);
    }

    public static bool RemoveClosedCaptions(string fileName)
    {
        ProcessFile processFile = new(fileName);
        if (!processFile.GetMediaInfo())
        {
            return false;
        }
        bool modified = false;
        return processFile.RemoveClosedCaptions(false, ref modified);
    }
}
