using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using InsaneGenius.Utilities;
using Serilog;

// https://ffmpeg.org/ffprobe.html

namespace PlexCleaner;

// Use FfMpeg family
public class FfProbeTool : FfMpegTool
{
    public override ToolType GetToolType() => ToolType.FfProbe;

    protected override string GetToolNameWindows() => "ffprobe.exe";

    protected override string GetToolNameLinux() => "ffprobe";

    public bool GetPacketInfo(string commandline, out List<FfMpegToolJsonSchema.Packet> packetList)
    {
        // Init
        packetList = null;

        // Write JSON text output to compressed memory stream to save memory
        // TODO: Do the packet calculation in ProcessEx.OutputHandler() instead of writing all output to stream then processing the stream
        // Make sure that the various stream processors leave the memory stream open for the duration of operations
        using MemoryStream memoryStream = new();
        using GZipStream compressStream = new(memoryStream, CompressionMode.Compress, true);
        using ProcessEx process = new();
        process.RedirectOutput = true;
        process.OutputStream = new StreamWriter(compressStream);
        process.RedirectError = true;
        process.ConsoleError = !Program.Options.Parallel;

        // Get packet info
        string path = GetToolPath();
        Log.Information("Executing {ToolType} : {Parameters}", GetToolType(), commandline);
        int exitCode = process.ExecuteEx(path, commandline);
        process.OutputStream.Close();
        if (exitCode != 0 || memoryStream.Length == 0)
        {
            return false;
        }

        // Read JSON from stream
        _ = memoryStream.Seek(0, SeekOrigin.Begin);
        using GZipStream decompressStream = new(memoryStream, CompressionMode.Decompress, true);
        FfMpegToolJsonSchema.PacketInfo packetInfo =
            JsonSerializer.Deserialize<FfMpegToolJsonSchema.PacketInfo>(
                decompressStream,
                ConfigFileJsonSchema.JsonReadOptions
            );
        if (packetInfo == null)
        {
            return false;
        }

        packetList = packetInfo.Packets;
        return true;
    }

    public bool GetSubCcPacketInfo(
        string fileName,
        out List<FfMpegToolJsonSchema.Packet> packetList
    )
    {
        // Build commandline
        StringBuilder commandline = new();
        _ = commandline.Append("-loglevel error ");
        if (Program.Options.TestSnippets)
        {
            _ = commandline.Append(
                CultureInfo.InvariantCulture,
                $"-read_intervals %{Program.SnippetTimeSpan:mm\\:ss} "
            );
        }
        // Get packet info using ccsub filter
        // https://www.ffmpeg.org/ffmpeg-devices.html#Options-10
        _ = commandline.Append(
            CultureInfo.InvariantCulture,
            $"-select_streams s:0 -f lavfi -i \"movie={EscapeMovieFileName(fileName)}[out0+subcc]\" -show_packets -print_format json"
        );

        return GetPacketInfo(commandline.ToString(), out packetList);
    }

