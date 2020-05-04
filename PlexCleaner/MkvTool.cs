using System;
using System.IO;
using System.Net;
using System.IO.Compression;
using InsaneGenius.Utilities;
using System.Linq;
using System.Globalization;
using System.Diagnostics;

namespace PlexCleaner
{
    public static class MkvTool
    {
        // Tool version, read from Tools.json
        public static string Version { get; set; } = "";

        public static int MkvMergeCli(string parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            parameters = parameters.Trim();

            string path = Tools.CombineToolPath(ToolsOptions.MkvToolNix, MkvMergeBinary);
            ConsoleEx.WriteLineTool($"MKVMerge : {parameters}");
            return ProcessEx.Execute(path, parameters);
        }

        public static int MkvMergeCli(string parameters, out string output)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            parameters = parameters.Trim();

            string path = Tools.CombineToolPath(ToolsOptions.MkvToolNix, MkvMergeBinary);
            ConsoleEx.WriteLineTool($"MKVMerge : {parameters}");
            return ProcessEx.Execute(path, parameters, out output);
        }

        public static int MkvPropEditCli(string parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            parameters = parameters.Trim();

            string path = Tools.CombineToolPath(ToolsOptions.MkvToolNix, MkvPropEditBinary);
            ConsoleEx.WriteLineTool($"MKVPropEdit : {parameters}");
            return ProcessEx.Execute(path, parameters);
        }

        public static string GetToolFolder()
        {
            return Tools.CombineToolPath(ToolsOptions.MkvToolNix);
        }

