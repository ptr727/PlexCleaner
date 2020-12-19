namespace PlexCleaner
{
    public class ToolsOptions
    {
        public bool UseSystem { get; set; }
        public string RootPath { get; set; } = @".\Tools\";
        public bool RootRelative { get; set; } = true;

        // Tool subfolders        
        public const string MkvToolNix = "MKVToolNix";
        public const string HandBrake = "HandBrake";
        public const string MediaInfo = "MediaInfo";
        public const string FfMpeg = "FFmpeg";
        public const string EchoArgs = "EchoArgs";
        public const string SevenZip = "7Zip";
    }
}
