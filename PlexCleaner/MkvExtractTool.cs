using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using InsaneGenius.Utilities;

// https://mkvtoolnix.download/doc/mkvextract.html

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
        MediaInfo extractTracks,
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
        foreach (VideoInfo info in extractTracks.Video)
        {
            outputFile = $"{inputName}.Track_{info.Id}.video";
            _ = FileEx.DeleteFile(outputFile);
            idToFileNames[info.Id] = outputFile;
            _ = output.Append(CultureInfo.InvariantCulture, $"{info.Id}:\"{outputFile}\" ");
        }
        foreach (AudioInfo info in extractTracks.Audio)
        {
            outputFile = $"{inputName}.Track_{info.Id}.audio";
            _ = FileEx.DeleteFile(outputFile);
            idToFileNames[info.Id] = outputFile;
            _ = output.Append(CultureInfo.InvariantCulture, $"{info.Id}:\"{outputFile}\" ");
        }
        foreach (SubtitleInfo info in extractTracks.Subtitle)
        {
            outputFile = $"{inputName}.Track_{info.Id}.subtitle";
            _ = FileEx.DeleteFile(outputFile);
            idToFileNames[info.Id] = outputFile;
            _ = output.Append(CultureInfo.InvariantCulture, $"{info.Id}:\"{outputFile}\" ");
        }

        // Extract
        string commandline = $"\"{inputName}\" tracks {ExtractOptions} {output}";
        int exitCode = Command(commandline);
        return exitCode is 0 or 1;
    }

    private const string ExtractOptions = "";
}
