using System;
using System.Diagnostics;
using System.Text;

// https://mkvtoolnix.download/doc/mkvmerge.html

namespace PlexCleaner
{
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

        public bool SetMkvTrackLanguage(string filename, MediaInfo unknown, string language)
        {
            if (unknown == null)
                throw new ArgumentNullException(nameof(unknown));

            // Verify correct data type
            Debug.Assert(unknown.Parser == ToolType.MkvMerge);

            // Set language on all unknown tracks
            StringBuilder commandline = new StringBuilder();
            commandline.Append($"\"{filename}\" {Options} ");
            foreach (TrackInfo track in unknown.GetTrackList())
            {
                commandline.Append($"--edit track:@{track.Number} --set language={language} ");
            }

            int exitcode = Command(commandline.ToString());
            return exitcode == 0;
        }

        public bool SetMkvTrackLanguage(string filename, int track, string language)
        {
            // Set track language
            string commandline = $"\"{filename}\" {Options} --edit track:@{track} --set language={language}";
            int exitcode = Command(commandline);
            return exitcode == 0;
        }

        public bool ClearMkvTags(string filename)
        {
            // Clear all tags
            string commandline = $"\"{filename}\" {Options} --tags all: --delete title";
            int exitcode = Command(commandline);
            return exitcode == 0;
        }

        public bool ClearMkvTags(string filename, MediaInfo info)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            // Verify correct data type
            Debug.Assert(info.Parser == ToolType.MkvMerge);

            // Clear all tags
            StringBuilder commandline = new StringBuilder();
            commandline.Append($"\"{filename}\" {Options} --tags all: --delete title ");

            // Delete all track titles
            foreach (TrackInfo track in info.GetTrackList())
            {
                // Add all tracks with a title that is not used in processing
                if (!string.IsNullOrEmpty(track.Title) &&
                    !MediaInfo.IsUsefulTrackTitle(track.Title))
                    commandline.Append($"--edit track:@{track.Number} --delete name ");
            }

            // Delete all attachments
            for (int id = 0; id < info.Attachments; id ++)
                commandline.Append($"--delete-attachment {id + 1} ");

            int exitcode = Command(commandline.ToString());
            return exitcode == 0;
        }

        private const string Options = "--flush-on-close";
    }
}
