using System;
using System.Diagnostics;
using System.IO;
using InsaneGenius.Utilities;
using Serilog;

namespace PlexCleaner;

public static class Convert
{
    public static bool ConvertToMkv(
        string inputName,
        SelectMediaProps selectMediaProps,
        out string outputName
    )
    {
        // Match the logic in ReMuxToMKV()

        // Create a MKV and temp filename based on the input name
        // Input may already be MKV file
        outputName = Path.ChangeExtension(inputName, ".mkv");
        string tempName = Path.ChangeExtension(inputName, ".tmp1");
        Debug.Assert(inputName != tempName);

        // Convert using ffmpeg
        // Selected is ReEncode
        // NotSelected is Keep
        Log.Information("Reencode using FfMpeg : {FileName}", inputName);
        if (!Tools.FfMpeg.ConvertToMkv(inputName, selectMediaProps, tempName, out string error))
        {
            Log.Error("Failed to reencode using FfMpeg : {FileName}", inputName);
            Log.Error("{Error}", error);
            _ = FileEx.DeleteFile(tempName);
            return false;
        }

        // Rename the temp file to the output file
        if (!FileEx.RenameFile(tempName, outputName))
        {
            return false;
        }

        // If the input and output names are not the same, delete the input
        return inputName.Equals(outputName, StringComparison.OrdinalIgnoreCase)
            || FileEx.DeleteFile(inputName);
    }

    public static bool ReMuxToMkv(string inputName, out string outputName)
    {
        // This function will try both MkvMerge and FfMpeg

        // Match the logic in ConvertToMKV()

        // Create a MKV and temp filename based on the input name
        // Input may already be MKV file
        outputName = Path.ChangeExtension(inputName, ".mkv");
        string tempName = Path.ChangeExtension(inputName, ".tmp2");
        Debug.Assert(inputName != tempName);

        // MkvMerge and FfMpeg both have problems dealing with some AVI files, so we will try both
        // E.g. https://github.com/FFmpeg/FFmpeg/commit/8de1ee9f725aa3c550f425bd3120bcd95d5b2ea8
        // E.g. https://github.com/mbunkus/mkvtoolnix/issues/2123

        // Try MKV first
        Log.Information("Remux using MkvMerge : {FileName}", inputName);
        if (!Tools.MkvMerge.ReMuxToMkv(inputName, tempName, out string error))
        {
            // Failed, delete temp file
            _ = FileEx.DeleteFile(tempName);

            // Cancel requested
            if (Program.IsCancelledError())
            {
                return false;
            }

            Log.Error("Failed to remux using MkvMerge : {FileName}", inputName);
            Log.Error("{Error}", error);

            // Retry using FfMpeg
            Log.Information("Remux using FfMpeg : {FileName}", inputName);
            if (!Tools.FfMpeg.ReMuxToMkv(inputName, tempName, out error))
            {
                // Failed, delete temp file
                _ = FileEx.DeleteFile(tempName);

                // Cancel requested
                if (Program.IsCancelledError())
                {
                    return false;
                }

                Log.Error("Failed to remux using FfMpeg : {TempFileName}", tempName);
                Log.Error("{Error}", error);

                // Error
                return false;
            }

            // ReMux using MkvMerge after FfMpeg or HandBrake encoding
            if (!ReMux(tempName))
            {
                _ = FileEx.DeleteFile(tempName);
                return false;
            }
        }

        // Rename the temp file to the output file
        if (!FileEx.RenameFile(tempName, outputName))
        {
            _ = FileEx.DeleteFile(tempName);
            return false;
        }

        // If the input and output names are not the same, delete the input
        return inputName.Equals(outputName, StringComparison.OrdinalIgnoreCase)
            || FileEx.DeleteFile(inputName);
    }

    public static bool ReMuxToMkv(
        string inputName,
        SelectMediaProps selectMediaProps,
        out string outputName
    )
    {
        // This function will only use MkvMerge
        if (selectMediaProps == null)
        {
            // Use version that will try both MkvMerge and FfMpeg
            return ReMuxToMkv(inputName, out outputName);
        }

        // This only works on MKV files and MkvMerge MediaProps types
        Debug.Assert(selectMediaProps.Selected.Parser == MediaTool.ToolType.MkvMerge);
        Debug.Assert(SidecarFile.IsMkvFile(inputName));

        // Match the logic in ConvertToMKV()

        // Create a MKV and temp filename based on the input name
        // Input may already be MKV file
        outputName = Path.ChangeExtension(inputName, ".mkv");
        string tempName = Path.ChangeExtension(inputName, ".tmp3");
        Debug.Assert(inputName != tempName);

        // ReMux keeping specific tracks
        // Selected is Keep
        // NotSelected is Remove
        Log.Information("Remux using MkvMerge : {FileName}", inputName);
        if (!Tools.MkvMerge.ReMuxToMkv(inputName, selectMediaProps, tempName, out string error))
        {
            Log.Error("Failed to remux using MkvMerge : {FileName}", inputName);
            Log.Error("{Error}", error);
            _ = FileEx.DeleteFile(tempName);
            return false;
        }

        // Rename the temp file to the output file
        if (!FileEx.RenameFile(tempName, outputName))
        {
            return false;
        }

        // If the input and output names are not the same, delete the input
        return inputName.Equals(outputName, StringComparison.OrdinalIgnoreCase)
            || FileEx.DeleteFile(inputName);
    }

    public static bool ReMux(string fileName)
    {
        // This function will only use MkvMerge
        // The file will be replaced with the remuxed file

        // Create a temp output filename
        string tempName = Path.ChangeExtension(fileName, ".tmp4");
        Debug.Assert(fileName != tempName);
        _ = FileEx.DeleteFile(tempName);

        // ReMux
        Log.Information("Remux using MkvMerge : {FileName}", fileName);
        if (!Tools.MkvMerge.ReMuxToMkv(fileName, tempName, out string error))
        {
            Log.Error("Failed to remux using MkvMerge : {FileName}", fileName);
            Log.Error("{Error}", error);
            _ = FileEx.DeleteFile(tempName);
            return false;
        }

        return FileEx.RenameFile(tempName, fileName);
    }
}
