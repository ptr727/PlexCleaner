using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Serilog;

// http://manpages.ubuntu.com/manpages/zesty/man1/mediainfo.1.html

namespace PlexCleaner;

public partial class MediaInfoTool : MediaTool
{
    public override ToolFamily GetToolFamily() => ToolFamily.MediaInfo;

    public override ToolType GetToolType() => ToolType.MediaInfo;

    protected override string GetToolNameWindows() => "mediainfo.exe";

    protected override string GetToolNameLinux() => "mediainfo";

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

        // Second line of stdout as version
        // E.g. Windows : "MediaInfoLib - v20.09"
        // E.g. Linux : "MediaInfoLib - v20.09"
        string[] lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        // Extract the short version number
        Match match = InstalledVersionRegex().Match(lines[1]);
        Debug.Assert(match.Success);
        mediaToolInfo.Version = match.Groups["version"].Value;
        Debug.Assert(Version.TryParse(mediaToolInfo.Version, out _));

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

    protected override bool GetLatestVersionWindows(out MediaToolInfo mediaToolInfo)
    {
        // Initialize
        mediaToolInfo = new MediaToolInfo(this);

        try
        {
            // Get the latest release version number from github releases
            // https://github.com/MediaArea/MediaInfo
            const string repo = "MediaArea/MediaInfo";
            mediaToolInfo.Version = GetLatestGitHubRelease(repo);

            // Strip the "v", v23.09 -> 23.09
            Debug.Assert(mediaToolInfo.Version.StartsWith('v'));
            mediaToolInfo.Version = mediaToolInfo.Version[1..];

            // Create the filename using the version number
            // MediaInfo_CLI_17.10_Windows_x64.zip
            mediaToolInfo.FileName = $"MediaInfo_CLI_{mediaToolInfo.Version}_Windows_x64.zip";

            // Create the download Uri, binaries are not published on GitHub
            // https://mediaarea.net/download/binary/mediainfo/17.10/MediaInfo_CLI_17.10_Windows_x64.zip
            mediaToolInfo.Url =
                $"https://mediaarea.net/download/binary/mediainfo/{mediaToolInfo.Version}/{mediaToolInfo.FileName}";
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

    public bool GetMediaInfo(string fileName, out MediaInfo mediaInfo)
    {
        mediaInfo = null;
        return GetMediaInfoXml(fileName, out string xml)
            && GetMediaInfoFromXml(xml, fileName, out mediaInfo);
    }

    public bool GetMediaInfoXml(string fileName, out string xml)
    {
        // Get media info as XML
        string commandline = $"--Output=XML \"{fileName}\"";
        int exitCode = Command(commandline, out xml);

        // TODO: No error is returned when the file does not exist
        // https://sourceforge.net/p/mediainfo/bugs/1052/
        // Empty XML files are around 86 bytes
        // Match size check with ProcessSidecarFile()
        return exitCode == 0 && xml.Length >= 100;
    }

    public static bool GetMediaInfoFromXml(string xml, string fileName, out MediaInfo mediaInfo)
    {
        // Parser type is MediaInfo
        mediaInfo = new MediaInfo(ToolType.MediaInfo);

        // Populate the MediaInfo object from the XML string
        try
        {
            // Deserialize
            MediaInfoToolXmlSchema.MediaInfo xmInfo = MediaInfoToolXmlSchema.MediaInfo.FromXml(xml);
            MediaInfoToolXmlSchema.MediaElement xmlMedia = xmInfo.Media;
            if (xmInfo.Media == null || xmlMedia.Tracks.Count == 0)
            {
                return false;
            }

            // Tracks
            foreach (MediaInfoToolXmlSchema.Track track in xmlMedia.Tracks)
            {
                // Handle sub-tracks e.g. 0-1, 256-CC1, 256-1
                if (HandleSubTrack(track, fileName, mediaInfo))
                {
                    continue;
                }

                // Process by track type
                switch (track.Type.ToLowerInvariant())
                {
                    case "general":
                        if (!string.IsNullOrEmpty(track.Duration))
                        {
                            mediaInfo.Duration = TimeSpan.FromMicroseconds(
                                double.Parse(track.Duration, CultureInfo.InvariantCulture)
                                    * 1000000.0
                            );
                        }
                        mediaInfo.Container = track.Format;
                        break;
                    case "video":
                        mediaInfo.Video.Add(VideoInfo.Create(fileName, track));
                        break;
                    case "audio":
                        mediaInfo.Audio.Add(AudioInfo.Create(fileName, track));
                        break;
                    case "text":
                        mediaInfo.Subtitle.Add(SubtitleInfo.Create(fileName, track));
                        break;
                    case "menu":
                        // TODO: Verify chapters get removed
                        break;
                    default:
                        Log.Warning(
                            "MediaInfoToolXmlSchema : Unknown track type : {TrackType} : {FileName}",
                            track.Type,
                            fileName
                        );
                        break;
                }
            }

            // Errors, any unsupported tracks
            mediaInfo.HasErrors = mediaInfo.Unsupported;

            // TODO: Tags, look in the Extra field, but not reliable
            // TODO: Chapters
            // TODO: Attachments
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            return false;
        }
        return true;
    }

    private static bool HandleSubTrack(
        MediaInfoToolXmlSchema.Track track,
        string fileName,
        MediaInfo mediaInfo
    )
    {
        // Handle sub-tracks e.g. 0-1, 256-CC1, 256-1
        if (!track.Id.Contains('-', StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Id maps to Number
        // StreamOrder maps to Id

        // Test for a closed caption tracks
        // <track type="Video" typeorder="4">
        //     <ID>256</ID>
        //     <Format>MPEG Video</Format>
        // <track type="Text" typeorder="1">
        //     <ID>256-CC1</ID>
        //     <Format>EIA-608</Format>
        //     <MuxingMode>A/53 / DTVCC Transport</MuxingMode>
        if (
            track.Type.Equals("Text", StringComparison.OrdinalIgnoreCase)
            && (
                track.Format.Equals("EIA-608", StringComparison.OrdinalIgnoreCase)
                || track.Format.Equals("EIA-708", StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            // Parse the number
            Match match = TrackRegex().Match(track.Id);
            Debug.Assert(match.Success);
            int number = int.Parse(match.Groups["id"].Value, CultureInfo.InvariantCulture);

            // Find the video track matching the number
            if (mediaInfo.Video.Find(item => item.Number == number) is { } videoTrack)
            {
                // Set the closed caption flag
                Log.Information(
                    "MediaInfoToolXmlSchema : Setting closed captions flag from sub-track : Id: {Id}, Sub-Track: {Number}, Format: {Format} : {FileName}",
                    videoTrack.Id,
                    track.Id,
                    track.Format,
                    fileName
                );
                videoTrack.ClosedCaptions = true;
            }
            else
            {
                // Could not find matching video track
                Log.Error(
                    "MediaInfoToolXmlSchema : Closed caption sub-track track with missing video track : Sub-Track: {Number}, Format: {Format} : {FileName}",
                    track.Id,
                    track.Format,
                    fileName
                );
            }

            // Done with this track
            return true;
        }

        // Skip sub-tacks
        Log.Warning(
            "MediaInfoToolXmlSchema : Skipping sub-track : Type: {Type}, Id: {Id}, Format: {Format} : {FileName}",
            track.Type,
            track.Id,
            track.Format,
            fileName
        );

        return true;
    }

    private const string InstalledVersionPattern = @"MediaInfoLib\ -\ v(?<version>.*)";

    [GeneratedRegex(InstalledVersionPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    public static partial Regex InstalledVersionRegex();

    [GeneratedRegex(@"(?<id>\d+)")]
    public static partial Regex TrackRegex();

    // Common format tags
    public const string HDR10Format = "SMPTE ST 2086";
    public const string HDR10PlusFormat = "SMPTE ST 2094";
    public const string H264Format = "h264";
    public const string H265Format = "hevc";
    public const string AV1Format = "av1";
}
