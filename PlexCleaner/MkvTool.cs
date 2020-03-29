using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Xml.Serialization;
using System.IO;
using System.Net;
using System.IO.Compression;
using InsaneGenius.Utilities;
using System.Xml;

// We are using generated code to read JSON and XML
// https://quicktype.io/
// http://xmltocsharp.azurewebsites.net/

// Schema
// https://github.com/mbunkus/mkvtoolnix/tree/master/doc/json-schema


namespace PlexCleaner
{
    public static class MkvTool
    {
        internal class MkvMergeJson
        {
            [JsonProperty("tracks")]
            public List<TrackJson> Tracks { get; set; }

            public static MkvMergeJson FromJson(string json) =>
                JsonConvert.DeserializeObject<MkvMergeJson>(json, Settings);

            private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
            {
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                DateParseHandling = DateParseHandling.None
            };
        }

        internal class TrackJson
        {
            [JsonProperty("codec")]
            public string Codec { get; set; }

            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("properties")]
            public PropertiesJson Properties { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }
        }

        internal class PropertiesJson
        {
            [JsonProperty("codec_id")]
            public string CodecId { get; set; }

            [JsonProperty("language")]
            public string Language { get; set; }

            [JsonProperty("number")]
            public int Number { get; set; }
        }

        [XmlRoot(ElementName = "mkvtoolnix-releases")]
        internal class MkvToolNixReleasesXml
        {
            [XmlElement(ElementName = "latest-source")]
            public LatestSourceXml LatestSourceXml { get; set; }

            public static MkvToolNixReleasesXml FromXml(string xml)
            {
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(MkvToolNixReleasesXml));
                using TextReader textReader = new StringReader(xml);
                using XmlReader xmlReader = XmlReader.Create(textReader);
                return xmlSerializer.Deserialize(xmlReader) as MkvToolNixReleasesXml;
            }
        }

        [XmlRoot(ElementName = "latest-source")]
        internal class LatestSourceXml
        {
            [XmlElement(ElementName = "version")]
            public string Version { get; set; }
        }

        public static int MkvMerge(string parameters)
        {
            string path = Tools.CombineToolPath(Tools.Options.MkvToolNix, MkvMergeBinary);
            ConsoleEx.WriteLineTool($"MKVMerge : {parameters}");
            return ProcessEx.Execute(path, parameters);
        }

        public static int MkvMerge(string parameters, out string output)
        {
            string path = Tools.CombineToolPath(Tools.Options.MkvToolNix, MkvMergeBinary);
            ConsoleEx.WriteLineTool($"MKVMerge : {parameters}");
            return ProcessEx.Execute(path, parameters, out output);
        }

/*
        public static int MkvInfo(string parameters)
        {
            string path = Tools.CombineToolPath(Settings.Default.MKVToolNix, @"mkvinfo.exe");
            return Tools.Execute(path, parameters);
        }
*/

        public static int MkvPropEdit(string parameters)
        {
            string path = Tools.CombineToolPath(Tools.Options.MkvToolNix, MkvPropEditBinary);
            ConsoleEx.WriteLineTool($"MKVPropEdit : {parameters}");
            return ProcessEx.Execute(path, parameters);
        }

        public static string GetToolPath()
        {
            return Tools.CombineToolPath(Tools.Options.MkvToolNix);
        }

        public static bool GetLatestVersion(ToolInfo toolinfo)
        {
            if (toolinfo == null)
                throw new ArgumentNullException(nameof(toolinfo));

            try
            {
                // Download latest release file
                // https://mkvtoolnix.download/latest-release.xml.gz
                using WebClient wc = new WebClient();
                Stream wcstream = wc.OpenRead("https://mkvtoolnix.download/latest-release.xml.gz");
                if (wcstream == null) throw new ArgumentNullException(nameof(toolinfo));

                // Get XML from Gzip
                using GZipStream gzstream = new GZipStream(wcstream, CompressionMode.Decompress);
                using StreamReader sr = new StreamReader(gzstream);
                string xml = sr.ReadToEnd();

                // Get the version number from XML
                MkvToolNixReleasesXml mkvtools = MkvToolNixReleasesXml.FromXml(xml);
                toolinfo.Version = mkvtools.LatestSourceXml.Version;

                // Create download URL and the output filename using the version number
                // E.g. https://mkvtoolnix.download/windows/releases/18.0.0/mkvtoolnix-64-bit-18.0.0.7z
                toolinfo.FileName = $"mkvtoolnix-64-bit-{toolinfo.Version}.7z";
                toolinfo.Url = $"https://mkvtoolnix.download/windows/releases/{toolinfo.Version}/{toolinfo.FileName}";
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return true;
        }

        private const string MkvMergeBinary = @"mkvmerge.exe";
        private const string MkvPropEditBinary = @"mkvpropedit.exe";
    }
}