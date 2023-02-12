using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using InsaneGenius.Utilities;
using Serilog;

// https://mkvtoolnix.download/doc/mkvmerge.html

namespace PlexCleaner;

public partial class MkvMergeTool : MediaTool
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
        var exitCode = Command(commandline, out var output);
        if (exitCode != 0)
        {
            return false;
        }

        // First line as version
        // E.g. Windows : "mkvmerge v51.0.0 ('I Wish') 64-bit"
        // E.g. Linux : "mkvmerge v51.0.0 ('I Wish') 64-bit"
        var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        // Extract the short version number
        // Match word for mkvmerge or mkvpropedit
        var match = InstalledVersionRegex().Match(lines[0]);
        Debug.Assert(match.Success);
        mediaToolInfo.Version = match.Groups["version"].Value;

        // Get tool fileName
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

    protected override bool GetLatestVersionWindows(out MediaToolInfo mediaToolInfo)
    {
        // Initialize            
        mediaToolInfo = new MediaToolInfo(this);

        try
        {
            // Download latest release file
            // https://mkvtoolnix.download/latest-release.xml.gz
            using HttpClient httpClient = new();
            var releaseStream = httpClient.GetStreamAsync("https://mkvtoolnix.download/latest-release.xml.gz").Result;

            // Get XML from Gzip
            using GZipStream gzstream = new(releaseStream, CompressionMode.Decompress);
            using StreamReader sr = new(gzstream);
            var xml = sr.ReadToEnd();

            // Get the version number from XML
            var mkvtools = MkvToolXmlSchema.MkvToolnixReleases.FromXml(xml);
            mediaToolInfo.Version = mkvtools.LatestSource.Version;

            // Create download URL and the output fileName using the version number
            // E.g. https://mkvtoolnix.download/windows/releases/18.0.0/mkvtoolnix-64-bit-18.0.0.7z
            mediaToolInfo.FileName = $"mkvtoolnix-64-bit-{mediaToolInfo.Version}.7z";
            mediaToolInfo.Url = $"https://mkvtoolnix.download/windows/releases/{mediaToolInfo.Version}/{mediaToolInfo.FileName}";
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            return false;
        }
        return true;
    }

    protected override bool GetLatestVersionLinux(out MediaToolInfo mediaToolInfo)
    {
        // Initialize            
        mediaToolInfo = new MediaToolInfo(this);

        // TODO
        return false;
    }

    public bool GetMkvInfo(string fileName, out MediaInfo mediaInfo)
    {
        mediaInfo = null;
        return GetMkvInfoJson(fileName, out var json) &&
               GetMkvInfoFromJson(json, out mediaInfo);
    }

    public bool GetMkvInfoJson(string fileName, out string json)
    {
        // Get media info as JSON
        var commandline = $"--identify \"{fileName}\" --identification-format json";
        var exitCode = Command(commandline, out json);
        return exitCode == 0;
    }

    public static bool GetMkvInfoFromJson(string json, out MediaInfo mediaInfo)
    {
        // Parser type is MkvMerge
        mediaInfo = new MediaInfo(ToolType.MkvMerge);

        // Populate the MediaInfo object from the JSON string
        try
        {
            // Deserialize
            var mkvMerge = MkvToolJsonSchema.MkvMerge.FromJson(json);
            if (mkvMerge == null)
            {
                return false;
            }

            // No tracks
            if (mkvMerge.Tracks.Count == 0)
            {
                return false;
            }

            // Tracks
            foreach (var track in mkvMerge.Tracks)
            {
                // If the container is not a MKV, ignore missing CodecId's
                if (!mkvMerge.Container.Type.Equals("Matroska", StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrEmpty(track.Properties.CodecId))
                {
                    track.Properties.CodecId = "Unknown";
                }

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
            mediaInfo.Container = mkvMerge.Container.Type;

            // Attachments
            mediaInfo.Attachments = mkvMerge.Attachments.Count;

            // Chapters
            mediaInfo.Chapters = mkvMerge.Chapters.Count;

            // Track errors
            mediaInfo.HasErrors = mediaInfo.Video.Any(item => item.HasErrors) ||
                                  mediaInfo.Audio.Any(item => item.HasErrors) ||
                                  mediaInfo.Subtitle.Any(item => item.HasErrors);

            // Tags in container or any tracks
            mediaInfo.HasTags = mkvMerge.GlobalTags.Count > 0 ||
                                mkvMerge.TrackTags.Count > 0 ||
                                mediaInfo.Attachments > 0 ||
                                !string.IsNullOrEmpty(mkvMerge.Container.Properties.Title) ||
                                mediaInfo.Video.Any(item => item.HasTags) ||
                                mediaInfo.Audio.Any(item => item.HasTags) ||
                                mediaInfo.Subtitle.Any(item => item.HasTags);

            // Duration in nanoseconds
            mediaInfo.Duration = TimeSpan.FromSeconds(mkvMerge.Container.Properties.Duration / 1000000.0);

            // Must be Matroska type
            if (!mkvMerge.Container.Type.Equals("Matroska", StringComparison.OrdinalIgnoreCase))
            {
                mediaInfo.HasErrors = true;
                Log.Logger.Warning("MKV container type is not Matroska : {Type}", mkvMerge.Container.Type);
            }
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod().Name))
        {
            return false;
        }
        return true;
    }

    public static bool IsMkvFile(string fileName)
    {
        return IsMkvExtension(Path.GetExtension(fileName));
    }

    public static bool IsMkvFile(FileInfo fileInfo)
    {
        if (fileInfo == null)
        {
            throw new ArgumentNullException(nameof(fileInfo));
        }

        return IsMkvExtension(fileInfo.Extension);
    }

    public static bool IsMkvExtension(string extension)
    {
        if (extension == null)
        {
            throw new ArgumentNullException(nameof(extension));
        }

        // Case insensitive match, .mkv or .MKV
        return extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase);
    }

    public bool ReMuxToMkv(string inputName, MediaInfo keep, string outputName)
    {
        if (keep == null)
        {
            return ReMuxToMkv(inputName, outputName);
        }

        // Verify correct data type
        Debug.Assert(keep.Parser == ToolType.MkvMerge);

        // Delete output file
        FileEx.DeleteFile(outputName);

        // Build commandline
        StringBuilder commandline = new();
        DefaultArgs(outputName, commandline);
        TracksArgs(keep, commandline);
        commandline.Append($"\"{inputName}\"");

        // Remux tracks
        var exitCode = Command(commandline.ToString());
        return exitCode is 0 or 1;
    }

    public bool ReMuxToMkv(string inputName, string outputName)
    {
        // Delete output file
        FileEx.DeleteFile(outputName);

        // Build commandline
        StringBuilder commandline = new();
        DefaultArgs(outputName, commandline);
        commandline.Append($"\"{inputName}\"");

        // Remux all
        var exitCode = Command(commandline.ToString());
        return exitCode is 0 or 1;
    }

    public bool MergeToMkv(string sourceOne, MediaInfo keepOne, string sourceTwo, string outputName)
    {
        // Selectively merge tracks from sourceOne with all tracks in sourceTwo

        // Verify correct data type
        Debug.Assert(keepOne.Parser == ToolType.MkvMerge);

        // Delete output file
        FileEx.DeleteFile(outputName);

        // Build commandline
        StringBuilder commandline = new();
        DefaultArgs(outputName, commandline);
        TracksArgs(keepOne, commandline);
        commandline.Append($"--no-chapters \"{sourceOne}\" \"{sourceTwo}\"");

        // Remux tracks
        var exitCode = Command(commandline.ToString());
        return exitCode is 0 or 1;
    }

    private static void DefaultArgs(string outputName, StringBuilder commandline)
    {
        commandline.Append($"{MergeOptions} ");
        if (Program.Options.Parallel)
        {
            // Suppress console output
            commandline.Append("--quiet ");
        }
        if (Program.Options.TestSnippets)
        {
            commandline.Append($"{Snippet} ");
        }
        commandline.Append($"--output \"{outputName}\" ");
    }

    private static void TracksArgs(MediaInfo mediaInfo, StringBuilder commandline)
    {
        // Create the track number filters
        // The track numbers are reported by MkvMerge --identify, use the track.id values
        if (mediaInfo.Video.Count > 0)
        {
            commandline.Append($"--video-tracks {string.Join(",", mediaInfo.Video.Select(info => info.Id.ToString(CultureInfo.InvariantCulture)))} ");
        }
        else
        {
            commandline.Append("--no-video ");
        }
        if (mediaInfo.Audio.Count > 0)
        {
            commandline.Append($"--audio-tracks {string.Join(",", mediaInfo.Audio.Select(info => info.Id.ToString(CultureInfo.InvariantCulture)))} ");
        }
        else
        {
            commandline.Append("--no-audio ");
        }
        if (mediaInfo.Subtitle.Count > 0)
        {
            commandline.Append($"--subtitle-tracks {string.Join(",", mediaInfo.Subtitle.Select(info => info.Id.ToString(CultureInfo.InvariantCulture)))} ");
        }
        else
        {
            commandline.Append("--no-subtitles ");
        }
    }

    private const string Snippet = "--split parts:00:00:00-00:03:00";
    private const string MergeOptions = "--disable-track-statistics-tags --no-global-tags --no-track-tags --no-attachments --no-buttons --flush-on-close";

    const string InstalledVersionPattern = @"([^\s]+)\ v(?<version>.*?)\ \(";

    [GeneratedRegex(InstalledVersionPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex InstalledVersionRegex();
}
