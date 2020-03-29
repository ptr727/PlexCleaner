using System;
using System.Collections.Generic;
using System.Text;

namespace PlexCleaner
{
    public class ToolsOptions
    {
        public string RootPath { get; set; } = @".\Tools\";
        public bool RootRelative { get; set; } = true;
        public string MkvToolNix { get; set; } = "MKVToolNix";
        public string Handbrake { get; set; } = "Handbrake";
        public string MediaInfo { get; set; } = "MediaInfo";
        public string FfMpeg { get; set; } = "FFMpeg";
        public string HandBrake { get; set; } = "HandBrake";
        public string EchoArgs { get; set; } = "EchoArgs";
        public string SevenZip { get; set; } = "7Zip";
    }
}
