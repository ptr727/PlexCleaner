using System;
using System.Diagnostics;
using System.IO;
using InsaneGenius.Utilities;
using Serilog;

namespace PlexCleaner;

// Conditional processing:
//   Program.Options.TestNoModify

public static class Convert
{
    public static bool ConvertToMkv(string inputName, out string outputName) =>
        ConvertToMkv(inputName, null, out outputName);

    public static bool ConvertToMkv(
        string inputName,
        SelectMediaInfo selectMediaInfo,
        out string outputName
    )
    {
        // Match the logic in ReMuxToMKV()

        // Test
        if (Program.Options.TestNoModify)
        {
            outputName = inputName;
            return true;
        }

        // Create a temp filename based on the input name
        outputName = Path.ChangeExtension(inputName, ".mkv");
        Debug.Assert(inputName != outputName);
        string tempName = Path.ChangeExtension(inputName, ".tmp1");
        Debug.Assert(inputName != tempName);

        // Convert using ffmpeg
        // Selected is ReEncode
        // NotSelected is Keep
        Log.Information("ReEncode using FfMpeg : {FileName}", inputName);
        bool result =
            selectMediaInfo == null
                ? Tools.FfMpeg.ConvertToMkv(inputName, tempName)
                : Tools.FfMpeg.ConvertToMkv(inputName, selectMediaInfo, tempName);
        if (!result)
        {
            Log.Error("ReEncode using FfMpeg failed : {FileName}", inputName);
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
        // Match the logic in ConvertToMKV()

        // Test
        if (Program.Options.TestNoModify)
        {
            outputName = inputName;
            return true;
        }

        // Create a MKV and temp filename based on the input name
        outputName = Path.ChangeExtension(inputName, ".mkv");
        Debug.Assert(inputName != outputName);
        string tempName = Path.ChangeExtension(inputName, ".tmp2");
        Debug.Assert(inputName != tempName);

        // MkvMerge and FfMpeg both have problems dealing with some AVI files, so we will try both
        // E.g. https://github.com/FFmpeg/FFmpeg/commit/8de1ee9f725aa3c550f425bd3120bcd95d5b2ea8
        // E.g. https://github.com/mbunkus/mkvtoolnix/issues/2123

        // Try MKV first
        Log.Information("ReMux using MkvMerge : {FileName}", inputName);
        if (!Tools.MkvMerge.ReMuxToMkv(inputName, tempName))
        {
            // Failed, delete temp file
            _ = FileEx.DeleteFile(tempName);

            // Cancel requested
            if (Program.IsCancelledError())
            {
                return false;
            }

            // Failed
            Log.Error("ReMux using MkvMerge failed : {FileName}", inputName);

            // Retry using FfMpeg
            Log.Information("ReMux using FfMpeg : {FileName}", inputName);
            if (!Tools.FfMpeg.ReMuxToMkv(inputName, tempName))
            {
                // Failed, delete temp file
                _ = FileEx.DeleteFile(tempName);

                // Cancel requested
                if (Program.IsCancelledError())
                {
                    return false;
                }

                // Error
                Log.Error("ReMux using FfMpeg failed : {FileName}", inputName);
                return false;
            }

            // Remux using MkvMerge after FfMpeg or HandBrake encoding
            Log.Information("ReMux using MkvMerge : {FileName}", inputName);
            if (!MkvProcess.ReMux(tempName))
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
        SelectMediaInfo selectMediaInfo,
        out string outputName
    )
    {
        if (selectMediaInfo == null)
        {
            return ReMuxToMkv(inputName, out outputName);
        }

        // This only works on MKV files and MkvMerge MediaInfo types
        Debug.Assert(selectMediaInfo.Selected.Parser == MediaTool.ToolType.MkvMerge);
        Debug.Assert(SidecarFile.IsMkvFile(inputName));

        // Match the logic in ConvertToMKV()

        // Test
        if (Program.Options.TestNoModify)
        {
            outputName = inputName;
            return true;
        }

        // Create a temp filename based on the input name
        outputName = Path.ChangeExtension(inputName, ".mkv");
        Debug.Assert(inputName != outputName);
        string tempName = Path.ChangeExtension(inputName, ".tmp3");
        Debug.Assert(inputName != tempName);

        // Remux keeping specific tracks
        // Selected is Keep
        // NotSelected is Remove
        Log.Information("ReMux using MkvMerge : {FileName}", inputName);
        if (!Tools.MkvMerge.ReMuxToMkv(inputName, selectMediaInfo, tempName))
        {
            Log.Error("ReMux using MkvMerge failed : {FileName}", inputName);
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
}
