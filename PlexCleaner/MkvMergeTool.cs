using System;
using System.IO;
using System.Net;
using System.IO.Compression;
using InsaneGenius.Utilities;
using System.Linq;
using System.Globalization;
using System.Diagnostics;
using System.Runtime.InteropServices;

// https://mkvtoolnix.download/doc/mkvmerge.html

namespace PlexCleaner
{
    public class MkvMergeTool : MediaTool
    {
        public override ToolFamily GetToolFamily()
        {
            return ToolFamily.MkvToolNix;
        }

        public override ToolType GetToolType()
        {
            return ToolType.MkvMerge;
        }

        protected override string GetToolNameWindows()
        {
            return "mkvmerge.exe";
        }

        protected override string GetToolNameLinux()
        {
            return "mkvmerge";
        }

        public override bool GetInstalledVersion(out ToolInfo toolInfo)
        {
            // Initialize            
            toolInfo = new ToolInfo
            {
                Tool = GetToolType().ToString()
            };

            // Get version
            string commandline = $"--version";
            int exitcode = Command(commandline, out string output);
            if (exitcode != 0)
                return false;

            // First line as version
            string[] lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            toolInfo.Version = lines[0];

            // Get tool filename
            toolInfo.FileName = GetToolPath();

            return true;
        }

        public override bool GetLatestVersion(out ToolInfo toolInfo)
        {
            // Initialize            
            toolInfo = new ToolInfo
            {
                Tool = GetToolType().ToString()
            };

            // Windows or Linux
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return GetLatestVersionWindows(toolInfo);
            return GetLatestVersionLinux(toolInfo);
        }

        protected bool GetLatestVersionWindows(ToolInfo toolInfo)
        {
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
                toolInfo.Version = mkvtools.LatestSource.Version;

                // Create download URL and the output filename using the version number
                // E.g. https://mkvtoolnix.download/windows/releases/18.0.0/mkvtoolnix-64-bit-18.0.0.7z
                toolInfo.FileName = $"mkvtoolnix-64-bit-{toolInfo.Version}.7z";
                toolInfo.Url = $"https://mkvtoolnix.download/windows/releases/{toolInfo.Version}/{toolInfo.FileName}";
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return true;
        }

        protected bool GetLatestVersionLinux(ToolInfo toolInfo)
        {
            // TODO
            return false;
        }

        public bool GetMkvInfo(string filename, out MediaInfo mediaInfo)
        {
            mediaInfo = null;
            return GetMkvInfoJson(filename, out string json) && 
                   GetMkvInfoFromJson(json, out mediaInfo);
        }

        public bool GetMkvInfoJson(string filename, out string json)
        {
            // Get media info as JSON
            string commandline = $"--identify \"{filename}\" --identification-format json";
            int exitcode = Command(commandline, out json);
            return exitcode == 0;
        }

        public bool GetMkvInfoFromJson(string json, out MediaInfo mediaInfo)
        {
            // Parser type is MkvMerge
            mediaInfo = new MediaInfo(MediaTool.ToolType.MkvMerge);

            // Populate the MediaInfo object from the JSON string
            try
            {
                // Deserialize
                MkvToolJsonSchema.MkvMerge mkvmerge = MkvToolJsonSchema.MkvMerge.FromJson(json);

                // No tracks
                if (mkvmerge.Tracks.Count == 0)
                    return false;

                // Tracks
                foreach (MkvToolJsonSchema.Track track in mkvmerge.Tracks)
                {
                    if (track.Type.Equals("video", StringComparison.OrdinalIgnoreCase))
                    {
                        VideoInfo info = new VideoInfo(track);
                        mediaInfo.Video.Add(info);
                    }
                    else if (track.Type.Equals("audio", StringComparison.OrdinalIgnoreCase))
                    {
                        AudioInfo info = new AudioInfo(track);
                        mediaInfo.Audio.Add(info);
                    }
                    else if (track.Type.Equals("subtitles", StringComparison.OrdinalIgnoreCase))
                    {
                        SubtitleInfo info = new SubtitleInfo(track);
                        mediaInfo.Subtitle.Add(info);
                    }
                }

                // Container type
                mediaInfo.Container = mkvmerge.Container.Type;

                // Track errors
                mediaInfo.HasErrors = mediaInfo.Video.Any(item => item.HasErrors) || 
                                      mediaInfo.Audio.Any(item => item.HasErrors) || 
                                      mediaInfo.Subtitle.Any(item => item.HasErrors);

                // Must be Matroska type
                if (!mkvmerge.Container.Type.Equals("Matroska", StringComparison.OrdinalIgnoreCase))
                    mediaInfo.HasErrors = true;

                // Tags or Title
                mediaInfo.HasTags = mkvmerge.GlobalTags.Count > 0 || 
                                    mkvmerge.TrackTags.Count > 0 ||
                                    !string.IsNullOrEmpty(mkvmerge.Container.Properties.Title);

                // Duration (JSON uses nanoseconds)
                mediaInfo.Duration = TimeSpan.FromSeconds(mkvmerge.Container.Properties.Duration / 1000000.0);
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return true;
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

        public bool ReMuxToMkv(string inputname, MediaInfo keep, string outputname)
        {
            if (keep == null)
                return ReMuxToMkv(inputname, outputname);

            // Verify correct data type
            Debug.Assert(keep.Parser == MediaTool.ToolType.MkvMerge);

            // Delete output file
            FileEx.DeleteFile(outputname);

            // Create the track number filters
            // The track numbers are reported by MKVMerge --identify, use the track.id values
            string videotracks = keep.Video.Count > 0 ? $"--video-tracks {string.Join(",", keep.Video.Select(info => info.Id.ToString(CultureInfo.InvariantCulture)))} " : "--no-video ";
            string audiotracks = keep.Audio.Count > 0 ? $"--audio-tracks {string.Join(",", keep.Audio.Select(info => info.Id.ToString(CultureInfo.InvariantCulture)))} " : "--no-audio ";
            string subtitletracks = keep.Subtitle.Count > 0 ? $"--subtitle-tracks {string.Join(",", keep.Subtitle.Select(info => info.Id.ToString(CultureInfo.InvariantCulture)))} " : "--no-subtitles ";

            // Remux tracks
            string snippets = Program.Options.TestSnippets ? MkvmergeSnippet : "";
            string commandline = $"{snippets} --output \"{outputname}\" {videotracks}{audiotracks}{subtitletracks} \"{inputname}\"";
            int exitcode = Command(commandline);
            return exitcode == 0 || exitcode == 1;
        }

        public bool ReMuxToMkv(string inputname, string outputname)
        {
            // Delete output file
            FileEx.DeleteFile(outputname);

            // Remux all
            string snippets = Program.Options.TestSnippets ? MkvmergeSnippet : "";
            string commandline = $"{snippets} --output \"{outputname}\" \"{inputname}\"";
            int exitcode = Command(commandline);
            return exitcode == 0 || exitcode == 1;
        }

        private const string MkvmergeSnippet = "--split parts:00:00:00-00:01:00";
    }
}