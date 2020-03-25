using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Newtonsoft.Json;
using InsaneGenius.Utilities;

// We are using generated code to read the JSON
// https://quicktype.io/

namespace PlexCleaner
{
    public static class FfMpegTool
    { 
        public class FfProbeJson
        {
            [JsonProperty("streams")]
            public List<StreamJson> Streams { get; set; }

            public static FfProbeJson FromJson(string json) => JsonConvert.DeserializeObject<FfProbeJson>(json, Settings);

            private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
            {
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                DateParseHandling = DateParseHandling.None
            };
        }

        public class StreamJson
        {
            [JsonProperty("index")]
            public int Index { get; set; }
            [JsonProperty("codec_long_name")]
            public string CodecLongName { get; set; }
            [JsonProperty("codec_name")]
            public string CodecName { get; set; }
            [JsonProperty("codec_tag")]
            public string CodecTag { get; set; }
            [JsonProperty("codec_tag_string")]
            public string CodecTagString { get; set; }
            [JsonProperty("codec_type")]
            public string CodecType { get; set; }
            [JsonProperty("level")]
            public string Level { get; set; }
            [JsonProperty("profile")]
            public string Profile { get; set; }
            [JsonProperty("tags")]
            public TagsJson Tags { get; set; }
        }

        public class TagsJson
        {
            [JsonProperty("language")]
            public string Language { get; set; }
            [JsonProperty("mimetype")]
            public string MimeType { get; set; }
        }

        public static int FfMpeg(Config config, string parameters)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            string path = Tools.CombineToolPath(config, config.FfMpeg, FfMpegBinary);
            ConsoleEx.WriteLineTool($"FFMpeg : {parameters}");
            return ProcessEx.Execute(path, parameters);
        }

        public static int FfProbe(Config config, string parameters, out string output, out string error)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            string path = Tools.CombineToolPath(config, config.FfMpeg, FfProbeBinary);
            ConsoleEx.WriteLineTool($"FFProbe : {parameters}");
            return ProcessEx.Execute(path, parameters, out output, out error);
        }

        public static string GetToolPath(Config config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            return Tools.CombineToolPath(config, config.FfMpeg);
        }

        public static bool GetLatestVersion(ToolInfo toolinfo)
        {
            if (toolinfo == null)
                throw new ArgumentNullException(nameof(toolinfo));

            try
            {
                // Load the download page
                // TODO : Find a more reliable way of getting the last released version number
                // https://www.ffmpeg.org/download.html
                // https://ffmpeg.zeranoe.com/builds/
                HtmlWeb web = new HtmlWeb();
                HtmlDocument doc = web.Load(new Uri(@"https://www.ffmpeg.org/download.html"));

                // Get the download element and the download button
                HtmlNode download = doc.GetElementbyId("download");
                HtmlNodeCollection divs = download.SelectNodes("//div[contains(@class, 'btn-download-wrapper')]");
                if (divs.Count != 1) throw new ArgumentException($"Expecting only one node : {divs.Count}");

                // Get the current version URL from the first href
                HtmlNodeCollection anchors = divs.First().SelectNodes("a");
                if (anchors.Count != 2) throw new ArgumentException($"Expecting two nodes : {anchors.Count}");
                HtmlAttribute attr = anchors.First().Attributes["href"];
                string sourceurl = attr.Value;

                // Extract the version number from the URL
                // E.g. https://ffmpeg.org/releases/ffmpeg-3.4.tar.bz2
                // ffmpeg\.org\/releases\/ffmpeg-(.*?)\.tar\.bz2
                // TODO : Figure out how to use a named group
                string pattern = Regex.Escape(@"ffmpeg.org/releases/ffmpeg-") + @"(.*?)" + Regex.Escape(@".tar.bz2");
                Regex regex = new Regex(pattern);
                Match match = regex.Match(sourceurl);
                toolinfo.Version = match.Groups[1].Value;

                // Create download URL and the output filename using the version number
                // E.g. https://ffmpeg.zeranoe.com/builds/win64/static/ffmpeg-3.4-win64-static.zip
                toolinfo.FileName = $"ffmpeg-{toolinfo.Version}-win64-static.zip";
                toolinfo.Url = $"https://ffmpeg.zeranoe.com/builds/win64/static/{toolinfo.FileName}";
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return true;
        }

        private const string FfMpegBinary = @"bin\ffmpeg.exe";
        private const string FfProbeBinary = @"bin\ffprobe.exe";
    }
}
