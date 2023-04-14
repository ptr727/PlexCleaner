using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using InsaneGenius.Utilities;
using Newtonsoft.Json;
using PlexCleaner.FfMpegToolJsonSchema;
using Serilog;

// https://ffmpeg.org/ffprobe.html

namespace PlexCleaner;

// Use FfMpeg family
public class FfProbeTool : FfMpegTool
{
    public override ToolType GetToolType()
    {
        return ToolType.FfProbe;
    }

    protected override string GetToolNameWindows()
    {
        return "ffprobe.exe";
    }

    protected override string GetToolNameLinux()
    {
        return "ffprobe";
    }

    public bool GetPacketInfo(string filename, out List<Packet> packets)
    {
        // Init
        packets = null;

        // Write JSON text output to compressed memory stream to save memory
        // TODO: Do the packet calculation in ProcessEx.OutputHandler() instead of writing all output to stream then processing the stream
        // Make sure that the various stream processors leave the memory stream open for the duration of operations
        using MemoryStream memoryStream = new();
        using GZipStream compressStream = new(memoryStream, CompressionMode.Compress, true);
        using ProcessEx process = new()
        {
            RedirectOutput = true,
            OutputStream = new StreamWriter(compressStream),
            // No console output
            RedirectError = true
        };

        // Build commandline
        StringBuilder commandline = new();
        commandline.Append("-loglevel error ");
        if (Program.Options.TestSnippets)
        {
            commandline.Append($"-read_intervals %{Program.SnippetTimeSpan.ToString("mm:ss")} ");
        }
        commandline.Append($"-show_packets -show_entries packet=codec_type,stream_index,pts_time,dts_time,duration_time,size -print_format json \"{filename}\"");

        // Get packet info
        string path = GetToolPath();
        Log.Logger.Information("Executing {ToolType} : {Parameters}", GetToolType(), commandline);
        int exitCode = process.ExecuteEx(path, commandline.ToString());
        if (exitCode != 0)
        {
            return false;
        }

        // Close and flush the output stream
        process.OutputStream.Close();

        // Read JSON from stream
        memoryStream.Seek(0, SeekOrigin.Begin);
        using GZipStream decompressStream = new(memoryStream, CompressionMode.Decompress, true);
        using StreamReader streamReader = new(decompressStream);
        using JsonTextReader jsonReader = new(streamReader);

        JsonSerializer serializer = new();
        var packetInfo = serializer.Deserialize<PacketInfo>(jsonReader);
        if (packetInfo == null)
        {
            return false;
        }

        packets = packetInfo.Packets;
        return true;
    }

    public bool GetFfProbeInfo(string filename, out MediaInfo mediainfo)
    {
        mediainfo = null;
        return GetFfProbeInfoJson(filename, out string json) &&
               GetFfProbeInfoFromJson(json, out mediainfo);
    }

    public bool GetFfProbeInfoJson(string filename, out string json)
    {
        // Get media info as JSON
        string commandline = $"-loglevel quiet -show_streams -show_format -print_format json \"{filename}\"";
        int exitCode = Command(commandline, out json, out string error);
        return exitCode == 0 && error.Length == 0;
    }

    public bool GetFfProbeInfoText(string filename, out string text)
    {
        // Get media info using default output
        string commandline = $"-hide_banner \"{filename}\"";
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
            FfProbe ffProbe = FfProbe.FromJson(json);
            if (ffProbe == null)
            {
                return false;
            }

            // No tracks
            if (ffProbe.Streams.Count == 0)
            {
                return false;
            }

            // Tracks
            foreach (FfMpegToolJsonSchema.Stream stream in ffProbe.Streams)
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
                    if (string.IsNullOrEmpty(stream.CodecName) ||
                        string.IsNullOrEmpty(stream.CodecLongName))
                    {
                        Log.Logger.Warning("FfProbe Subtitle Format unknown");
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

            // TODO: Errors

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

    private static bool HasUnwantedTags(Dictionary<string, string> tags)
    {
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

        // TODO: Find a more reliable method for determining what tags are expected or not

        // Look for undesirable Tags
        return tags.Keys.Any(key => UndesirableTags.Any(tag => tag.Equals(key, StringComparison.OrdinalIgnoreCase)));
    }

    // "Undesirable" tags
    private static readonly string[] UndesirableTags = { "statistics" };
}