        public static bool GetLatestVersion(ToolInfo toolinfo)
        {
            if (toolinfo == null)
                throw new ArgumentNullException(nameof(toolinfo));

            toolinfo.Tool = nameof(MkvTool);

            try
            {
                // Download latest release file
                // https://mkvtoolnix.download/latest-release.xml.gz
                using WebClient wc = new WebClient();
                Stream wcstream = wc.OpenRead("https://mkvtoolnix.download/latest-release.xml.gz");

                // Get XML from Gzip
                using GZipStream gzstream = new GZipStream(wcstream, CompressionMode.Decompress);
                using StreamReader sr = new StreamReader(gzstream);
                string xml = sr.ReadToEnd();

                // Get the version number from XML
                MkvToolXmlSchema.MkvToolnixReleases mkvtools = MkvToolXmlSchema.MkvToolnixReleases.FromXml(xml);
                toolinfo.Version = mkvtools.LatestSource.Version;

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

        public static bool GetMkvInfo(string filename, out MediaInfo mediainfo)
        {
            mediainfo = null;
            return GetMkvInfoJson(filename, out string json) && 
                   GetMkvInfoFromJson(json, out mediainfo);
        }

        public static bool GetMkvInfoJson(string filename, out string json)
        {
            // Create the MKVMerge commandline and execute
            // Note the correct usage of "id" or "number" in the MKV tools
            // https://mkvtoolnix.download/doc/mkvmerge.html#mkvmerge.description.identify
            // https://mkvtoolnix.download/doc/mkvpropedit.html
            // https://mkvtoolnix.download/doc/mkvmerge.html
            string commandline = $"--identify \"{filename}\" --identification-format json";
            ConsoleEx.WriteLine("");
            int exitcode = MkvMergeCli(commandline, out json);
            ConsoleEx.WriteLine("");
            return exitcode == 0;
        }

        public static bool GetMkvInfoFromJson(string json, out MediaInfo mediainfo)
        {
            // Parser type is MkvMerge
            mediainfo = new MediaInfo(MediaInfo.ParserType.MkvMerge);

            // Populate the MediaInfo object from the JSON string
            try
            {
                MkvToolJsonSchema.MkvMerge mkvmerge = MkvToolJsonSchema.MkvMerge.FromJson(json);
                if (mkvmerge.Tracks.Count == 0)
                {
                    // No tracks
                    return false;
                }

                // Tracks
                foreach (MkvToolJsonSchema.Track track in mkvmerge.Tracks)
                {
                    if (track.Type.Equals("video", StringComparison.OrdinalIgnoreCase))
                    {
                        VideoInfo info = new VideoInfo(track);
                        mediainfo.Video.Add(info);
                    }
                    else if (track.Type.Equals("audio", StringComparison.OrdinalIgnoreCase))
                    {
                        AudioInfo info = new AudioInfo(track);
                        mediainfo.Audio.Add(info);
                    }
                    else if (track.Type.Equals("subtitles", StringComparison.OrdinalIgnoreCase))
                    {
                        SubtitleInfo info = new SubtitleInfo(track);
                        mediainfo.Subtitle.Add(info);
                    }
                }

                // Errors
                mediainfo.HasErrors = mediainfo.Video.Any(item => item.HasErrors) || mediainfo.Audio.Any(item => item.HasErrors) || mediainfo.Subtitle.Any(item => item.HasErrors);

                // Tags
                mediainfo.HasTags = mkvmerge.GlobalTags.Count > 0 || mkvmerge.TrackTags.Count > 0;

                // Duration (JSON uses nanoseconds)
                mediainfo.Duration = TimeSpan.FromSeconds(mkvmerge.Container.Properties.Duration / 1000000.0);
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLineError(e);
                return false;
            }

            return true;
        }

        public static bool SetMkvTrackLanguage(string filename, MediaInfo unknown, string language)
        {
            if (unknown == null)
                throw new ArgumentNullException(nameof(unknown));

            // Verify correct data type
            Debug.Assert(unknown.Parser == MediaInfo.ParserType.MkvMerge);

            // Mark all tracks
            return unknown.GetTrackList().All(track => SetMkvTrackLanguage(filename, track.Number, language));
        }

        public static bool SetMkvTrackLanguage(string filename, int track, string language)
        {
            // Create the MKVPropEdit commandline and execute
            // The track number is reported by MKVMerge --identify using the track.properties.number value
            // https://mkvtoolnix.download/doc/mkvpropedit.html
            string commandline = $"\"{filename}\" --edit track:@{track} --set language={language}";
            ConsoleEx.WriteLine("");
            int exitcode = MkvPropEditCli(commandline);
            ConsoleEx.WriteLine("");
            return exitcode == 0;
        }

        public static bool ClearMkvTags(string filename)
        {
            // Create the MKVPropEdit commandline and execute
            // https://mkvtoolnix.download/doc/mkvpropedit.html
            string commandline = $"\"{filename}\" --tags all:";
            ConsoleEx.WriteLine("");
            int exitcode = MkvPropEditCli(commandline);
            ConsoleEx.WriteLine("");
            return exitcode == 0;
        }

        public static bool IsMkvFile(string filename)
        {
            return IsMkvExtension(Path.GetExtension(filename));
        }

        public static bool IsMkvFile(FileInfo fileinfo)
        {
            if (fileinfo == null)
                throw new ArgumentNullException(nameof(fileinfo));

            return IsMkvExtension(fileinfo.Extension);
        }

        public static bool IsMkvExtension(string extension)
        {
            if (extension == null)
                throw new ArgumentNullException(nameof(extension));

            return extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase);
        }

        public static bool ReMuxToMkv(string inputname, MediaInfo keep, string outputname)
        {
            if (keep == null)
                return ReMuxToMkv(inputname, outputname);

            // Verify correct data type
            Debug.Assert(keep.Parser == MediaInfo.ParserType.MkvMerge);

            // Delete output file
            FileEx.DeleteFile(outputname);

            // Create the track number filters
            // The track numbers are reported by MKVMerge --identify, use the track.id values
            string videotracks = keep.Video.Count > 0 ? $"--video-tracks {string.Join(",", keep.Video.Select(info => info.Id.ToString(CultureInfo.InvariantCulture)))} " : "--no-video ";
            string audiotracks = keep.Audio.Count > 0 ? $"--audio-tracks {string.Join(",", keep.Audio.Select(info => info.Id.ToString(CultureInfo.InvariantCulture)))} " : "--no-audio ";
            string subtitletracks = keep.Subtitle.Count > 0 ? $"--subtitle-tracks {string.Join(",", keep.Subtitle.Select(info => info.Id.ToString(CultureInfo.InvariantCulture)))} " : "--no-subtitles ";

            // Create the MKVMerge commandline and execute
            // https://mkvtoolnix.download/doc/mkvmerge.html
            string snippets = Program.Options.TestSnippets ? MkvmergeSnippet : "";
            string commandline = $"{snippets} --output \"{outputname}\" {videotracks}{audiotracks}{subtitletracks} \"{inputname}\"";
            ConsoleEx.WriteLine("");
            int exitcode = MkvMergeCli(commandline);
            ConsoleEx.WriteLine("");
            return exitcode == 0 || exitcode == 1;
        }

        public static bool ReMuxToMkv(string inputname, string outputname)
        {
            // Delete output file
            FileEx.DeleteFile(outputname);

            // Create the MKVMerge commandline and execute
            // https://mkvtoolnix.download/doc/mkvmerge.html
            string snippets = Program.Options.TestSnippets ? MkvmergeSnippet : "";
            string commandline = $"{snippets} --output \"{outputname}\" \"{inputname}\"";
            ConsoleEx.WriteLine("");
            int exitcode = MkvMergeCli(commandline);
            ConsoleEx.WriteLine("");
            return exitcode == 0 || exitcode == 1;
        }

        private const string MkvMergeBinary = @"mkvmerge.exe";
        private const string MkvPropEditBinary = @"mkvpropedit.exe";
        private const string MkvmergeSnippet = "--split parts:00:00:00-00:01:00";
    }
}