using InsaneGenius.Utilities;
using System;
using System.Collections.Generic;
using System.Text;

namespace PlexCleaner
{
    public class ToolInfo
    {
        public string FileName { get; set; }
        public string ModifiedTime { get; set; }
        public long Size { get; set; }
        public string Tool { get; set; }
#pragma warning disable CA1056 // Uri properties should not be strings
        public string Url { get; set; }
#pragma warning restore CA1056 // Uri properties should not be strings
        public string Version { get; set; }

        public void WriteLine(string prefix)
        {
            ConsoleEx.WriteLine($"{prefix} : {Version}, {FileName}, {Size}, {ModifiedTime}");
        }
    }
}
