using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

// https://mkvtoolnix.download/doc/mkvpropedit.html

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

    public bool SetTrackLanguage(string filename, MediaInfo unknown, string language)
    {
        if (unknown == null)
        {
            throw new ArgumentNullException(nameof(unknown));
        }

        // Verify correct data type
        Debug.Assert(unknown.Parser == ToolType.MkvMerge);

        // Set language on all unknown tracks
        StringBuilder commandline = new();
        commandline.Append($"\"{filename}\" {EditOptions} ");
        unknown.GetTrackList().ForEach(item => commandline.Append($"--edit track:@{item.Number} --set language={language} "));
        int exitCode = Command(commandline.ToString());
        return exitCode == 0;
    }

    public bool SetTrackLanguage(string filename, int track, string language)
    {
        // Set track language
        string commandline = $"\"{filename}\" {EditOptions} --edit track:@{track} --set language={language}";
        int exitCode = Command(commandline);
        return exitCode == 0;
    }

    public bool ClearTags(string filename)
    {
        // Clear all tags
        string commandline = $"\"{filename}\" {EditOptions} --tags all: --delete title";
        int exitCode = Command(commandline);
        return exitCode == 0;
    }

    public bool ClearTags(string filename, MediaInfo info)
    {
        if (info == null)
        {
            throw new ArgumentNullException(nameof(info));
        }

        // Verify correct data type
        Debug.Assert(info.Parser == ToolType.MkvMerge);

        // Clear all tags and main title
        StringBuilder commandline = new();
        commandline.Append($"\"{filename}\" {EditOptions} --tags all: --delete title ");

        // Delete all track titles if the title is not considered "useful"
        // TODO: Consider using HasTags() or other methods to be more consistent
        foreach (TrackInfo track in info.GetTrackList().Where(track => !string.IsNullOrEmpty(track.Title) &&
                                                                       !MediaInfo.IsUsefulTrackTitle(track.Title)))
        {
            commandline.Append($"--edit track:@{track.Number} --delete name ");
        }

        // Command
        int exitCode = Command(commandline.ToString());
        return exitCode == 0;
    }

    public bool ClearAttachments(string filename, MediaInfo info)
    {
        if (info == null)
        {
            throw new ArgumentNullException(nameof(info));
        }

        // Verify correct data type
        Debug.Assert(info.Parser == ToolType.MkvMerge);

        // Clear all tags
        StringBuilder commandline = new();
        commandline.Append($"\"{filename}\" {EditOptions} ");

        // Delete all attachments
        for (int id = 0; id < info.Attachments; id++)
        {
            commandline.Append($"--delete-attachment {id + 1} ");
        }

        // Command
        int exitCode = Command(commandline.ToString());
        return exitCode == 0;
    }

    private const string EditOptions = "--flush-on-close";
}
