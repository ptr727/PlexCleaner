using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using InsaneGenius.Utilities;

// https://mkvtoolnix.download/doc/mkvextract.html
// mkvextract {source-filename} {mode1} [options] [extraction-spec1] [mode2] [options] [extraction-spec2] [...]

namespace PlexCleaner;

// Use MkvMerge family
public class MkvExtractTool : MkvMergeTool
{
    public override ToolType GetToolType() => ToolType.MkvExtract;

    protected override string GetToolNameWindows() => "mkvextract.exe";

    protected override string GetToolNameLinux() => "mkvextract";

    public bool ExtractToFile(string inputName, int trackId, string outputName)
    {
        // Delete existing output file
        _ = FileEx.DeleteFile(outputName);

        // Extract track to file
        string commandline = $"\"{inputName}\" tracks {ExtractOptions} {trackId}:\"{outputName}\"";
        int exitCode = Command(commandline);
        return exitCode is 0 or 1;
    }

    public bool ExtractToFiles(
        string inputName,
        MediaProps extractTracks,
        out Dictionary<int, string> idToFileNames
    )
    {
        // Verify correct data type
        Debug.Assert(extractTracks.Parser == ToolType.MkvMerge);

        // Create the track ids and destination filenames using the input name and track ids
        // The track numbers are reported by MkvMerge --identify, use the track.id values
        idToFileNames = [];
        StringBuilder output = new();
        string outputFile;
        foreach (VideoProps item in extractTracks.Video)
        {
            outputFile = $"{inputName}.Track_{item.Id}.video";
            _ = FileEx.DeleteFile(outputFile);
            idToFileNames[item.Id] = outputFile;
            _ = output.Append(CultureInfo.InvariantCulture, $"{item.Id}:\"{outputFile}\" ");
        }
        foreach (AudioProps item in extractTracks.Audio)
        {
            outputFile = $"{inputName}.Track_{item.Id}.audio";
            _ = FileEx.DeleteFile(outputFile);
            idToFileNames[item.Id] = outputFile;
            _ = output.Append(CultureInfo.InvariantCulture, $"{item.Id}:\"{outputFile}\" ");
        }
        foreach (SubtitleProps item in extractTracks.Subtitle)
        {
            outputFile = $"{inputName}.Track_{item.Id}.subtitle";
            _ = FileEx.DeleteFile(outputFile);
            idToFileNames[item.Id] = outputFile;
            _ = output.Append(CultureInfo.InvariantCulture, $"{item.Id}:\"{outputFile}\" ");
        }

        // Extract
        string commandline = $"\"{inputName}\" tracks {ExtractOptions} {output}";
        int exitCode = Command(commandline);
        return exitCode is 0 or 1;
    }

    private const string ExtractOptions = "";
}
