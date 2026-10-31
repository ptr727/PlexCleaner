using System.Diagnostics;

namespace PlexCleaner;

public static class Convert
{
    public static bool ConvertToMkv(
        string inputName,
        SelectMediaProps selectMediaProps,
        MediaProps languageProps,
        out string outputName
    )
    {
        // Match the logic in ReMuxToMKV()

        // Create a MKV and temp filename based on the input name
        // Input may already be MKV file
        outputName = Path.ChangeExtension(inputName, ".mkv");
        string tempName = Path.ChangeExtension(inputName, ".tmp1");
        Debug.Assert(inputName != tempName);
        if (!CanReplace(inputName, outputName))
        {
            return false;
        }

        // Convert using ffmpeg
        // Selected is ReEncode
        // NotSelected is Keep
        Log.Information("Reencode using FfMpeg : {FileName}", inputName);
        if (!Tools.FfMpeg.ConvertToMkv(inputName, selectMediaProps, tempName))
        {
            Log.Error("Failed to reencode using FfMpeg : {FileName}", inputName);
            File.Delete(tempName);
            return false;
        }

        if (!ReMux(tempName))
        {
            File.Delete(tempName);
            return false;
        }

        // FfMpeg discards IETF language tags, restore them before the output replaces the input
        if (!Tools.MkvPropEdit.SetTrackLanguage(tempName, languageProps))
        {
            File.Delete(tempName);
            return false;
        }

        ReplaceInput(inputName, tempName, outputName);
        return true;
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
        if (!CanReplace(inputName, outputName))
        {
            return false;
        }

        // MkvMerge and FfMpeg both have problems dealing with some AVI files, so we will try both
        // E.g. https://github.com/FFmpeg/FFmpeg/commit/8de1ee9f725aa3c550f425bd3120bcd95d5b2ea8
        // E.g. https://github.com/mbunkus/mkvtoolnix/issues/2123

        // Try MKV first
        Log.Information("Remux using MkvMerge : {FileName}", inputName);
        if (!Tools.MkvMerge.ReMuxToMkv(inputName, tempName))
        {
            // Failed, delete temp file
            File.Delete(tempName);

            // Cancel requested
            if (Program.IsCancelledError())
            {
                return false;
            }

            Log.Error("Failed to remux using MkvMerge : {FileName}", inputName);

            // Retry using FfMpeg
            Log.Information("Remux using FfMpeg : {FileName}", inputName);
            if (!Tools.FfMpeg.ReMuxToMkv(inputName, tempName))
            {
                // Failed, delete temp file
                File.Delete(tempName);

                // Cancel requested
                if (Program.IsCancelledError())
                {
                    return false;
                }

                Log.Error("Failed to remux using FfMpeg : {TempFileName}", tempName);

                // Error
                return false;
            }

            // ReMux using MkvMerge after FfMpeg or HandBrake encoding
            if (!ReMux(tempName))
            {
                File.Delete(tempName);
                return false;
            }
        }

        ReplaceInput(inputName, tempName, outputName);
        return true;
    }

    public static bool ReMuxToMkv(
        string inputName,
        SelectMediaProps? selectMediaProps,
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
        if (!CanReplace(inputName, outputName))
        {
            return false;
        }

        // ReMux keeping specific tracks
        // Selected is Keep
        // NotSelected is Remove
        Log.Information("Remux using MkvMerge : {FileName}", inputName);
        if (!Tools.MkvMerge.ReMuxToMkv(inputName, selectMediaProps, tempName))
        {
            Log.Error("Failed to remux using MkvMerge : {FileName}", inputName);
            File.Delete(tempName);
            return false;
        }

        ReplaceInput(inputName, tempName, outputName);
        return true;
    }

    public static bool ReMux(string fileName)
    {
        // This function will only use MkvMerge
        // The file will be replaced with the remuxed file

        // Create a temp output filename
        string tempName = Path.ChangeExtension(fileName, ".tmp4");
        Debug.Assert(fileName != tempName);

        // ReMux
        Log.Information("Remux using MkvMerge : {FileName}", fileName);
        if (!Tools.MkvMerge.ReMuxToMkv(fileName, tempName))
        {
            Log.Error("Failed to remux using MkvMerge : {FileName}", fileName);
            File.Delete(tempName);
            return false;
        }

        // Rename the temp file to the original file
        File.Move(tempName, fileName, true);

        return true;
    }

    // A rename or move onto targetName replaces an existing entry other than fileName itself
    public static bool WouldReplaceOtherFile(string fileName, string targetName) =>
        Path.Exists(targetName) && !IsSameFile(fileName, targetName);

    private static bool IsSameFile(string fileName, string targetName)
    {
        if (fileName.Equals(targetName, StringComparison.Ordinal))
        {
            return true;
        }
        if (!fileName.Equals(targetName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // A case-only rename names the file itself unless the directory lists the exact target name
        string targetFileName = Path.GetFileName(targetName);
        return Path.Exists(targetName)
            && !Directory
                .EnumerateFileSystemEntries(
                    Path.GetDirectoryName(Path.GetFullPath(targetName))!,
                    targetFileName,
                    new EnumerationOptions
                    {
                        MatchCasing = MatchCasing.CaseInsensitive,
                        AttributesToSkip = 0,
                    }
                )
                .Any(entry =>
                    Path.GetFileName(entry).Equals(targetFileName, StringComparison.Ordinal)
                );
    }

    private static bool CanReplace(string inputName, string outputName)
    {
        if (!WouldReplaceOtherFile(inputName, outputName))
        {
            return true;
        }
        Log.Error("Output file already exists : {OutputName} : {FileName}", outputName, inputName);
        return false;
    }

    private static void ReplaceInput(string inputName, string tempName, string outputName)
    {
        // Overwrite only the input itself, so a file created at the output name since the check fails the move
        bool inPlace = IsSameFile(inputName, outputName);
        File.Move(tempName, outputName, inPlace);
        if (inPlace)
        {
            return;
        }

        // The media is at the output name, so a failed delete still reports the output and the modification
        try
        {
            File.Delete(inputName);
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e))
        {
            Log.Error(
                "Failed to delete the input after moving to the output : {FileName}",
                inputName
            );
        }
    }
}
