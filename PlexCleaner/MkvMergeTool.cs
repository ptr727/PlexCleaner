using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using InsaneGenius.Utilities;
using Serilog;

// https://mkvtoolnix.download/doc/mkvmerge.html
// mkvmerge [global options] {-o out} [options1] {file1} [[options2] {file2}] [@options-file.json]

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
            const string uri = "https://mkvtoolnix.download/latest-release.xml.gz";
            Log.Logger.Information("{Tool} : Reading latest version from : {Uri}", GetToolFamily(), uri);
            var releaseStream = Download.GetHttpClient().GetStreamAsync(uri).Result;

            // Get XML from Gzip
            using GZipStream gzStream = new(releaseStream, CompressionMode.Decompress);
            using StreamReader sr = new(gzStream);
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

        // TODO: Linux implementation
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
        StringBuilder commandline = new();
        // Normalize IETF tags to extended format, e.g. zh-cmn-Hant vs. cmn-Hant
        commandline.Append($"--normalize-language-ietf extlang --identify \"{fileName}\" --identification-format json");
        var exitCode = Command(commandline.ToString(), out json);
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

            // Container type
            mediaInfo.Container = mkvMerge.Container.Type;

            // Attachments
            mediaInfo.Attachments = mkvMerge.Attachments.Count;

            // Chapters
            mediaInfo.Chapters = mkvMerge.Chapters.Count;

            // Errors, any unsupported tracks
            mediaInfo.HasErrors = mediaInfo.Unsupported;

            // Unwanted tags
            mediaInfo.HasTags = mkvMerge.GlobalTags.Count > 0 ||
                                mkvMerge.TrackTags.Count > 0 ||
                                mediaInfo.Attachments > 0 ||
                                !string.IsNullOrEmpty(mkvMerge.Container.Properties.Title);

            // Duration in nanoseconds
            mediaInfo.Duration = TimeSpan.FromSeconds(mkvMerge.Container.Properties.Duration / 1000000.0);

            // Must be Matroska type
            if (!IsMkvContainer(mediaInfo))
            {
                Log.Logger.Warning("MKV container type is not Matroska : {Type}", mkvMerge.Container.Type);

                // Remux to convert to MKV
                mediaInfo.HasErrors = true;
                mediaInfo.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.ReMux);
            }
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
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
        return IsMkvExtension(fileInfo.Extension);
    }

    public static bool IsMkvExtension(string extension)
    {
        // Case insensitive match, .mkv or .MKV
        return extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsMkvContainer(MediaInfo mediaInfo)
    {
        return mediaInfo.Container.Equals("Matroska", StringComparison.OrdinalIgnoreCase);
    }

    public bool ReMuxToMkv(string inputName, SelectMediaInfo selectMediaInfo, string outputName)
    {
        // Verify correct media type
        Debug.Assert(selectMediaInfo.Selected.Parser == ToolType.MkvMerge);

        // Delete output file
        FileEx.DeleteFile(outputName);

        // Build commandline
        // Selected is Keep
        // NotSelected is Remove
        StringBuilder commandline = new();
        CreateDefaultArgs(outputName, commandline);
        CreateTrackArgs(selectMediaInfo.Selected, commandline);
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
        CreateDefaultArgs(outputName, commandline);
        commandline.Append($"\"{inputName}\"");

        // Remux all
        var exitCode = Command(commandline.ToString());
        return exitCode is 0 or 1;
    }

    public bool MergeToMkv(string sourceOne, string sourceTwo, MediaInfo keepTwo, string outputName)
    {
        // Merge all tracks from sourceOne with selected tracks in sourceTwo

        // Verify correct parser type
        Debug.Assert(keepTwo.Parser == ToolType.MkvMerge);

        // Delete output file
        FileEx.DeleteFile(outputName);

        // Build commandline
        StringBuilder commandline = new();
        // Default args
        CreateDefaultArgs(outputName, commandline);
        // Source one as is
        commandline.Append($"\"{sourceOne}\" ");
        // Source two track options
        CreateTrackArgs(keepTwo, commandline);
        // Source two
        // TODO: Why did I use --no-chapters
        commandline.Append($"\"{sourceTwo}\"");
        // Remux tracks
        var exitCode = Command(commandline.ToString());
        return exitCode is 0 or 1;
    }

    private static void CreateDefaultArgs(string outputName, StringBuilder commandline)
    {
        commandline.Append($"{MergeOptions} ");
        if (Program.Options.Parallel)
        {
            // Suppress console output
            commandline.Append("--quiet ");
        }
        if (Program.Options.TestSnippets)
        {
            commandline.Append($"--split parts:00:00:00-{Program.SnippetTimeSpan:hh\\:mm\\:ss} ");
        }
        commandline.Append($"--output \"{outputName}\" ");
    }

    private static void CreateTrackArgs(MediaInfo mediaInfo, StringBuilder commandline)
    {
        // Verify correct media type
        Debug.Assert(mediaInfo.Parser == ToolType.MkvMerge);

        // Create the track number filters
        // The track numbers are reported by MkvMerge --identify, use the track.id values
        if (mediaInfo.Video.Count > 0)
        {
            commandline.Append($"--video-tracks {string.Join(",", mediaInfo.Video.Select(info => $"{info.Id}"))} ");
        }
        else
        {
            commandline.Append("--no-video ");
        }
        if (mediaInfo.Audio.Count > 0)
        {
            commandline.Append($"--audio-tracks {string.Join(",", mediaInfo.Audio.Select(info => $"{info.Id}"))} ");
        }
        else
        {
            commandline.Append("--no-audio ");
        }
        if (mediaInfo.Subtitle.Count > 0)
        {
            commandline.Append($"--subtitle-tracks {string.Join(",", mediaInfo.Subtitle.Select(info => $"{info.Id}"))} ");
        }
        else
        {
            commandline.Append("--no-subtitles ");
        }
    }

    private const string MergeOptions = "--disable-track-statistics-tags --no-global-tags --no-track-tags --no-attachments --no-buttons --normalize-language-ietf extlang";

    private const string InstalledVersionPattern = @"([^\s]+)\ v(?<version>.*?)\ \(";
    [GeneratedRegex(InstalledVersionPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    public static partial Regex InstalledVersionRegex();
}
