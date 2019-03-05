using System.Collections.Generic;
using CommandLine;

namespace PlexCleaner
{
    // Commandline options
    public class Commands
    {
        [Option(HelpText = "Process the files or folders.")]
        public bool Process { get; set; }

        [Option(HelpText = "Re-Muxtiplex the files.")]
        public bool ReMux { get; set; }

        [Option(HelpText = "Re-Encode the files.")]
        public bool ReEncode { get; set; }

        [Option(HelpText = "Write sidecar files.")]
        public bool WriteSidecar { get; set; }

        [Option(HelpText = "Create a tag map for the files.")]
        public bool CreateTagMap { get; set; }

        [Option(HelpText = "Check for new tools and download if available.")]
        public bool CheckForTools { get; set; }

        [Option(HelpText = "Monitor for changes in folders, and process any changed files.")]
        public bool Monitor { get; set; }

        [Option(HelpText = "List of folders to process.")]
        public IEnumerable<string> Folders { get; set; }

        [Option(HelpText = "List of files to process.")]
        public IEnumerable<string> Files { get; set; }
    }
}
