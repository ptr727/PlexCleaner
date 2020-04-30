using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using InsaneGenius.Utilities;

namespace PlexCleaner
{
    public static class MediaInfoTool
    {
        // Tool version, read from Tools.json
        public static string Version { get; set; } = "";

        public static int MediaInfoCli(string parameters, out string output)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            parameters = parameters.Trim();

            string path = Tools.CombineToolPath(ToolsOptions.MediaInfo, MediaInfoBinary);
            ConsoleEx.WriteLineTool($"MediaInfo : {parameters}");
            return ProcessEx.Execute(path, parameters, out output);
        }

        public static string GetToolFolder()
        {
            return Tools.CombineToolPath(ToolsOptions.MediaInfo);
        }

        public static string GetToolPath()
        {
            return Tools.CombineToolPath(ToolsOptions.MediaInfo, MediaInfoBinary);
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
                if (string.IsNullOrEmpty(line))
                    throw new NotImplementedException();

                // Extract the version number from the line
                // E.g. Version 17.10, 2017-11-02
                const string pattern = @"Version\ (?<version>.*?),";
                Regex regex = new Regex(pattern);
                Match match = regex.Match(line);
                Debug.Assert(match.Success);
                toolinfo.Version = match.Groups["version"].Value;

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

        public static bool GetMediaInfo(string filename, out MediaInfo mediainfo)
        {
            mediainfo = null;
            return GetMediaInfoXml(filename, out string xml) && 
                   GetMediaInfoFromXml(xml, out mediainfo);
        }

        public static bool GetMediaInfoXml(string filename, out string xml)
        {
            // Create the MediaInfo commandline and execute
            // http://manpages.ubuntu.com/manpages/zesty/man1/mediainfo.1.html
            string commandline = $"--Output=XML \"{filename}\"";
            ConsoleEx.WriteLine("");
            int exitcode = MediaInfoCli(commandline, out xml);
            ConsoleEx.WriteLine("");

            // TODO : No error is returned when the file does not exist
            // https://sourceforge.net/p/mediainfo/bugs/1052/
            // Empty XML files are around 86 bytes
            // Match size check with ProcessSidecarFile()
            return exitcode == 0 && xml.Length >= 100;
        }

        public static bool GetMediaInfoFromXml(string xml, out MediaInfo mediainfo)
        {
            // Parser type is MediaInfo
            mediainfo = new MediaInfo(MediaInfo.ParserType.MediaInfo);

            // Populate the MediaInfo object from the XML string
            try
            {
                MediaInfoToolXmlSchema.MediaInfo xmlinfo = MediaInfoToolXmlSchema.MediaInfo.FromXml(xml);
                MediaInfoToolXmlSchema.Media xmlmedia = xmlinfo.Media;
                if (xmlmedia.Track.Count == 0)
                {
                    // No tracks
                    return false;
                }

                foreach (MediaInfoToolXmlSchema.Track track in xmlmedia.Track)
                {
                    if (track.Type.Equals("Video", StringComparison.OrdinalIgnoreCase))
                    {
                        VideoInfo info = new VideoInfo(track);
                        mediainfo.Video.Add(info);
                    }
                    else if (track.Type.Equals("Audio", StringComparison.OrdinalIgnoreCase))
                    {
                        AudioInfo info = new AudioInfo(track);
                        mediainfo.Audio.Add(info);
                    }
                    else if (track.Type.Equals("Text", StringComparison.OrdinalIgnoreCase))
                    {
                        SubtitleInfo info = new SubtitleInfo(track);
                        mediainfo.Subtitle.Add(info);
                    }
                }

                // Errors
                mediainfo.HasErrors = mediainfo.Video.Any(item => item.HasErrors) || mediainfo.Audio.Any(item => item.HasErrors) || mediainfo.Subtitle.Any(item => item.HasErrors);

                // Tags
                // TODO : Maybe look in the Extra field, but not reliable
                // Duration
                // TODO : Duration, too many different formats to parse
                // https://github.com/MediaArea/MediaInfoLib/blob/master/Source/Resource/Text/Stream/General.csv#L92-L98
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
