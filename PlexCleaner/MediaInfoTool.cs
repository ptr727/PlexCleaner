using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using InsaneGenius.Utilities;

// We are using genrated code to read XML
// http://xmltocsharp.azurewebsites.net/

namespace PlexCleaner
{
    public static class MediaInfoTool
    {
        [XmlRoot(ElementName = "MediaInfo", Namespace = "https://mediaarea.net/mediainfo")]
        public class MediaInfoXml
        {
            [XmlElement(ElementName = "media", Namespace = "https://mediaarea.net/mediainfo")]
            public MediaXml Media { get; set; }

            public static MediaInfoXml FromXml(string xml)
            {
                XmlSerializer xmlserializer = new XmlSerializer(typeof(MediaInfoXml));
                using TextReader textreader = new StringReader(xml);
                using XmlReader xmlReader = XmlReader.Create(textreader);
                return xmlserializer.Deserialize(xmlReader) as MediaInfoXml;
            }
        }

        [XmlRoot(ElementName = "media", Namespace = "https://mediaarea.net/mediainfo")]
        public class MediaXml
        {
            [XmlElement(ElementName = "track", Namespace = "https://mediaarea.net/mediainfo")]
            public List<TrackXml> Track { get; set; }
        }


        [XmlRoot(ElementName = "track", Namespace = "https://mediaarea.net/mediainfo")]
        public class TrackXml
        {
            [XmlElement(ElementName = "Format", Namespace = "https://mediaarea.net/mediainfo")]
            public string Format { get; set; }

            [XmlElement(ElementName = "Format_Version", Namespace = "https://mediaarea.net/mediainfo")]
            public string FormatVersion { get; set; }

            [XmlAttribute(AttributeName = "type")]
            public string Type { get; set; }

            [XmlElement(ElementName = "StreamOrder", Namespace = "https://mediaarea.net/mediainfo")]
            public string StreamOrder { get; set; }

            [XmlElement(ElementName = "ID", Namespace = "https://mediaarea.net/mediainfo")]
            public string Id { get; set; }

            [XmlElement(ElementName = "Format_Profile", Namespace = "https://mediaarea.net/mediainfo")]
            public string FormatProfile { get; set; }

            [XmlElement(ElementName = "Format_Level", Namespace = "https://mediaarea.net/mediainfo")]
            public string FormatLevel { get; set; }

            [XmlElement(ElementName = "CodecID", Namespace = "https://mediaarea.net/mediainfo")]
            public string CodecId { get; set; }

            [XmlElement(ElementName = "Language", Namespace = "https://mediaarea.net/mediainfo")]
            public string Language { get; set; }

            [XmlElement(ElementName = "MuxingMode", Namespace = "https://mediaarea.net/mediainfo")]
            public string MuxingMode { get; set; }
            
            [XmlElement(ElementName = "ScanType", Namespace = "https://mediaarea.net/mediainfo")]
            public string ScanType { get; set; }
        }

        public static int MediaInfo(Config config, string parameters, out string output)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            string path = Tools.CombineToolPath(config, config.MediaInfo, MediaInfoBinary);
            ConsoleEx.WriteLineTool($"MediaInfo : {parameters}");
            return ProcessEx.Execute(path, parameters, out output);
        }

        public static string GetToolPath(Config config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            return Tools.CombineToolPath(config, config.MediaInfo);
        }

        public static bool GetLatestVersion(ToolInfo toolinfo)
        {
            if (toolinfo == null)
                throw new ArgumentNullException(nameof(toolinfo));

            try
            {
                // Load the download page
                // https://raw.githubusercontent.com/MediaArea/MediaInfo/master/History_CLI.txt
                // https://mediaarea.net/en/MediaInfo/Download/Windows
                using WebClient wc = new WebClient();
                string history = wc.DownloadString("https://raw.githubusercontent.com/MediaArea/MediaInfo/master/History_CLI.txt");

                // Read each line until we find the first version line
                // E.g. Version 17.10, 2017-11-02
                using StringReader sr = new StringReader(history);
                string line;
                while (true)
                {
                    // Read the line
                    line = sr.ReadLine();
                    if (line == null)
                        break;

                    // See if the line starts with "Version"
                    if (line.IndexOf("Version", StringComparison.Ordinal) == 0)
                        break;
                }
                if (string.IsNullOrEmpty(line)) throw new ArgumentException("Did not find version number field");

                // Extract the version number from the line
                // // E.g. Version 17.10, 2017-11-02
                // Version (.*?), 
                // TODO : Figure out how to use a named group
                string pattern = Regex.Escape(@"Version ") + @"(.*?)" + Regex.Escape(@",");
                Regex regex = new Regex(pattern);
                Match match = regex.Match(line);
                toolinfo.Version = match.Groups[1].Value;

                // Create download URL and the output filename using the version number
                // E.g. https://mediaarea.net/download/binary/mediainfo/17.10/MediaInfo_CLI_17.10_Windows_x64.zip
                toolinfo.FileName = $"MediaInfo_CLI_{toolinfo.Version}_Windows_x64.zip";
                toolinfo.Url = $"https://mediaarea.net/download/binary/mediainfo/{toolinfo.Version}/{toolinfo.FileName}";
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return true;
        }

        private const string MediaInfoBinary = @"mediainfo.exe";
    }
}
