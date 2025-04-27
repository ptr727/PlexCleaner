using System;
using System.Diagnostics;
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
        return GetMediaInfoXml(fileName, out string xml) && GetMediaInfoFromXml(xml, out mediaInfo);
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

    public static bool GetMediaInfoFromXml(string xml, out MediaInfo mediaInfo)
    {
        // Parser type is MediaInfo
        mediaInfo = new MediaInfo(ToolType.MediaInfo);

        // Populate the MediaInfo object from the XML string
        try
        {
            // Deserialize
            MediaInfoToolXmlSchema.MediaInfo xmInfo = MediaInfoToolXmlSchema.MediaInfo.FromXml(xml);
            MediaInfoToolXmlSchema.MediaElement xmlMedia = xmInfo.Media;

            // No tracks
            if (xmlMedia.Tracks.Count == 0)
            {
                return false;
            }

            // Tracks
            foreach (MediaInfoToolXmlSchema.Track track in xmlMedia.Tracks)
            {
                if (track.Type.Equals("Video", StringComparison.OrdinalIgnoreCase))
                {
                    VideoInfo info = new(track);
                    mediaInfo.Video.Add(info);
                }
                else if (track.Type.Equals("Audio", StringComparison.OrdinalIgnoreCase))
                {
                    // Skip sub-tracks e.g. 0-1
                    if (
                        string.IsNullOrEmpty(track.CodecId)
                        && track.Id.Contains('-', StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        Log.Warning("MediaInfo skipping Audio sub-track : {TrackId}", track.Id);
                        continue;
                    }

                    AudioInfo info = new(track);
                    mediaInfo.Audio.Add(info);
                }
                else if (track.Type.Equals("Text", StringComparison.OrdinalIgnoreCase))
                {
                    SubtitleInfo info = new(track);
                    mediaInfo.Subtitle.Add(info);
                }
            }

            // Errors, any unsupported tracks
            mediaInfo.HasErrors = mediaInfo.Unsupported;

            // TODO: Tags, look in the Extra field, but not reliable
            // TODO: Duration, too many different formats to parse
            // https://github.com/MediaArea/MediaInfoLib/blob/master/Source/Resource/Text/Stream/General.csv#L92-L98
            // TODO: ContainerType
            // TODO: Chapters
            // TODO: Attachments
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            return false;
        }
        return true;
    }

    private const string InstalledVersionPattern = @"MediaInfoLib\ -\ v(?<version>.*)";

    [GeneratedRegex(InstalledVersionPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    public static partial Regex InstalledVersionRegex();

    // Common format tags
    public const string HDR10Format = "SMPTE ST 2086";
    public const string HDR10PlusFormat = "SMPTE ST 2094";
    public const string H264Format = "h264";
    public const string H265Format = "hevc";
    public const string AV1Format = "av1";
}
