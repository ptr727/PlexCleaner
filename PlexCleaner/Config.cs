using Newtonsoft.Json;
using System.IO;

namespace PlexCleaner
{
    public class Config
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

        public string KeepExtensions { get; set; } = "";
        public string ReMuxExtensions { get; set; } = ".avi,.m2ts,.ts,.vob,.mp4,.m4v,.asf,.wmv";
        public string ReEncodeVideoCodec { get; set; } = "mpeg2video,msmpeg4v3,h264";
        public string ReEncodeVideoProfile { get; set; } = "*,*,Constrained Baseline@30";
        public string DefaultLanguage { get; set; } = "eng";
        public int VideoEncodeQuality { get; set; } = 20;
        public string KeepLanguages { get; set; } = "eng,afr,chi,ind";
        public string ReEncodeAudioCodec { get; set; } = "flac,mp2,vorbis,wmapro";
        public string AudioEncodeCodec { get; set; } = "ac3";

        public bool DeleteEmptyFolders { get; set; } = true;
        public bool UseSidecarFiles { get; set; } = true;
        public bool DeleteUnwantedExtensions { get; set; } = true;
        public bool RemuxExtensions { get; set; } = true;
        public bool DeInterlace { get; set; } = true;
        public bool ReEncode { get; set; } = true;
        public bool SetUnknownLanguage { get; set; } = true;
        public bool RemoveUnwanted { get; set; } = false;
        public bool ReMux { get; set; } = false;

        public int MonitorWaitTime { get; set; } = 60;
        public int FileRetryWaitTime { get; set; } = 5;
        public int FileRetryCount { get; set; } = 2;
        public bool DeleteFailedFiles { get; set; } = true;
        public bool TestNoModify { get; set; } = false;
        public bool TestSnippets { get; set; } = false;

        public static Config FromFile(string path)
        {
            return FromJson(File.ReadAllText(path));
        }

        public static void ToFile(string path, Config settings)
        {
            File.WriteAllText(path, ToJson(settings));
        }

        public static string ToJson(Config settings) =>
            JsonConvert.SerializeObject(settings, JsonSettings);

        public static Config FromJson(string json) =>
            JsonConvert.DeserializeObject<Config>(json, JsonSettings);

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Formatting = Formatting.Indented
        };
    }
}