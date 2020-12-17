using System;
using System.IO;
using System.Net;
using System.IO.Compression;
using InsaneGenius.Utilities;
using System.Linq;
using System.Globalization;
using System.Diagnostics;
using System.Runtime.InteropServices;

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
            Debug.Assert(unknown.Parser == MediaTool.ToolType.MkvMerge);

            // Mark all unknown  tracks
            return unknown.GetTrackList().All(track => SetMkvTrackLanguage(filename, track.Number, language));
        }

        public bool SetMkvTrackLanguage(string filename, int track, string language)
        {
            // Set track language
            string commandline = $"\"{filename}\" --edit track:@{track} --set language={language}";
            int exitcode = Command(commandline);
            return exitcode == 0;
        }

        public bool ClearMkvTags(string filename)
        {
            // Clear all tags
            string commandline = $"\"{filename}\" --tags all: --delete title";
            int exitcode = Command(commandline);
            return exitcode == 0;
        }
    }
}
