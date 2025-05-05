using System;
using System.Diagnostics;
using System.Globalization;
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

// TODO: What is an equivalent to ffmpeg -nostats to suppress progress output?
// https://help.mkvtoolnix.download/t/option-to-suppress-progress-reporting-but-keep-static-output/1320

namespace PlexCleaner;

public partial class MkvMergeTool : MediaTool
{
    public override ToolFamily GetToolFamily() => ToolFamily.MkvToolNix;

    public override ToolType GetToolType() => ToolType.MkvMerge;

    protected override string GetToolNameWindows() => "mkvmerge.exe";

    protected override string GetToolNameLinux() => "mkvmerge";

    public override bool GetInstalledVersion(out MediaToolInfo mediaToolInfo)
    {
        // Initialize
        mediaToolInfo = new MediaToolInfo(this);

        // Get version
        const string commandline = "--version";
        int exitCode = Command(commandline, out string output);
        if (exitCode != 0)
        {
            return false;
        }

        // First line of stdout as version
        // E.g. Windows : "mkvmerge v51.0.0 ('I Wish') 64-bit"
        // E.g. Linux : "mkvmerge v51.0.0 ('I Wish') 64-bit"
        string[] lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        // Extract the short version number
        // Match word for mkvmerge or mkvpropedit
        Match match = InstalledVersionRegex().Match(lines[0]);
        Debug.Assert(match.Success);
        mediaToolInfo.Version = match.Groups["version"].Value;
        Debug.Assert(Version.TryParse(mediaToolInfo.Version, out _));

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
            Log.Information("{Tool} : Reading latest version from : {Uri}", GetToolFamily(), uri);
            Stream releaseStream = Download.GetHttpClient().GetStreamAsync(uri).Result;

            // Get XML from Gzip
            using GZipStream gzStream = new(releaseStream, CompressionMode.Decompress);
            using StreamReader sr = new(gzStream);
            string xml = sr.ReadToEnd();

            // Get the version number from XML
            MkvToolXmlSchema.MkvToolnixReleases mkvtools =
                MkvToolXmlSchema.MkvToolnixReleases.FromXml(xml);
            mediaToolInfo.Version = mkvtools.LatestSource.Version;

            // Create download URL and the output fileName using the version number
            // E.g. https://mkvtoolnix.download/windows/releases/18.0.0/mkvtoolnix-64-bit-18.0.0.7z
            mediaToolInfo.FileName = $"mkvtoolnix-64-bit-{mediaToolInfo.Version}.7z";
            mediaToolInfo.Url =
                $"https://mkvtoolnix.download/windows/releases/{mediaToolInfo.Version}/{mediaToolInfo.FileName}";
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            return false;
        }
        return true;
    }

    protected override bool GetLatestVersionLinux(out MediaToolInfo mediaToolInfo)
    {
        // Not implemented
        mediaToolInfo = null;
        return false;
    }

    public static string GetStartStopSplit(TimeSpan timeStart, TimeSpan timeEnd) =>
        $"--split parts:{(int)timeStart.TotalSeconds}s-{(int)timeEnd.TotalSeconds}s";

    public static string GetStartSplit(TimeSpan timeSpan) =>
        $"--split parts:{(int)timeSpan.TotalSeconds}s-";

    public static string GetStopSplit(TimeSpan timeSpan) =>
        $"--split parts:-{(int)timeSpan.TotalSeconds}s";

    public bool GetMkvInfo(string fileName, out MediaInfo mediaInfo)
    {
        mediaInfo = null;
        return GetMkvInfoJson(fileName, out string json) && GetMkvInfoFromJson(json, out mediaInfo);
    }

    public bool GetMkvInfoJson(string fileName, out string json)
    {
        // Get media info as JSON
        StringBuilder commandline = new();
        // Normalize IETF tags to extended format, e.g. zh-cmn-Hant vs. cmn-Hant
        _ = commandline.Append(
            CultureInfo.InvariantCulture,
            $"--normalize-language-ietf extlang --identify \"{fileName}\" --identification-format json"
        );
        int exitCode = Command(commandline.ToString(), out json);
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
            MkvToolJsonSchema.MkvMerge mkvMerge = MkvToolJsonSchema.MkvMerge.FromJson(json);
            if (mkvMerge == null || mkvMerge.Tracks.Count == 0)
            {
                return false;
            }

            // Tracks
            foreach (MkvToolJsonSchema.Track track in mkvMerge.Tracks)
            {
                // If the container is not a MKV, ignore missing CodecId's
                if (
                    !mkvMerge.Container.Type.Equals("Matroska", StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrEmpty(track.Properties.CodecId)
                )
                {
                    Log.Warning(
                        "MkvToolJsonSchema : Overriding unknown codec for non-Matroska container : Codec: {Codec}",
                        track.Properties.CodecId
                    );
                    track.Properties.CodecId = "Unknown";
                }

                // Process by track type
                switch (track.Type.ToLowerInvariant())
                {
                    case "video":
                        mediaInfo.Video.Add(new(track));
                        break;
                    case "audio":
                        mediaInfo.Audio.Add(new(track));
                        break;
                    case "subtitles":
                        // TODO: Some variants of DVBSUB are not supported by MkvToolNix
                        // https://gitlab.com/mbunkus/mkvtoolnix/-/issues/1648
                        // https://github.com/ietf-wg-cellar/matroska-specification/pull/77/
                        // https://gitlab.com/mbunkus/mkvtoolnix/-/issues/3258
                        // TODO: Reported fixed, to be verified
                        mediaInfo.Subtitle.Add(new(track));
                        break;
                    default:
                        Log.Warning(
                            "MkvToolJsonSchema : Unknown track type : {TrackType}",
                            track.Type
                        );
                        break;
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
            mediaInfo.HasTags =
                mkvMerge.GlobalTags.Count > 0
                || mkvMerge.TrackTags.Count > 0
                || mediaInfo.Attachments > 0
                || !string.IsNullOrEmpty(mkvMerge.Container.Properties.Title);

            // Duration in nanoseconds
            mediaInfo.Duration = TimeSpan.FromSeconds(
                mkvMerge.Container.Properties.Duration / 1000000.0
            );

            // Must be Matroska type
            if (!IsMkvContainer(mediaInfo))
            {
                Log.Warning(
                    "MkvToolJsonSchema : MKV file type is not Matroska : {Type}",
                    mkvMerge.Container.Type
                );

                // ReMux to convert to MKV
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

    public static bool IsMkvContainer(MediaInfo mediaInfo) =>
        mediaInfo.Container.Equals("Matroska", StringComparison.OrdinalIgnoreCase);

    public bool ReMuxToMkv(
        string inputName,
        SelectMediaInfo selectMediaInfo,
        string outputName,
        out string error
    )
    {
        // Verify correct media type
        Debug.Assert(selectMediaInfo.Selected.Parser == ToolType.MkvMerge);

        // Delete output file
        _ = FileEx.DeleteFile(outputName);

        // Defaults
        StringBuilder commandline = new();
        _ = commandline.Append($"{MergeOptions} ");

        // Snippets
        if (Program.Options.TestSnippets)
        {
            _ = commandline.Append(
                CultureInfo.InvariantCulture,
                $"{GetStopSplit(Program.SnippetTimeSpan)} "
            );
        }

        // Output file
        _ = commandline.Append(CultureInfo.InvariantCulture, $"--output \"{outputName}\" ");

        // Selected is Keep
        // NotSelected is Remove
        CreateTrackArgs(selectMediaInfo.Selected, commandline);
        _ = commandline.Append(CultureInfo.InvariantCulture, $"\"{inputName}\"");

        // ReMux tracks
        int exitCode = Command(commandline.ToString(), 5, out error, out _);
        return exitCode is 0 or 1;
    }

    public bool ReMuxToMkv(string inputName, string outputName, out string error)
    {
        // Delete output file
        _ = FileEx.DeleteFile(outputName);

        // Defaults
        StringBuilder commandline = new();
        _ = commandline.Append($"{MergeOptions} ");

        // Snippets
        if (Program.Options.TestSnippets)
        {
            _ = commandline.Append(
                CultureInfo.InvariantCulture,
                $"{GetStopSplit(Program.SnippetTimeSpan)} "
            );
        }

        // Output file
        _ = commandline.Append(CultureInfo.InvariantCulture, $"--output \"{outputName}\" ");

        // Input file
        _ = commandline.Append(CultureInfo.InvariantCulture, $"\"{inputName}\"");

        // ReMux all
        int exitCode = Command(commandline.ToString(), 5, out error, out _);
        return exitCode is 0 or 1;
    }

    public bool RemoveSubtitles(string inputName, string outputName, out string error)
    {
        // Delete output file
        _ = FileEx.DeleteFile(outputName);

        // Defaults
        StringBuilder commandline = new();
        _ = commandline.Append($"{MergeOptions} ");

        // Snippets
        if (Program.Options.TestSnippets)
        {
            _ = commandline.Append(
                CultureInfo.InvariantCulture,
                $"{GetStopSplit(Program.SnippetTimeSpan)} "
            );
        }

        // Output file
        _ = commandline.Append(CultureInfo.InvariantCulture, $"--output \"{outputName}\" ");

        // No subtitles and input file
        _ = commandline.Append(CultureInfo.InvariantCulture, $"--no-subtitles \"{inputName}\"");

        // ReMux tracks
        int exitCode = Command(commandline.ToString(), 5, out error, out _);
        return exitCode is 0 or 1;
    }

    public bool MergeToMkv(
        string sourceOne,
        string sourceTwo,
        MediaInfo keepTwo,
        string outputName,
        out string error
    )
    {
        // Merge all tracks from sourceOne with selected tracks in sourceTwo

        // Verify correct parser type
        Debug.Assert(keepTwo.Parser == ToolType.MkvMerge);

        // Delete output file
        _ = FileEx.DeleteFile(outputName);

        // Defaults
        StringBuilder commandline = new();
        _ = commandline.Append($"{MergeOptions} ");

        // Snippets
        if (Program.Options.TestSnippets)
        {
            _ = commandline.Append(
                CultureInfo.InvariantCulture,
                $"{GetStopSplit(Program.SnippetTimeSpan)} "
            );
        }

        // Output file
        _ = commandline.Append(CultureInfo.InvariantCulture, $"--output \"{outputName}\" ");

        // Source one as is
        _ = commandline.Append(CultureInfo.InvariantCulture, $"\"{sourceOne}\" ");

        // Source two track options
        CreateTrackArgs(keepTwo, commandline);

        // Source two
        _ = commandline.Append(CultureInfo.InvariantCulture, $"\"{sourceTwo}\"");

        // ReMux tracks
        int exitCode = Command(commandline.ToString(), 5, out error, out _);
        return exitCode is 0 or 1;
    }

    private static void CreateTrackArgs(MediaInfo mediaInfo, StringBuilder commandline)
    {
        // Verify correct media type
        Debug.Assert(mediaInfo.Parser == ToolType.MkvMerge);

        // Create the track number filters
        // The track numbers are reported by MkvMerge --identify, use the track.id values
        _ =
            mediaInfo.Video.Count > 0
                ? commandline.Append(
                    CultureInfo.InvariantCulture,
                    $"--video-tracks {string.Join(",", mediaInfo.Video.Select(info => $"{info.Id}"))} "
                )
                : commandline.Append("--no-video ");
        _ =
            mediaInfo.Audio.Count > 0
                ? commandline.Append(
                    CultureInfo.InvariantCulture,
                    $"--audio-tracks {string.Join(",", mediaInfo.Audio.Select(info => $"{info.Id}"))} "
                )
                : commandline.Append("--no-audio ");
        _ =
            mediaInfo.Subtitle.Count > 0
                ? commandline.Append(
                    CultureInfo.InvariantCulture,
                    $"--subtitle-tracks {string.Join(",", mediaInfo.Subtitle.Select(info => $"{info.Id}"))} "
                )
                : commandline.Append("--no-subtitles ");
    }

    private const string MergeOptions =
        "--disable-track-statistics-tags --no-global-tags --no-track-tags --no-attachments --no-buttons --normalize-language-ietf extlang";

    private const string InstalledVersionPattern = @"([^\s]+)\ v(?<version>.*?)\ \(";

    [GeneratedRegex(InstalledVersionPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    public static partial Regex InstalledVersionRegex();
}
