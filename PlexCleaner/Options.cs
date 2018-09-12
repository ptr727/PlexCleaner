using System.Collections.Generic;
using CommandLine;

namespace PlexCleaner
{
    public class Options
    {
        // Commanline options
        [Option(HelpText = "Process the folders.")]
        public bool Process { get; set; }

        [Option(HelpText = "Re-Muxtiplex all files in the folders.")]
        public bool ReMux { get; set; }

        [Option(HelpText = "Re-Encode all files in the folders.")]
        public bool ReEncode { get; set; }

        [Option(HelpText = "Write sidecar files in the folders.")]
        public bool WriteSidecar { get; set; }

        [Option(HelpText = "Create a tag map for files in the folders.")]
        public bool CreateTagMap { get; set; }

        [Option(HelpText = "Check for new tools and download if available.")]
        public bool CheckForTools { get; set; }

        [Option(HelpText = "Monitor for changes and process the folders.")]
        public bool Monitor { get; set; }

        [Option(HelpText = "List of folders to process.")]
        public IEnumerable<string> Folders { get; set; }
    }
}