    public static string EscapeMovieFileName(string fileName) =>
        // Escape the file name, specifically : \ ' characters
        // \ -> /
        // : -> \\:
        // ' -> \\\'
        // , -> \\\,
        // https://superuser.com/questions/1893137/how-to-quote-a-file-name-containing-single-quotes-in-ffmpeg-ffprobe-movie-filena
        fileName
            .Replace(@"\", @"/")
            .Replace(@":", @"\\:")
            .Replace(@"'", @"\\\'")
            .Replace(@",", @"\\\,");

    public bool GetBitratePacketInfo(
        string fileName,
        out List<FfMpegToolJsonSchema.Packet> packetList
    )
    {
        // Build commandline
        StringBuilder commandline = new();
        _ = commandline.Append("-loglevel error ");
        if (Program.Options.TestSnippets)
        {
            _ = commandline.Append(
                CultureInfo.InvariantCulture,
                $"-read_intervals %{Program.SnippetTimeSpan:mm\\:ss} "
            );
        }
        _ = commandline.Append(
            CultureInfo.InvariantCulture,
            $"-show_packets -print_format json \"{fileName}\""
        );

        return GetPacketInfo(commandline.ToString(), out packetList);
    }

    public bool GetFfProbeInfo(string fileName, out MediaInfo mediaInfo)
    {
        mediaInfo = null;
        return GetFfProbeInfoJson(fileName, out string json)
            && GetFfProbeInfoFromJson(json, out mediaInfo);
    }

    public bool GetFfProbeInfoJson(string fileName, out string json)
    {
        // Get media info as JSON
        string commandline =
            $"-loglevel quiet -show_streams -show_format -print_format json \"{fileName}\"";
        int exitCode = Command(commandline, out json, out string error);
        return exitCode == 0 && error.Length == 0;
    }

    public bool GetFfProbeInfoText(string fileName, out string text)
    {
        // Get media info using default output
        string commandline = $"-hide_banner \"{fileName}\"";
        int exitCode = Command(commandline, out _, out text);
        return exitCode == 0;
    }

    public static bool GetFfProbeInfoFromJson(string json, out MediaInfo mediaInfo)
    {
        // Parser type is FfProbe
        mediaInfo = new MediaInfo(ToolType.FfProbe);

        // Populate the MediaInfo object from the JSON string
        try
        {
            // Deserialize
            FfMpegToolJsonSchema.FfProbe ffProbe = FfMpegToolJsonSchema.FfProbe.FromJson(json);
            if (ffProbe == null)
            {
                return false;
            }

            // No tracks
            if (ffProbe.Tracks.Count == 0)
            {
                return false;
            }

            // Tracks
            foreach (FfMpegToolJsonSchema.Track stream in ffProbe.Tracks)
            {
                // Process by track type
                if (stream.CodecType.Equals("video", StringComparison.OrdinalIgnoreCase))
                {
                    VideoInfo info = new(stream);
                    mediaInfo.Video.Add(info);
                }
                else if (stream.CodecType.Equals("audio", StringComparison.OrdinalIgnoreCase))
                {
                    AudioInfo info = new(stream);
                    mediaInfo.Audio.Add(info);
                }
                else if (stream.CodecType.Equals("subtitle", StringComparison.OrdinalIgnoreCase))
                {
                    // Some subtitle codecs are not supported, e.g. S_TEXT / WEBVTT
                    if (
                        string.IsNullOrEmpty(stream.CodecName)
                        || string.IsNullOrEmpty(stream.CodecLongName)
                    )
                    {
                        Log.Warning("FfProbe Subtitle Format unknown");
                        if (string.IsNullOrEmpty(stream.CodecName))
                        {
                            stream.CodecName = "Unknown";
                        }

                        if (string.IsNullOrEmpty(stream.CodecLongName))
                        {
                            stream.CodecLongName = "Unknown";
                        }
                    }

                    SubtitleInfo info = new(stream);
                    mediaInfo.Subtitle.Add(info);
                }
            }

            // Errors, any unsupported tracks
            mediaInfo.HasErrors = mediaInfo.Unsupported;

            // Unwanted tags
            mediaInfo.HasTags = HasUnwantedTags(ffProbe.Format.Tags);

            // Duration in seconds
            mediaInfo.Duration = TimeSpan.FromSeconds(ffProbe.Format.Duration);

            // Container type
            mediaInfo.Container = ffProbe.Format.FormatName;

            // TODO: Chapters
            // TODO: Attachments
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            return false;
        }
        return true;
    }

    private static bool HasUnwantedTags(Dictionary<string, string> tags) =>
        // TODO: Find a more reliable method for determining what tags are expected or not

        // Format tags:
        // "encoder": "libebml v1.4.2 + libmatroska v1.6.4",
        // "creation_time": "2022-03-10T12:55:01.000000Z"

        // Stream tags:
        // "language": "eng",
        // "BPS": "4969575",
        // "DURATION": "00:42:30.648000000",
        // "NUMBER_OF_FRAMES": "76434",
        // "NUMBER_OF_BYTES": "1584454580",
        // "_STATISTICS_WRITING_APP": "mkvmerge v61.0.0 ('So') 64-bit",
        // "_STATISTICS_WRITING_DATE_UTC": "2022-03-10 12:55:01",
        // "_STATISTICS_TAGS": "BPS DURATION NUMBER_OF_FRAMES NUMBER_OF_BYTES"

        // Language and title are expected tags
        // Look for undesirable Tags
        tags.Keys.Any(key =>
            s_undesirableTags.Any(tag => tag.Equals(key, StringComparison.OrdinalIgnoreCase))
        );

    // "Undesirable" tags
    private static readonly string[] s_undesirableTags = ["statistics"];
}
