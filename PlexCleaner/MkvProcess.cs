using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using InsaneGenius.Utilities;
using Serilog;

namespace PlexCleaner;

public static class MkvProcess
{
    public static bool ReMux(string fileName)
    {
        // Only remux .mkv or ProcessOptions.ReMuxExtensions files
        if (
            !SidecarFile.IsMkvFile(fileName)
            && !Program.Config.ProcessOptions.ReMuxExtensions.Contains(
                Path.GetExtension(fileName).ToLowerInvariant()
            )
        )
        {
            // Warning only
            Log.Warning("ReMux called with unsupported file type : {FileName}", fileName);
            return true;
        }

        // ReMux in-place using MkvMerge
        return Convert.ReMux(fileName);
    }

    public static bool DeInterlace(string fileName)
    {
        // File must be MKV
        Debug.Assert(SidecarFile.IsMkvFile(fileName));

        // Create a temp output filename
        string tempName = Path.ChangeExtension(fileName, ".tmp5");
        Debug.Assert(fileName != tempName);
        _ = FileEx.DeleteFile(tempName);

        // DeInterlace without testing for interlaced frames
        if (!Tools.HandBrake.ConvertToMkv(fileName, tempName, true, true))
        {
            Log.Error("DeInterlace using HandBrake failed : {FileName}", fileName);
            _ = FileEx.DeleteFile(tempName);
            return false;
        }

        // ReMux using MkvMerge after Handbrake encoding
        if (!Convert.ReMux(tempName))
        {
            // Error
            _ = FileEx.DeleteFile(tempName);
            return false;
        }

        return FileEx.RenameFile(tempName, fileName);
    }

    public static bool ReEncode(string fileName)
    {
        // File must be MKV
        Debug.Assert(SidecarFile.IsMkvFile(fileName));

        // Create a temp output filename
        string tempName = Path.ChangeExtension(fileName, ".tmp11");
        Debug.Assert(fileName != tempName);
        _ = FileEx.DeleteFile(tempName);

        // ReEncode using FfMpeg
        if (!Tools.FfMpeg.ConvertToMkv(fileName, tempName))
        {
            Log.Error("ReEncode using FfMpeg failed : {FileName}", fileName);
            _ = FileEx.DeleteFile(tempName);
            return false;
        }

        // ReMux using MkvMerge after FfMpeg encoding
        if (!Convert.ReMux(tempName))
        {
            // Error
            _ = FileEx.DeleteFile(tempName);
            return false;
        }

        return FileEx.RenameFile(tempName, fileName);
    }

    public static bool RemoveSubtitles(string fileName)
    {
        // File must be MKV
        Debug.Assert(SidecarFile.IsMkvFile(fileName));

        // Create a temp output filename
        string tempName = Path.ChangeExtension(fileName, ".tmp6");
        Debug.Assert(fileName != tempName);
        _ = FileEx.DeleteFile(tempName);

        // Remove subtitles using MkvMerge
        if (!Tools.MkvMerge.RemoveSubtitles(fileName, tempName))
        {
            Log.Error("Remove Subtitles using MkvMerge failed : {FileName}", fileName);
            _ = FileEx.DeleteFile(tempName);
            return false;
        }

        return FileEx.RenameFile(tempName, fileName);
    }

    public static bool VerifyMedia(string fileName)
    {
        // File must be MKV
        Debug.Assert(SidecarFile.IsMkvFile(fileName));

        // Verify using FfMpeg
        if (!Tools.FfMpeg.VerifyMedia(fileName, out string _))
        {
            Log.Error("Media verifications using FfMpeg failed : {FileName}", fileName);
            return false;
        }

        return true;
    }

    public static bool RemoveClosedCaptions(string fileName)
    {
        // File must be MKV
        Debug.Assert(SidecarFile.IsMkvFile(fileName));

        // Reuse logic in ProcessFile
        ProcessFile processFile = new(fileName);
        if (!processFile.GetMediaInfo())
        {
            return false;
        }

        // Override conditional processing
        Program.Config.ProcessOptions.RemoveClosedCaptions = true;
        Program.Options.TestNoModify = false;

        // Detect and remove closed captions if present
        bool modified = false;
        return processFile.RemoveClosedCaptions(ref modified);
    }
}
