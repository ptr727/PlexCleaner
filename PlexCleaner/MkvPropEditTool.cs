using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

// https://mkvtoolnix.download/doc/mkvpropedit.html
// mkvpropedit [options] {source-filename} {actions}
// Use @ designation for track number from matroska header as discovered with mkvmerge identify

// TODO: How to suppress console output?

namespace PlexCleaner;

// Use MkvMerge family
public class MkvPropEditTool : MkvMergeTool
{
    public override ToolType GetToolType() => ToolType.MkvPropEdit;

    protected override string GetToolNameWindows() => "mkvpropedit.exe";

    protected override string GetToolNameLinux() => "mkvpropedit";

    public bool SetTrackLanguage(string fileName, MediaProps mediaProps)
    {
        // Verify correct data type
        Debug.Assert(mediaProps.Parser == ToolType.MkvMerge);

        // Build commandline
        StringBuilder commandline = new();
        DefaultArgs(fileName, commandline);

        // Set the language property not the language-ietf property
        // https://gitlab.com/mbunkus/mkvtoolnix/-/wikis/Languages-in-Matroska-and-MKVToolNix#mkvpropedit

        // Only set tracks that are set and not undefined
        System.Collections.Generic.List<TrackProps> trackList =
        [
            .. mediaProps.GetTrackList().Where(item => !Language.IsUndefined(item.LanguageAny)),
        ];
        trackList.ForEach(item =>
            commandline.Append(
                CultureInfo.InvariantCulture,
                $"--edit track:@{item.Number} --set language={item.LanguageAny} "
            )
        );

        // Set language on all unknown tracks
        int exitCode = Command(commandline.ToString(), out string _, out string _);
        return exitCode == 0;
    }

    public bool SetTrackFlags(string fileName, MediaProps mediaProps)
    {
        // Verify correct data type
        Debug.Assert(mediaProps.Parser == ToolType.MkvMerge);

        // Build commandline
        StringBuilder commandline = new();
        DefaultArgs(fileName, commandline);

        // Iterate over all tracks
        foreach (TrackProps item in mediaProps.GetTrackList())
        {
            // Setting a flag does not unset the counter flag, e.g. setting default on one track does not unset default on other tracks

            // Get flags list for this track
            System.Collections.Generic.List<TrackProps.FlagsType> flagList =
            [
                .. TrackProps.GetFlags(item.Flags),
            ];
            if (flagList.Count > 0)
            {
                // Edit track
                _ = commandline.Append(
                    CultureInfo.InvariantCulture,
                    $"--edit track:@{item.Number} "
                );

                // Set flag by name
                flagList.ForEach(item =>
                    commandline.Append(
                        CultureInfo.InvariantCulture,
                        $"--set {GetTrackFlag(item)}=1 "
                    )
                );
            }
        }

        // Set flags
        int exitCode = Command(commandline.ToString(), out string _, out string _);
        return exitCode == 0;
    }

    public static string GetTrackFlag(TrackProps.FlagsType flagType) =>
        // mkvpropedit --list-property-names
        // Enums must be single flag values, not combined flags
        flagType switch
        {
            TrackProps.FlagsType.Default => "flag-default",
            TrackProps.FlagsType.Forced => "flag-forced",
            TrackProps.FlagsType.HearingImpaired => "flag-hearing-impaired",
            TrackProps.FlagsType.VisualImpaired => "flag-visual-impaired",
            TrackProps.FlagsType.Descriptions => "flag-text-descriptions",
            TrackProps.FlagsType.Original => "flag-original",
            TrackProps.FlagsType.Commentary => "flag-commentary",
            TrackProps.FlagsType.None => throw new NotImplementedException(),
            _ => throw new NotImplementedException(),
        };

    public bool ClearTags(string fileName, MediaProps mediaProps)
    {
        // Verify correct data type
        Debug.Assert(mediaProps.Parser == ToolType.MkvMerge);

        // Delete all tags and title
        StringBuilder commandline = new();
        DefaultArgs(fileName, commandline);
        _ = commandline.Append("--tags all: --delete title ");

        // Delete track titles if the title is not used as a flag
        System.Collections.Generic.List<TrackProps> trackList =
        [
            .. mediaProps.GetTrackList().Where(track => !track.TitleContainsFlag()),
        ];
        trackList.ForEach(track =>
            commandline.Append(
                CultureInfo.InvariantCulture,
                $"--edit track:@{track.Number} --delete name "
            )
        );

        // Clear all tags and main title and track titles
        int exitCode = Command(commandline.ToString(), out string _, out string _);
        return exitCode == 0;
    }

    public bool ClearAttachments(string fileName, MediaProps mediaProps)
    {
        // Verify correct data type
        Debug.Assert(mediaProps.Parser == ToolType.MkvMerge);
        Debug.Assert(mediaProps.Attachments > 0);

        // Delete all attachments
        StringBuilder commandline = new();
        DefaultArgs(fileName, commandline);
        for (int id = 0; id < mediaProps.Attachments; id++)
        {
            _ = commandline.Append(CultureInfo.InvariantCulture, $"--delete-attachment {id + 1} ");
        }

        int exitCode = Command(commandline.ToString(), out string _, out string _);
        return exitCode == 0;
    }

    private static void DefaultArgs(string fileName, StringBuilder commandline) =>
        commandline.Append(CultureInfo.InvariantCulture, $"\"{fileName}\" {EditOptions} ");

    private const string EditOptions =
        "--delete-track-statistics-tags --normalize-language-ietf extlang";
}
