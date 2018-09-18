using System;
using System.Collections.Generic;
using System.Text;

namespace PlexCleaner
{
    public class ToolOptions
    {
        public ToolOptions()
        {
            Default = this;
        }
        public static ToolOptions Default;

        public string RootPath { get; set; } = @".\Tools\";
        public bool RootRelative { get; set; } = true;
        public string MKVToolNix { get; set; } = "MKVToolNix";
        public string Handbrake { get; set; } = "Handbrake";
        public string MediaInfo { get; set; } = "MediaInfo";
        public string FFMpeg { get; set; } = "FFMpeg";
        public string EchoArgs { get; set; } = "EchoArgs";
        public string SevenZip { get; set; } = "7Zip";
    }

    public class EncodeOptions
    {
        public EncodeOptions()
        {
            Default = this;
        }
        public static EncodeOptions Default;

        public string KeepExtensions { get; set; } = "";
        public string ReMuxExtensions { get; set; } = ".avi,.m2ts,.ts,.vob,.mp4,.m4v,.asf,.wmv";
        public string ReEncodeVideoCodec { get; set; } = "mpeg2video,msmpeg4v3,h264";
        public string ReEncodeVideoProfile { get; set; } = "*,*,Constrained Baseline@30";
        public string DefaultLanguage { get; set; } = "eng";
        public int VideoEncodeQuality { get; set; } = 20;
        public string KeepLanguages { get; set; } = "eng,afr,chi,ind";
        public string ReEncodeAudioCodec { get; set; } = "flac,mp2,vorbis,wmapro";
        public string AudioEncodeCodec { get; set; } = "ac3";
    }

    public class AppOptions
    {
        public AppOptions()
        {
            Default = this;
        }
        public static AppOptions Default;

        public int MonitorWaitTime { get; set; } = 60;
        public int FileRetryWaitTime { get; set; } = 60;
        public int FileRetryCount { get; set; } = 30;
        public bool DeleteEmptyFolders { get; set; } = true;
        public bool UseSidecarFiles { get; set; } = true;
        public bool DeleteFailedFiles { get; set; } = true;
        public bool TestNoModify { get; set; } = false;
        public bool TestSnippets { get; set; } = false;
    }

    public class Options
    {
        public Options()
        {
            Default = this;
        }
        public static Options Default;

        public ToolOptions Tools { get; set; } = new ToolOptions();
        public EncodeOptions Encode { get; set; } = new EncodeOptions();
        public AppOptions App { get; set; } = new AppOptions();
    }
}
