using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

// https://mkvtoolnix.download/doc/mkvpropedit.html
// mkvpropedit [options] {source-filename} {actions}
// Use @ designation for track number from matroska header as discovered with mkvmerge identify

namespace PlexCleaner;

// Use MkvMerge family
public class MkvPropEditTool : MkvMergeTool
{
    public override ToolType GetToolType() => ToolType.MkvPropEdit;

    protected override string GetToolNameWindows() => "mkvpropedit.exe";

    protected override string GetToolNameLinux() => "mkvpropedit";

    public bool SetTrackLanguage(string fileName, MediaInfo mediaInfo)
    {
        // Verify correct data type
        Debug.Assert(mediaInfo.Parser == ToolType.MkvMerge);

        // Build commandline
        StringBuilder commandline = new();
        DefaultArgs(fileName, commandline);

        // Set the language property not the language-ietf property
        // https://gitlab.com/mbunkus/mkvtoolnix/-/wikis/Languages-in-Matroska-and-MKVToolNix#mkvpropedit

        // TODO: Should we be skipping und?
        // Only set tracks that are set and not undefined
        var trackList = mediaInfo.GetTrackList().Where(item => !Language.IsUndefined(item.LanguageAny)).ToList();
        trackList.ForEach(item => commandline.Append(CultureInfo.InvariantCulture, $"--edit track:@{item.Number} --set language={item.LanguageAny} "));

        // Set language on all unknown tracks
        int exitCode = Command(commandline.ToString());
        return exitCode == 0;
    }

    public bool SetTrackFlags(string fileName, MediaInfo mediaInfo)
    {
        // Verify correct data type
        Debug.Assert(mediaInfo.Parser == ToolType.MkvMerge);

        // Build commandline
        StringBuilder commandline = new();
        DefaultArgs(fileName, commandline);

        // Iterate over all tracks
        foreach (TrackInfo trackItem in mediaInfo.GetTrackList())
        {
            // Setting a flag does not unset the counter flag, e.g. setting default on one track does not unset default on other tracks
            // TODO: Should we set all flags for all tracks, cli gets very long, or only set flags
            // var flagList = TrackInfo.GetFlags().ToList();

            // Get flags list for this track
            var flagList = TrackInfo.GetFlags(trackItem.Flags).ToList();
            if (flagList.Count > 0)
            {
                // Edit track
                _ = commandline.Append(CultureInfo.InvariantCulture, $"--edit track:@{trackItem.Number} ");

                // Set flag by name
                flagList.ForEach(item => commandline.Append(CultureInfo.InvariantCulture, $"--set {GetTrackFlag(item)}=1 "));
            }
        }

        // Set flags
        int exitCode = Command(commandline.ToString());
        return exitCode == 0;
    }

    public static string GetTrackFlag(TrackInfo.FlagsType flagType) =>
        // mkvpropedit --list-property-names
        // Enums must be single flag values, not combined flags
        flagType switch
        {
            TrackInfo.FlagsType.Default => "flag-default",
            TrackInfo.FlagsType.Forced => "flag-forced",
            TrackInfo.FlagsType.HearingImpaired => "flag-hearing-impaired",
            TrackInfo.FlagsType.VisualImpaired => "flag-visual-impaired",
            TrackInfo.FlagsType.Descriptions => "flag-text-descriptions",
            TrackInfo.FlagsType.Original => "flag-original",
            TrackInfo.FlagsType.Commentary => "flag-commentary",
            TrackInfo.FlagsType.None => throw new NotImplementedException(),
            _ => throw new NotImplementedException(),
        };

    public bool ClearTags(string fileName, MediaInfo mediaInfo)
    {
        // Verify correct data type
        Debug.Assert(mediaInfo.Parser == ToolType.MkvMerge);

        // Build commandline
        StringBuilder commandline = new();
        DefaultArgs(fileName, commandline);
        _ = commandline.Append("--tags all: --delete title ");

        // Delete all track titles if the title is not a flag substitute
        var trackList = mediaInfo.GetTrackList().Where(track => track.NotTrackTitleFlag()).ToList();
        trackList.ForEach(track => commandline.Append(CultureInfo.InvariantCulture, $"--edit track:@{track.Number} --delete name "));

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
            _ = commandline.Append(CultureInfo.InvariantCulture, $"--delete-attachment {id + 1} ");
        }

        // Delete all attachments
        int exitCode = Command(commandline.ToString());
        return exitCode == 0;
    }

    private static void DefaultArgs(string fileName, StringBuilder commandline) =>
        // TODO: How to suppress console output?
        // if (Program.Options.Parallel)
        commandline.Append(CultureInfo.InvariantCulture, $"\"{fileName}\" {EditOptions} ");

    private const string EditOptions = "--delete-track-statistics-tags --normalize-language-ietf extlang";
}
