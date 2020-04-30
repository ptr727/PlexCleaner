using InsaneGenius.Utilities;
using System;

namespace PlexCleaner
{
    public class ToolInfo
    {
        public string FileName { get; set; }
        public DateTime ModifiedTime { get; set; }
        public long Size { get; set; }
        public string Tool { get; set; }
        public string Url { get; set; }
        public string Version { get; set; }

        public void WriteLine(string prefix)
        {
            ConsoleEx.WriteLine($"{prefix} : {Version}, {FileName}, {Size}, {ModifiedTime}");
        }
    }
}
