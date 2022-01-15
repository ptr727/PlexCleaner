using System;
using System.IO;
using System.IO.Compression;
using InsaneGenius.Utilities;
using System.Linq;
using System.Globalization;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Serilog;
using System.Reflection;
using System.Net.Http;

// https://mkvtoolnix.download/doc/mkvmerge.html

namespace PlexCleaner;

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

    public override bool GetInstalledVersion(out MediaToolInfo mediaToolInfo)
    {
        // Initialize            
        mediaToolInfo = new MediaToolInfo(this);

        // Get version
        const string commandline = "--version";
        int exitcode = Command(commandline, out string output);
        if (exitcode != 0)
            return false;

        // First line as version
        // E.g. Windows : "mkvmerge v51.0.0 ('I Wish') 64-bit"
        // E.g. Linux : "mkvmerge v51.0.0 ('I Wish') 64-bit"
        string[] lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        // Extract the short version number
        // Match word for mkvmerge or mkvpropedit
        const string pattern = @"([^\s]+)\ v(?<version>.*?)\ \(";
        Regex regex = new(pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
        Match match = regex.Match(lines[0]);
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
            // Download latest release file
            // https://mkvtoolnix.download/latest-release.xml.gz
            using HttpClient httpClient = new();
            Stream releaseStream = httpClient.GetStreamAsync("https://mkvtoolnix.download/latest-release.xml.gz").Result;

            // Get XML from Gzip
            using GZipStream gzstream = new(releaseStream, CompressionMode.Decompress);
            using StreamReader sr = new(gzstream);
            string xml = sr.ReadToEnd();

            // Get the version number from XML
            MkvToolXmlSchema.MkvToolnixReleases mkvtools = MkvToolXmlSchema.MkvToolnixReleases.FromXml(xml);
            mediaToolInfo.Version = mkvtools.LatestSource.Version;

            // Create download URL and the output filename using the version number
            // E.g. https://mkvtoolnix.download/windows/releases/18.0.0/mkvtoolnix-64-bit-18.0.0.7z
            mediaToolInfo.FileName = $"mkvtoolnix-64-bit-{mediaToolInfo.Version}.7z";
            mediaToolInfo.Url = $"https://mkvtoolnix.download/windows/releases/{mediaToolInfo.Version}/{mediaToolInfo.FileName}";
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
        mediaInfo = new MediaInfo(ToolType.MkvMerge);

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
                // If the container is not a MKV, ignore missing CodecId's
                if (!mkvmerge.Container.Type.Equals("Matroska", StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrEmpty(track.Properties.CodecId))
                    track.Properties.CodecId = "Unknown";

                if (track.Type.Equals("video", StringComparison.OrdinalIgnoreCase))
                {
                    VideoInfo info = new(track);
                    mediaInfo.Video.Add(info);
                }
                else if (track.Type.Equals("audio", StringComparison.OrdinalIgnoreCase))
                {
                    AudioInfo info = new(track);
                    mediaInfo.Audio.Add(info);
                }
                else if (track.Type.Equals("subtitles", StringComparison.OrdinalIgnoreCase))
                {
                    // TODO: Some variants of DVBSUB are not supported by MkvToolNix
                    // https://gitlab.com/mbunkus/mkvtoolnix/-/issues/1648
                    // https://github.com/ietf-wg-cellar/matroska-specification/pull/77/
                    // https://gitlab.com/mbunkus/mkvtoolnix/-/issues/3258

                    SubtitleInfo info = new(track);
                    mediaInfo.Subtitle.Add(info);
                }
            }

            // Remove cover art
            MediaInfo.RemoveCoverArt(mediaInfo);

            // Container type
            mediaInfo.Container = mkvmerge.Container.Type;

            // Track errors
            mediaInfo.HasErrors = mediaInfo.Video.Any(item => item.HasErrors) || 
                                  mediaInfo.Audio.Any(item => item.HasErrors) || 
                                  mediaInfo.Subtitle.Any(item => item.HasErrors);

            // Must be Matroska type
            if (!mkvmerge.Container.Type.Equals("Matroska", StringComparison.OrdinalIgnoreCase))
            { 
                mediaInfo.HasErrors = true;
                Log.Logger.Warning("MKV container type is not Matroska : {Type}", mkvmerge.Container.Type);
            }

            // Attachments
            mediaInfo.Attachments = mkvmerge.Attachments.Count;

            // Chapters
            mediaInfo.Chapters = mkvmerge.Chapters.Count;

            // Tags or title or track name or attachments
            // Only if track title is present but is not useful
            mediaInfo.HasTags = mkvmerge.GlobalTags.Count > 0 || 
                                mkvmerge.TrackTags.Count > 0 ||
                                !string.IsNullOrEmpty(mkvmerge.Container.Properties.Title) ||
                                mediaInfo.Video.Any(item => MediaInfo.IsTagTitle(item.Title)) ||
                                mediaInfo.Audio.Any(item => MediaInfo.IsTagTitle(item.Title)) ||
                                mediaInfo.Subtitle.Any(item => MediaInfo.IsTagTitle(item.Title)) ||
                                mediaInfo.Attachments > 0;

            // Duration (JSON uses nanoseconds)
            mediaInfo.Duration = TimeSpan.FromSeconds(mkvmerge.Container.Properties.Duration / 1000000.0);
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod().Name))
        {
            return false;
        }
        return true;
    }

    public static bool IsMkvFile(string filename)
    {
        return IsMkvExtension(Path.GetExtension(filename));
    }

    public static bool IsMkvFile(FileInfo fileInfo)
    {
        if (fileInfo == null)
            throw new ArgumentNullException(nameof(fileInfo));

        return IsMkvExtension(fileInfo.Extension);
    }

    public static bool IsMkvExtension(string extension)
    {
        if (extension == null)
            throw new ArgumentNullException(nameof(extension));

        // Case insensitive match, .mkv or .MKV
        return extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase);
    }

    public bool ReMuxToMkv(string inputname, MediaInfo keep, string outputname)
    {
        if (keep == null)
            return ReMuxToMkv(inputname, outputname);

        // Verify correct data type
        Debug.Assert(keep.Parser == ToolType.MkvMerge);

        // Delete output file
        FileEx.DeleteFile(outputname);

        // Create the track number filters
        // The track numbers are reported by MKVMerge --identify, use the track.id values
        string videotracks = keep.Video.Count > 0 ? $"--video-tracks {string.Join(",", keep.Video.Select(info => info.Id.ToString(CultureInfo.InvariantCulture)))} " : "--no-video ";
        string audiotracks = keep.Audio.Count > 0 ? $"--audio-tracks {string.Join(",", keep.Audio.Select(info => info.Id.ToString(CultureInfo.InvariantCulture)))} " : "--no-audio ";
        string subtitletracks = keep.Subtitle.Count > 0 ? $"--subtitle-tracks {string.Join(",", keep.Subtitle.Select(info => info.Id.ToString(CultureInfo.InvariantCulture)))} " : "--no-subtitles ";

        // Remux tracks
        string snippets = Program.Options.TestSnippets ? Snippet : "";
        string commandline = $"{MergeOptions} {snippets} --output \"{outputname}\" {videotracks}{audiotracks}{subtitletracks} \"{inputname}\"";
        int exitcode = Command(commandline);
        return exitcode is 0 or 1;
    }

    public bool ReMuxToMkv(string inputname, string outputname)
    {
        // Delete output file
        FileEx.DeleteFile(outputname);

        // Remux all
        string snippets = Program.Options.TestSnippets ? Snippet : "";
        string commandline = $"{MergeOptions} {snippets} --output \"{outputname}\" \"{inputname}\"";
        int exitcode = Command(commandline);
        return exitcode is 0 or 1;
    }

    private const string Snippet = "--split parts:00:00:00-00:03:00";
    private const string MergeOptions = "--disable-track-statistics-tags --no-global-tags --no-track-tags --flush-on-close";
}