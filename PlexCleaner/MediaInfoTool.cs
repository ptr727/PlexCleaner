using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using Serilog;

// http://manpages.ubuntu.com/manpages/zesty/man1/mediainfo.1.html

namespace PlexCleaner
{
    public class MediaInfoTool : MediaTool
    {
        public override ToolFamily GetToolFamily()
        {
            return ToolFamily.MediaInfo;
        }

        public override ToolType GetToolType()
        {
            return ToolType.MediaInfo;
        }

        protected override string GetToolNameWindows()
        {
            return "mediainfo.exe";
        }

        protected override string GetToolNameLinux()
        {
            return "mediainfo";
        }

        public override bool GetInstalledVersion(out MediaToolInfo mediaToolInfo)
        {
            // Initialize            
            mediaToolInfo = new MediaToolInfo(this);

            // Get version
            const string commandline = "--version";
            int exitcode = Command(commandline, false, false, out string output);
            if (exitcode != 0)
                return false;

            // Second line as version
            // E.g. Windows : "MediaInfoLib - v20.09"
            // E.g. Linux : "MediaInfoLib - v20.09"
            string[] lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            // Extract the short version number
            const string pattern = @"MediaInfoLib\ -\ v(?<version>.*)";
            Regex regex = new(pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
            Match match = regex.Match(lines[1]);
            Debug.Assert(match.Success);
            mediaToolInfo.Version = match.Groups["version"].Value;

            // Get tool filename
            mediaToolInfo.FileName = GetToolPath();

            // Get other attributes if we can read the file
            if (File.Exists(mediaToolInfo.FileName))
            {
                FileInfo fileInfo = new(mediaToolInfo.FileName);
                mediaToolInfo.ModifiedTime = fileInfo.LastWriteTimeUtc;
                mediaToolInfo.Size = fileInfo.Length;
            }

            return true;
        }

        public override bool GetLatestVersionWindows(out MediaToolInfo mediaToolInfo)
        {
            // Initialize            
            mediaToolInfo = new MediaToolInfo(this);

            try
            {
                // Load the release history page
                // https://raw.githubusercontent.com/MediaArea/MediaInfo/master/History_CLI.txt
                using HttpClient httpClient = new();
                string historyPage = httpClient.GetStringAsync("https://raw.githubusercontent.com/MediaArea/MediaInfo/master/History_CLI.txt").Result;

                // Read each line until we find the first version line
                // E.g. Version 17.10, 2017-11-02
                using StringReader sr = new(historyPage);
                string line;
                while (true)
                {
                    // Read the line
                    line = sr.ReadLine();
                    if (line == null)
                        break;

                    // See if the line starts with "Version"
                    line = line.Trim();
                    if (line.IndexOf("Version", StringComparison.Ordinal) == 0)
                        break;
                }
                if (string.IsNullOrEmpty(line))
                    throw new NotImplementedException();

                // Extract the version number from the line
                // E.g. Version 17.10, 2017-11-02
                const string pattern = @"Version\ (?<version>.*?),";
                Regex regex = new(pattern);
                Match match = regex.Match(line);
                Debug.Assert(match.Success);
                mediaToolInfo.Version = match.Groups["version"].Value;

                // Create download URL and the output filename using the version number
                // E.g. https://mediaarea.net/download/binary/mediainfo/17.10/MediaInfo_CLI_17.10_Windows_x64.zip
                mediaToolInfo.FileName = $"MediaInfo_CLI_{mediaToolInfo.Version}_Windows_x64.zip";
                mediaToolInfo.Url = $"https://mediaarea.net/download/binary/mediainfo/{mediaToolInfo.Version}/{mediaToolInfo.FileName}";
            }
            catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod().Name))
            {
                return false;
            }
            return true;
        }

        public override bool GetLatestVersionLinux(out MediaToolInfo mediaToolInfo)
        {
            // Initialize            
            mediaToolInfo = new MediaToolInfo(this);

            // TODO
            return false;
        }

        public bool GetMediaInfo(string filename, out MediaInfo mediaInfo)
        {
            mediaInfo = null;
            return GetMediaInfoXml(filename, out string xml) && 
                   GetMediaInfoFromXml(xml, out mediaInfo);
        }

        public bool GetMediaInfoXml(string filename, out string xml)
        {
            // Get media info as XML
            string commandline = $"--Output=XML \"{filename}\"";
            int exitcode = Command(commandline, false, false, out xml);

            // TODO : No error is returned when the file does not exist
            // https://sourceforge.net/p/mediainfo/bugs/1052/
            // Empty XML files are around 86 bytes
            // Match size check with ProcessSidecarFile()
            return exitcode == 0 && xml.Length >= 100;
        }

        public bool GetMediaInfoFromXml(string xml, out MediaInfo mediaInfo)
        {
            // Parser type is MediaInfo
            mediaInfo = new MediaInfo(ToolType.MediaInfo);

            // Populate the MediaInfo object from the XML string
            try
            {
                // Deserialize
                MediaInfoToolXmlSchema.MediaInfo xmlinfo = MediaInfoToolXmlSchema.MediaInfo.FromXml(xml);
                MediaInfoToolXmlSchema.MediaElement xmlmedia = xmlinfo.Media;

                // No tracks
                if (xmlmedia.Track.Count == 0)
                    return false;

                // Tracks
                foreach (MediaInfoToolXmlSchema.Track track in xmlmedia.Track)
                {
                    if (track.Type.Equals("Video", StringComparison.OrdinalIgnoreCase))
                    {
                        VideoInfo info = new(track);
                        mediaInfo.Video.Add(info);
                    }
                    else if (track.Type.Equals("Audio", StringComparison.OrdinalIgnoreCase))
                    {
                        AudioInfo info = new(track);
                        mediaInfo.Audio.Add(info);
                    }
                    else if (track.Type.Equals("Text", StringComparison.OrdinalIgnoreCase))
                    {
                        SubtitleInfo info = new(track);
                        mediaInfo.Subtitle.Add(info);
                    }
                }

                // Errors
                mediaInfo.HasErrors = mediaInfo.Video.Any(item => item.HasErrors) || 
                                      mediaInfo.Audio.Any(item => item.HasErrors) || 
                                      mediaInfo.Subtitle.Any(item => item.HasErrors);

                // TODO : Tags, maybe look in the Extra field, but not reliable
                // TODO : Duration, too many different formats to parse
                // https://github.com/MediaArea/MediaInfoLib/blob/master/Source/Resource/Text/Stream/General.csv#L92-L98
                // TODO : ContainerType
                // TODO : Chapters
                // TODO : Attachments
            }
            catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod().Name))
            {
                return false;
            }
            return true;
        }
    }
}
