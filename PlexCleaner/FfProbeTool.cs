using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    public static new string GetStartStopSplit(TimeSpan timeStart, TimeSpan timeEnd) =>
        $"-read_intervals +{(int)timeStart.TotalSeconds}%{(int)timeEnd.TotalSeconds}";

    public static new string GetStartSplit(TimeSpan timeSpan) =>
        $"-read_interval +{(int)timeSpan.TotalSeconds}%";

    public static new string GetStopSplit(TimeSpan timeSpan) =>
        $"-read_intervals %+{(int)timeSpan.TotalSeconds}";

    public bool GetPacketInfo(
        string commandline,
        out List<FfMpegToolJsonSchema.Packet> packetList,
        out string error
    )
    {
        // Init
        packetList = null;
        error = string.Empty;

        // Write JSON text output to compressed memory stream to save memory
        // Make sure that the various stream processors leave the memory stream open for the duration of operations
        using MemoryStream memoryStream = new();
        using GZipStream compressStream = new(memoryStream, CompressionMode.Compress, true);
        using ProcessEx process = new();
        process.RedirectOutput = true;
        process.OutputStream = new StreamWriter(compressStream);
        process.RedirectError = true;
        process.ErrorString = new StringHistory(5, 5);

        // Get packet info
        string path = GetToolPath();
        Log.Information("Executing {ToolType} : {Parameters}", GetToolType(), commandline);
        int exitCode = process.ExecuteEx(path, commandline);
        process.OutputStream.Close();
        if (exitCode != 0 || memoryStream.Length == 0)
        {
            error = process.ErrorString.ToString();
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
            Log.Error("Failed to DeSerialize JSON PacketInfo");
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
        // Quickscan
        // -t and read_intervals do not work with the subcc filter
        // https://superuser.com/questions/1893673/how-to-time-limit-the-input-stream-duration-when-using-movie-filenameout0subcc
        // ReMux using FFmpeg to a snippet file then scan the snippet file
        StringBuilder commandline = new();
        string error;
        if (Program.Options.QuickScan)
        {
            // Keep in sync with FfMpegTool.ReMuxToFormat()

            // Create a temp filename based on the input name
            string tempName = Path.ChangeExtension(fileName, ".tmp13");
            Debug.Assert(fileName != tempName);
            _ = FileEx.DeleteFile(tempName);

            // Default options
            _ = commandline.Append($"{GlobalOptions} ");

            // Quiet
            _ = commandline.Append(
                CultureInfo.InvariantCulture,
                $"{SilentOptions} -loglevel error "
            );

            // Add -fflags +genpts to generate missing timestamps
            _ = commandline.Append(CultureInfo.InvariantCulture, $"-fflags +genpts ");

            // Quickscan
            _ = commandline.Append(
                CultureInfo.InvariantCulture,
                $"{FfMpegTool.GetStopSplit(Program.QuickScanTimeSpan)} "
            );

            // Input filename
            _ = commandline.Append(CultureInfo.InvariantCulture, $"-i \"{fileName}\" ");

            // Default output options
            _ = commandline.Append($"{OutputOptions} ");

            // Use Matroska for snippet format as it supports more stream formats
            // E.g. DVCPRO videop streams can be muxed into MKV but not into TS
            // [mpegts @ 000001543cf744c0] Stream 0, codec dvvideo, is muxed as a private data stream and may not be recognized upon reading.

            // Copy only first video stream
            _ = commandline.Append(
                CultureInfo.InvariantCulture,
                $"-map 0:v -c copy -f matroska \"{tempName}\""
            );

            // Remux to temp file
            Log.Information("Creating temp media file : {TempFileName}", tempName);
            int exitCode = Tools.FfMpeg.Command(commandline.ToString(), 5, out _, out error);
            if (exitCode != 0)
            {
                Log.Error("Failed to create temp media file : {TempFileName}", tempName);
                Log.Error("{Error}", error);
                _ = FileEx.DeleteFile(tempName);
                packetList = null;
                return false;
            }

            // Use the temp file as the input file
            fileName = tempName;
        }

        // Quiet
        commandline = new();
        _ = commandline.Append("-hide_banner -loglevel error ");

        // Get packet info using subcc filter
        // https://www.ffmpeg.org/ffmpeg-devices.html#Options-10
        _ = commandline.Append(
            CultureInfo.InvariantCulture,
            $"-select_streams s:0 -f lavfi -i \"movie={EscapeMovieFileName(fileName)}[out0+subcc]\" -show_packets -print_format json"
        );
        Log.Information("Getting subcc packet info : {FileName}", fileName);
        bool ret = GetPacketInfo(commandline.ToString(), out packetList, out error);
        if (!ret)
        {
            Log.Error("Failed to get subcc packet info : {FileName}", fileName);
            Log.Error("{Error}", error);
        }
        if (Program.Options.QuickScan)
        {
            // Delete the temp file
            File.Delete(fileName);
        }
        return ret;
    }

    public static string EscapeMovieFileName(string fileName) =>
        // Escape the file name so that it does not interfere with building the filter graph
        // https://superuser.com/questions/1893137/how-to-quote-a-file-name-containing-single-quotes-in-ffmpeg-ffprobe-movie-filena
        // See av_get_token() in https://github.com/FFmpeg/FFmpeg/blob/master/libavutil/avstring.c
        fileName
            .Replace(@"\", @"/")
            .Replace(@":", @"\\:")
            .Replace(@"'", @"\\\'")
            .Replace(@",", @"\\\,")
            .Replace(@";", @"\\\;")
            .Replace(@"[", @"\\\[")
            .Replace(@"]", @"\\\]");

    public bool GetBitratePacketInfo(
        string fileName,
        out List<FfMpegToolJsonSchema.Packet> packetList
    )
    {
        // Quiet
        StringBuilder commandline = new();
        _ = commandline.Append("-hide_banner -loglevel error ");

        // Quickscan
        if (Program.Options.QuickScan)
        {
            _ = commandline.Append(
                CultureInfo.InvariantCulture,
                $"{GetStopSplit(Program.QuickScanTimeSpan)} "
            );
        }

        // Show packets in JSON format
        _ = commandline.Append(
            CultureInfo.InvariantCulture,
            $"-show_packets -print_format json \"{fileName}\""
        );

        // Get packet info
        Log.Information("Getting bitrate packet info : {FileName}", fileName);
        if (!GetPacketInfo(commandline.ToString(), out packetList, out string error))
        {
            Log.Error("Failed to get bitrate packet info : {FileName}", fileName);
            Log.Error("{Error}", error);
            return false;
        }
        return true;
    }

    public bool GetFfProbeInfo(string fileName, out MediaInfo mediaInfo)
    {
        mediaInfo = null;
        return GetFfProbeInfoJson(fileName, out string json)
            && GetFfProbeInfoFromJson(json, out mediaInfo);
    }

    public bool GetFfProbeInfoJson(string fileName, out string json)
    {
        // TODO: Add analyze_frames when available in all FFmpeg builds
        // https://github.com/FFmpeg/FFmpeg/commit/90af8e07b02e690a9fe60aab02a8bccd2cbf3f01

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
            if (ffProbe == null || ffProbe.Tracks.Count == 0)
            {
                return false;
            }

            // Tracks
            foreach (FfMpegToolJsonSchema.Track track in ffProbe.Tracks)
            {
                // Process by track type
                switch (track.CodecType.ToLowerInvariant())
                {
                    case "video":
                        mediaInfo.Video.Add(new(track));
                        break;
                    case "audio":
                        if (
                            string.IsNullOrEmpty(track.CodecName)
                            || string.IsNullOrEmpty(track.CodecLongName)
                        )
                        {
                            // Encrypted / DRM tracks, e.g. QuickTime audio report no codec information
                            // "codec_tag_string": "enca"
                            // "codec_tag": "0x61636e65"
                            if (!string.IsNullOrEmpty(track.CodecTagString))
                            {
                                Log.Warning(
                                    "FfMpegToolJsonSchema : Overriding unknown audio codec : Format: {Format}, Codec: {Codec}, CodecTagString: {CodecTagString}",
                                    track.CodecLongName,
                                    track.CodecName,
                                    track.CodecTagString
                                );
                                track.CodecLongName = track.CodecTagString;
                            }
                        }
                        mediaInfo.Audio.Add(new(track));
                        break;
                    case "subtitle":
                        if (
                            string.IsNullOrEmpty(track.CodecName)
                            || string.IsNullOrEmpty(track.CodecLongName)
                        )
                        {
                            // Some subtitle codecs are not supported by FFmpeg, e.g. S_TEXT / WEBVTT, but are supported by MKVToolNix
                            Log.Warning(
                                "FfMpegToolJsonSchema : Overriding unknown subtitle codec : Format: {Format}, Codec: {Codec}",
                                track.CodecLongName,
                                track.CodecName
                            );
                            if (string.IsNullOrEmpty(track.CodecName))
                            {
                                track.CodecName = "unknown";
                            }
                            if (string.IsNullOrEmpty(track.CodecLongName))
                            {
                                track.CodecLongName = "unknown";
                            }
                        }
                        mediaInfo.Subtitle.Add(new(track));
                        break;
                    default:
                        Log.Warning(
                            "FfMpegToolJsonSchema : Unknown track type : {CodecType}",
                            track.CodecType
                        );
                        break;
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
    private static readonly List<string> s_undesirableTags = ["statistics"];
}
