using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;
using Serilog;

// http://manpages.ubuntu.com/manpages/zesty/man1/mediainfo.1.html

namespace PlexCleaner;

public partial class MediaInfo
{
    // Common format tags
    public const string HDR10Format = "SMPTE ST 2086";
    public const string HDR10PlusFormat = "SMPTE ST 2094";
    public const string H264Format = "h264";
    public const string H265Format = "hevc";
    public const string AV1Format = "av1";

    public partial class Tool : MediaTool
    {
        public override ToolFamily GetToolFamily() => ToolFamily.MediaInfo;

        public override ToolType GetToolType() => ToolType.MediaInfo;

        protected override string GetToolNameWindows() => "mediainfo.exe";

        protected override string GetToolNameLinux() => "mediainfo";

        public IGlobalOptions GetBuilder() => Builder.Create(GetToolPath());

        public override bool GetInstalledVersion(out MediaToolInfo mediaToolInfo)
        {
            // Get version info
            mediaToolInfo = new MediaToolInfo(this) { FileName = GetToolPath() };
            Command command = Builder.Version(GetToolPath());
            return Execute(command, out BufferedCommandResult result)
                && result.ExitCode == 0
                && GetVersion(result.StandardOutput, mediaToolInfo);
        }

        public static bool GetVersion(string text, MediaToolInfo mediaToolInfo)
        {
            // Get file info
            if (File.Exists(mediaToolInfo.FileName))
            {
                FileInfo fileInfo = new(mediaToolInfo.FileName);
                mediaToolInfo.ModifiedTime = fileInfo.LastWriteTimeUtc;
                mediaToolInfo.Size = fileInfo.Length;
            }

            // "MediaInfoLib - v20.09"

            // Parse version
            string[] lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Match match = InstalledVersionRegex().Match(lines[1]);
            Debug.Assert(match.Success && Version.TryParse(match.Groups["version"].Value, out _));
            mediaToolInfo.Version = match.Groups["version"].Value;
            return true;
        }

        protected override bool GetLatestVersionWindows(out MediaToolInfo mediaToolInfo)
        {
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
            catch (Exception e)
                when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
            {
                return false;
            }
            return true;
        }

        public bool GetMediaProps(string fileName, out MediaProps mediaProps)
        {
            // TODO: Switch to JSON version
            mediaProps = null;
            return GetMediaPropsXml(fileName, out string xml)
                && GetMediaPropsFromXml(xml, fileName, out mediaProps);
        }

        public bool GetMediaPropsXml(string fileName, out string xml)
        {
            // Build command line
            xml = string.Empty;
            Command command = GetBuilder()
                .GlobalOptions(options => options.Default())
                .MediaInfoOptions(options => options.OutputFormatXml().InputFile(fileName))
                .Build();

            // Execute command
            Log.Information("Getting media info : {FileName}", fileName);
            if (!Execute(command, false, true, out BufferedCommandResult result))
            {
                return false;
            }
            if (result.ExitCode != 0)
            {
                Log.Error(
                    "{ToolType} : Failed to to get media info : {FileName}",
                    GetToolType(),
                    fileName
                );
                Log.Error("{ToolType} : {Error}", GetToolType(), result.StandardError.Trim());
                return false;
            }
            if (result.StandardError.Length > 0)
            {
                Log.Warning(
                    "{ToolType} : Warning getting media info : {FileName}",
                    GetToolType(),
                    fileName
                );
                Log.Warning("{ToolType} : {Warning}", GetToolType(), result.StandardError.Trim());
            }

            // Get XML output
            xml = result.StandardOutput;

            // TODO: No error is returned when the file does not exist
            // https://sourceforge.net/p/mediainfo/bugs/1052/
            // Empty XML files are around 86 bytes
            // Match size check with ProcessSidecarFile()
            return xml.Length >= 100;
        }

        public static bool GetMediaPropsFromXml(
            string xml,
            string fileName,
            out MediaProps mediaProps
        )
        {
            // Populate the MediaInfo object from the XML string
            mediaProps = new MediaProps(ToolType.MediaInfo, fileName);
            try
            {
                // Deserialize
                MediaInfoToolXmlSchema.MediaInfo xmlInfo = MediaInfoToolXmlSchema.MediaInfo.FromXml(
                    xml
                );
                ArgumentNullException.ThrowIfNull(xmlInfo);
                MediaInfoToolXmlSchema.MediaElement xmlMedia = xmlInfo.Media;
                ArgumentNullException.ThrowIfNull(xmlMedia);
                if (xmlMedia.Tracks.Count == 0)
                {
                    Log.Error(
                        "{ToolType} : Container not supported : Tracks: {Tracks} : {FileName}",
                        mediaProps.Parser,
                        xmlMedia.Tracks.Count,
                        fileName
                    );
                    return false;
                }

                // Tracks
                foreach (MediaInfoToolXmlSchema.Track track in xmlMedia.Tracks)
                {
                    // Process by track type
                    switch (track.Type.ToLowerInvariant())
                    {
                        case "general":
                            if (!string.IsNullOrEmpty(track.Duration))
                            {
                                mediaProps.Duration = TimeSpan.FromMicroseconds(
                                    double.Parse(track.Duration, CultureInfo.InvariantCulture)
                                        * 1000000.0
                                );
                            }
                            mediaProps.Container = track.Format;
                            break;
                        case "video":
                            VideoProps videoProps = new(mediaProps);
                            if (videoProps.Create(track))
                            {
                                mediaProps.Video.Add(videoProps);
                            }
                            break;
                        case "audio":
                            AudioProps audioProps = new(mediaProps);
                            if (audioProps.Create(track))
                            {
                                mediaProps.Audio.Add(audioProps);
                            }
                            break;
                        case "text":
                            SubtitleProps subtitleProps = new(mediaProps);
                            if (subtitleProps.Create(track))
                            {
                                mediaProps.Subtitle.Add(subtitleProps);
                            }
                            break;
                        case "menu":
                            // TODO: Verify chapters get removed
                            break;
                        default:
                            Log.Warning(
                                "{Parser} : Unknown track type : {TrackType} : {FileName}",
                                mediaProps.Parser,
                                track.Type,
                                fileName
                            );
                            break;
                    }
                }

                // Errors, any unsupported tracks
                mediaProps.HasErrors = mediaProps.Unsupported;

                // TODO: Tags, look in the Extra field, but not reliable
                // TODO: Chapters
                // TODO: Attachments
            }
            catch (Exception e)
                when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
            {
                return false;
            }
            return true;
        }

        public static bool ParseSubTrack(string text, out long number)
        {
            // Parse the track number
            number = -1;
            Match match = TrackRegex().Match(text);
            return match.Success && long.TryParse(match.Groups["id"].Value, out number);
        }

        [GeneratedRegex(
            @"MediaInfoLib\ -\ v(?<version>.*)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline
        )]
        public static partial Regex InstalledVersionRegex();

        [GeneratedRegex(@"(?<id>\d+)")]
        public static partial Regex TrackRegex();
    }
}
