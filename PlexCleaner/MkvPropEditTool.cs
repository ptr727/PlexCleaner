using System.Diagnostics;
using System.Linq;
using System.Text;

// https://mkvtoolnix.download/doc/mkvpropedit.html
// Use @ designation for track number from matroska header, not mkvmerge track id

namespace PlexCleaner;

// Use MkvMerge family
public class MkvPropEditTool : MkvMergeTool
{
    public override ToolType GetToolType()
    {
        return ToolType.MkvPropEdit;
    }

    protected override string GetToolNameWindows()
    {
        return "mkvpropedit.exe";
    }

    protected override string GetToolNameLinux()
    {
        return "mkvpropedit";
    }

    public bool SetTrackLanguage(string fileName, MediaInfo mediaInfo, string language)
    {
        // Verify correct data type
        Debug.Assert(mediaInfo.Parser == ToolType.MkvMerge);

        // Build commandline
        StringBuilder commandline = new();
        DefaultArgs(fileName, commandline);
        mediaInfo.GetTrackList().ForEach(item => commandline.Append($"--edit track:@{item.Number} --set language={language} "));

        // Set language on all unknown tracks
        int exitCode = Command(commandline.ToString());
        return exitCode == 0;
    }

    public bool SetTrackLanguage(string fileName, int trackId, string language)
    {
        // Build commandline
        StringBuilder commandline = new();
        DefaultArgs(fileName, commandline);
        commandline.Append($"--edit track:@{trackId} --set language={language}");

        // Set track language
        int exitCode = Command(commandline.ToString());
        return exitCode == 0;
    }

    public bool ClearTags(string fileName)
    {
        // Build commandline
        StringBuilder commandline = new();
        DefaultArgs(fileName, commandline);
        commandline.Append("--tags all: --delete title");

        // Clear all tags and delete main title
        int exitCode = Command(commandline.ToString());
        return exitCode == 0;
    }

    public bool ClearTags(string fileName, MediaInfo mediaInfo)
    {
        // Verify correct data type
        Debug.Assert(mediaInfo.Parser == ToolType.MkvMerge);

        // Build commandline
        StringBuilder commandline = new();
        DefaultArgs(fileName, commandline);
        commandline.Append("--tags all: --delete title ");

        // Delete all track titles if the title is not considered "useful"
        // TODO: Consider using HasTags() or other methods to be more consistent
        var trackList = mediaInfo.GetTrackList().Where(track => !string.IsNullOrEmpty(track.Title) && !TrackInfo.IsUsefulTrackTitle(track.Title)).ToList();
        trackList.ForEach(track => commandline.Append($"--edit track:@{track.Number} --delete name "));

        // Clear all tags and main title and track titles
        int exitCode = Command(commandline.ToString());
        return exitCode == 0;
    }

    public bool ClearAttachments(string fileName, MediaInfo mediaInfo)
    {
        // Verify correct data type
        Debug.Assert(mediaInfo.Parser == ToolType.MkvMerge);
        Debug.Assert(mediaInfo.Attachments > 0);

        // Build commandline
        StringBuilder commandline = new();
        DefaultArgs(fileName, commandline);
        for (int id = 0; id < mediaInfo.Attachments; id++)
        {
            commandline.Append($"--delete-attachment {id + 1} ");
        }

        // Delete all attachments
        int exitCode = Command(commandline.ToString());
        return exitCode == 0;
    }

    private static void DefaultArgs(string fileName, StringBuilder commandline)
    {
        // TODO: How to suppress console output?
        // if (Program.Options.Parallel)
        commandline.Append($"\"{fileName}\" {EditOptions} ");
    }

    private const string EditOptions = "--flush-on-close --delete-track-statistics-tags --normalize-language-ietf extlang";
}
