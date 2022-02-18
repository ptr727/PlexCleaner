using InsaneGenius.Utilities;
using System;
using System.Diagnostics;
using System.IO;

namespace PlexCleaner;

public static class Convert
{
    public static bool ConvertToMkv(string inputname, out string outputname)
    {
        // Convert all tracks
        return ConvertToMkv(inputname, null, null, out outputname);
    }

    public static bool ConvertToMkv(string inputname, MediaInfo keep, MediaInfo reencode, out string outputname)
    {
        return ConvertToMkvFfMpeg(inputname, keep, reencode, out outputname);
    }

    public static bool ConvertToMkvFfMpeg(string inputname, out string outputname)
    {
        // Convert all tracks
        return ConvertToMkvFfMpeg(inputname, null, null, out outputname);
    }

    public static bool ConvertToMkvFfMpeg(string inputname, MediaInfo keep, MediaInfo reencode, out string outputname)
    {
        if (inputname == null)
            throw new ArgumentNullException(nameof(inputname));

        // Match the logic in ReMuxToMKV()

        // Test
        if (Program.Options.TestNoModify)
        {
            outputname = inputname;
            return true;
        }

        // Create a temp filename based on the input name
        outputname = Path.ChangeExtension(inputname, ".mkv");
        string tempname = Path.ChangeExtension(inputname, ".tmp");

        // Convert using ffmpeg
        if (!Tools.FfMpeg.ConvertToMkv(inputname, keep, reencode, tempname))
        {
            FileEx.DeleteFile(tempname);
            return false;
        }

        // Rename the temp file to the output file
        if (!FileEx.RenameFile(tempname, outputname))
            return false;

        // If the input and output names are not the same, delete the input
        return inputname.Equals(outputname, StringComparison.OrdinalIgnoreCase) ||
               FileEx.DeleteFile(inputname);
    }

    public static bool ReMuxToMkv(string inputname, out string outputname)
    {
        if (inputname == null)
            throw new ArgumentNullException(nameof(inputname));

        // Match the logic in ConvertToMKV()

        // Test
        if (Program.Options.TestNoModify)
        {
            outputname = inputname;
            return true;
        }

        // Create a MKV and temp filename based on the input name
        outputname = Path.ChangeExtension(inputname, ".mkv");
        string tempname = Path.ChangeExtension(inputname, ".tmp");

        // MKVToolNix and FFmpeg both have problems dealing with some AVI files, so we will try both
        // E.g. https://github.com/FFmpeg/FFmpeg/commit/8de1ee9f725aa3c550f425bd3120bcd95d5b2ea8
        // E.g. https://github.com/mbunkus/mkvtoolnix/issues/2123

        // Try MKV first
        if (!Tools.MkvMerge.ReMuxToMkv(inputname, tempname))
        {
            // Failed, delete temp file
            FileEx.DeleteFile(tempname);

            // Cancel requested
            if (Program.IsCancelledError())
                return false;

            // Retry using FFmpeg
            if (!Tools.FfMpeg.ReMuxToMkv(inputname, tempname))
            {
                // Failed, delete temp file
                FileEx.DeleteFile(tempname);

                // Error
                return false;
            }
        }

        // Rename the temp file to the output file
        if (!FileEx.RenameFile(tempname, outputname))
            return false;

        // If the input and output names are not the same, delete the input
        return inputname.Equals(outputname, StringComparison.OrdinalIgnoreCase) ||
               FileEx.DeleteFile(inputname);
    }

    public static bool ReMuxToMkv(string inputname, MediaInfo keep, out string outputname)
    {
        if (inputname == null)
            throw new ArgumentNullException(nameof(inputname));
        if (keep == null)
            throw new ArgumentNullException(nameof(keep));

        // This only works on MKV files and MkvMerge MediaInfo types
        Debug.Assert(keep.Parser == MediaTool.ToolType.MkvMerge);
        Debug.Assert(MkvMergeTool.IsMkvFile(inputname));

        // Match the logic in ConvertToMKV()

        // Test
        if (Program.Options.TestNoModify)
        {
            outputname = inputname;
            return true;
        }

        // Create a temp filename based on the input name
        outputname = Path.ChangeExtension(inputname, ".mkv");
        string tempname = Path.ChangeExtension(inputname, ".tmp");

        // Remux keeping specific tracks
        if (!Tools.MkvMerge.ReMuxToMkv(inputname, keep, tempname))
        {
            FileEx.DeleteFile(tempname);
            return false;
        }

        // Rename the temp file to the output file
        if (!FileEx.RenameFile(tempname, outputname))
            return false;

        // If the input and output names are not the same, delete the input
        return inputname.Equals(outputname, StringComparison.OrdinalIgnoreCase) ||
               FileEx.DeleteFile(inputname);
    }

    public static bool DeInterlaceToMkv(string inputname, out string outputname)
    {
        // HandBrake produces the best de-interlacing results
        return DeInterlaceToMkvHandbrake(inputname, out outputname);
    }

    public static bool DeInterlaceToMkvHandbrake(string inputname, out string outputname)
    {
        if (inputname == null)
            throw new ArgumentNullException(nameof(inputname));

        // Match the logic in ConvertToMKV()

        // Test
        if (Program.Options.TestNoModify)
        {
            outputname = inputname;
            return true;
        }

        // Create a temp filename based on the input name
        outputname = Path.ChangeExtension(inputname, ".mkv");
        string tempname = Path.ChangeExtension(inputname, ".tmp");

        // De-interlace video using handbrake
        if (!Tools.HandBrake.DeInterlaceToMkv(inputname, tempname))
        {
            FileEx.DeleteFile(tempname);
            return false;
        }

        // Rename the temp file to the output file
        if (!FileEx.RenameFile(tempname, outputname))
            return false;

        // If the input and output names are not the same, delete the input
        return inputname.Equals(outputname, StringComparison.OrdinalIgnoreCase) ||
               FileEx.DeleteFile(inputname);
    }

    public static bool ConvertToMkvHandBrake(string inputname, out string outputname)
    {
        if (inputname == null)
            throw new ArgumentNullException(nameof(inputname));

        // Match the logic in ConvertToMKV()

        // Test
        if (Program.Options.TestNoModify)
        {
            outputname = inputname;
            return true;
        }

        // Create a temp filename based on the input name
        outputname = Path.ChangeExtension(inputname, ".mkv");
        string tempname = Path.ChangeExtension(inputname, ".tmp");

        // Re-encode audio and video using handbrake
        if (!Tools.HandBrake.ConvertToMkv(inputname, tempname))
        {
            FileEx.DeleteFile(tempname);
            return false;
        }

        // Rename the temp file to the output file
        if (!FileEx.RenameFile(tempname, outputname))
            return false;

        // If the input and output names are not the same, delete the input
        return inputname.Equals(outputname, StringComparison.OrdinalIgnoreCase) ||
               FileEx.DeleteFile(inputname);
    }
}