using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using InsaneGenius.Utilities;
using Serilog;

namespace PlexCleaner;

public static class MkvProcess
{
    public static bool ReMuxTypes(string fileName)
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

        // Create a temp output filename
        string tempName = Path.ChangeExtension(fileName, ".tmp4");
        Debug.Assert(fileName != tempName);
        _ = FileEx.DeleteFile(tempName);

        // Remux
        if (!Tools.MkvMerge.ReMuxToMkv(fileName, tempName))
        {
            Log.Error("ReMux using MkvMerge failed : {FileName}", fileName);
            _ = FileEx.DeleteFile(tempName);
            return false;
        }

        return FileEx.RenameFile(tempName, fileName);
    }

    public static bool ReMux(string fileName)
    {
        // Do not limit file types by extension, called with temp extensions during processing

        // Create a temp output filename
        string tempName = Path.ChangeExtension(fileName, ".tmp4");
        Debug.Assert(fileName != tempName);
        _ = FileEx.DeleteFile(tempName);

        // Remux
        if (!Tools.MkvMerge.ReMuxToMkv(fileName, tempName))
        {
            Log.Error("ReMux using MkvMerge failed : {FileName}", fileName);
            _ = FileEx.DeleteFile(tempName);
            return false;
        }

        return FileEx.RenameFile(tempName, fileName);
    }

    public static bool DeInterlace(string fileName)
    {
        // File must be MKV
        Debug.Assert(SidecarFile.IsMkvFile(fileName));

        // Create a temp output filename
        string tempName = Path.ChangeExtension(fileName, ".tmp5");
        Debug.Assert(fileName != tempName);
        _ = FileEx.DeleteFile(tempName);

        // Deinterlace
        if (!Tools.HandBrake.ConvertToMkv(fileName, tempName, true, true))
        {
            Log.Error("DeInterlace using HandBrake failed : {FileName}", fileName);
            _ = FileEx.DeleteFile(tempName);
            return false;
        }

        // Remux using MkvMerge after Handbrake encoding
        if (!ReMux(tempName))
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

        // ReEncode
        if (!Tools.FfMpeg.ConvertToMkv(fileName, tempName))
        {
            Log.Error("ReEncode using FfMpeg failed : {FileName}", fileName);
            _ = FileEx.DeleteFile(tempName);
            return false;
        }

        // Remux using MkvMerge after FfMpeg encoding
        if (!ReMux(tempName))
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
            Log.Error("Failed to get media tool info : {FileName}", fileName);
            return false;
        }

        // Override conditional processing
        Program.Config.ProcessOptions.RemoveClosedCaptions = true;
        Program.Options.TestNoModify = false;

        // Remove closed captions
        bool modified = false;
        if (!processFile.RemoveClosedCaptions(ref modified))
        {
            Log.Error("Remove Closed Captions failed : {FileName}", fileName);
            return false;
        }

        // Done
        return true;
    }
}
