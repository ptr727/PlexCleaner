using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using InsaneGenius.Utilities;

// https://mkvtoolnix.download/doc/mkvextract.html

namespace PlexCleaner;

// Use MkvMerge family
public class MkvExtractTool : MkvMergeTool
{
    public override ToolType GetToolType()
    {
        return ToolType.MkvExtract;
    }

    protected override string GetToolNameWindows()
    {
        return "mkvextract.exe";
    }

    protected override string GetToolNameLinux()
    {
        return "mkvextract";
    }

    public bool ExtractToFile(string inputName, int trackId, string outputName)
    {
        // Delete existing output file
        FileEx.DeleteFile(outputName);

        // Extract track to file
        string commandline = $"\"{inputName}\" tracks {ExtractOptions} {trackId}:\"{outputName}\"";
        int exitCode = Command(commandline);
        return exitCode is 0 or 1;
    }

    public bool ExtractToFiles(string inputName, MediaInfo extractTracks, out Dictionary<int, string> idToFileNames)
    {
        // Verify correct data type
        Debug.Assert(extractTracks.Parser == ToolType.MkvMerge);

        // Create the track ids and destination filenames using the input name and track ids
        // The track numbers are reported by MkvMerge --identify, use the track.id values
        idToFileNames = new Dictionary<int, string>();
        StringBuilder output = new();
        string outputFile;
        foreach (VideoInfo info in extractTracks.Video)
        {
            outputFile = $"{inputName}.Track_{info.Id}.video";
            FileEx.DeleteFile(outputFile);
            idToFileNames[info.Id] = outputFile;
            output.Append($"{info.Id}:\"{outputFile}\" ");
        }
        foreach (AudioInfo info in extractTracks.Audio)
        {
            outputFile = $"{inputName}.Track_{info.Id}.audio";
            FileEx.DeleteFile(outputFile);
            idToFileNames[info.Id] = outputFile;
            output.Append($"{info.Id}:\"{outputFile}\" ");
        }
        foreach (SubtitleInfo info in extractTracks.Subtitle)
        {
            outputFile = $"{inputName}.Track_{info.Id}.subtitle";
            FileEx.DeleteFile(outputFile);
            idToFileNames[info.Id] = outputFile;
            output.Append($"{info.Id}:\"{outputFile}\" ");
        }

        // Extract
        string commandline = $"\"{inputName}\" tracks {ExtractOptions} {output}";
        int exitCode = Command(commandline);
        return exitCode is 0 or 1;
    }

    // private const string ExtractOptions = "--flush-on-close";
    private const string ExtractOptions = "";
}
