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
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;
using InsaneGenius.Utilities;
using Serilog;

// https://ffmpeg.org/ffprobe.html

namespace PlexCleaner;

public partial class FfProbe
{
    // Reuse FfMpeg family
    public class FfProbeTool : FfMpegTool
    {
        public override ToolType GetToolType() => ToolType.FfProbe;

        protected override string GetToolNameWindows() => "ffprobe.exe";

        protected override string GetToolNameLinux() => "ffprobe";

        public IGlobalOptions GetFfProbeBuilder() => FfProbeBuilder.Create(GetToolPath());

        public bool GetPacketList(
            Command command,
            out List<FfMpegToolJsonSchema.Packet> packetList,
            out string error
        )
        {
            // Init
            packetList = null;
            error = string.Empty;

            // Write JSON output to gzip memory stream to save memory
            using MemoryStream memoryStream = new();
            using GZipStream compressStream = new(memoryStream, CompressionMode.Compress, true);
            StringBuilder stdErrorBuffer = new();

            // Execute command
            if (
                !Execute(
                    command
                        .WithStandardOutputPipe(PipeTarget.ToStream(compressStream, true))
                        .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrorBuffer)),
                    out CommandResult result
                )
                || result.ExitCode != 0
                || memoryStream.Length == 0
            )
            {
                error = stdErrorBuffer.ToString();
                return false;
            }

            // Read JSON from gzip memory stream
            _ = memoryStream.Seek(0, SeekOrigin.Begin);
            using GZipStream decompressStream = new(memoryStream, CompressionMode.Decompress, true);
            FfMpegToolJsonSchema.PacketInfo packetInfo =
                JsonSerializer.Deserialize<FfMpegToolJsonSchema.PacketInfo>(
                    decompressStream,
                    ConfigFileJsonSchema.JsonReadOptions
                );
            if (packetInfo == null || packetInfo.Packets == null)
            {
                Log.Error("Failed to DeSerialize JSON PacketInfo");
                return false;
            }
            packetList = packetInfo.Packets;
            return true;
        }

        public async Task<(bool, string, List<FfMpegToolJsonSchema.Packet>)> GetPacketListAsync(
            Command command
        )
        {
            // TODO: This does not work
            // https://github.com/Tyrrrz/CliWrap/discussions/293

            // Write JSON output to memory stream
            using MemoryStream memoryStream = new();
            StringBuilder stdErrorBuffer = new();
            CommandTask<CommandResult> task = command
                .WithStandardOutputPipe(PipeTarget.ToStream(memoryStream, true))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrorBuffer))
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync(CancellationToken.None, Program.CancelToken());
            Log.Information(
                "Executing {ToolType} : ProcessId: {ProcessId}, Arguments: {Arguments}",
                GetToolType(),
                task.ProcessId,
                command.Arguments
            );

            // Execute command
            CommandResult result = await task;

            // Read JSON from memory stream
            FfMpegToolJsonSchema.PacketInfo packetInfo =
                await JsonSerializer.DeserializeAsync<FfMpegToolJsonSchema.PacketInfo>(
                    memoryStream,
                    ConfigFileJsonSchema.JsonReadOptions,
                    Program.CancelToken()
                );
            if (packetInfo == null || packetInfo.Packets == null)
            {
                Log.Error("Failed to DeSerialize JSON PacketInfo");
            }
            return (result.ExitCode == 0, stdErrorBuffer.ToString(), packetInfo?.Packets);
        }

        public bool GetSubCcPacketList(
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
                    $"{GetStopSplit(Program.QuickScanTimeSpan)} "
                );

                // Input filename
                _ = commandline.Append(CultureInfo.InvariantCulture, $"-i \"{fileName}\" ");

                // Default output options
                _ = commandline.Append($"{OutputOptions} ");

                // Use Matroska for snippet format as it supports more stream formats
                // E.g. DVCPRO video streams can be muxed into MKV but not into TS
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

            // Build command line
            // Get packet info using subcc filter
            // https://www.ffmpeg.org/ffmpeg-devices.html#Options-10
            Command command = GetFfProbeBuilder()
                .GlobalOptions(options => options.LogLevelQuiet().HideBanner())
                .FfProbeOptions(options =>
                    options
                        .SelectStreams("s:0")
                        .Format("lavfi")
                        .Input($"\"movie={EscapeMovieFileName(fileName)}[out0+subcc]\"")
                        .ShowPackets()
                        .OutputFormatJson()
                )
                .Build();

            // Get packet list
            Log.Information("Getting subcc packet info : {FileName}", fileName);
            bool ret = GetPacketList(command, out packetList, out error);
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

        public bool GetBitratePacketList(
            string fileName,
            out List<FfMpegToolJsonSchema.Packet> packetList
        )
        {
            // Build command line
            Command command = GetFfProbeBuilder()
                .GlobalOptions(options => options.LogLevelQuiet().HideBanner())
                .FfProbeOptions(options =>
                    options
                        .ReadIntervalsStop(
                            Program.Options.QuickScan ? Program.QuickScanTimeSpan : TimeSpan.Zero
                        )
                        .ShowPackets()
                        .OutputFormatJson()
                        .InputFile(fileName)
                )
                .Build();

            // Get packet list
            Log.Information("Getting bitrate packet info : {FileName}", fileName);

            // TODO: Convert to async when working
            //(bool result, string error, List<FfMpegToolJsonSchema.Packet> packetList) result =
            //    GetPacketListAsync(command).GetAwaiter().GetResult();

            if (!GetPacketList(command, out packetList, out string error))
            {
                Log.Error("Failed to get bitrate packet info : {FileName}", fileName);
                Log.Error("{Error}", error);
                return false;
            }
            return true;
        }

        public bool GetMediaProps(string fileName, out MediaProps mediaProps)
        {
            mediaProps = null;
            return GetMediaPropsJson(fileName, out string json)
                && GetMediaPropsFromJson(json, fileName, out mediaProps);
        }

        public bool GetMediaPropsJson(string fileName, out string json)
        {
            // TODO: Add analyze_frames when available in all FFmpeg builds
            // https://github.com/FFmpeg/FFmpeg/commit/90af8e07b02e690a9fe60aab02a8bccd2cbf3f01

            // Build command line
            json = string.Empty;
            Command command = GetFfProbeBuilder()
                .GlobalOptions(options => options.LogLevelQuiet().HideBanner())
                .FfProbeOptions(options =>
                    options.ShowStreams().ShowFormat().OutputFormatJson().InputFile(fileName)
                )
                .Build();

            // Execute command
            Log.Information("Getting media info : {FileName}", fileName);
            if (!Execute(command, out BufferedCommandResult result))
            {
                return false;
            }
            if (result.ExitCode != 0 || result.StandardError.Length > 0)
            {
                // Handle error
                Log.Error("Failed to to get media info : {FileName}", fileName);
                Log.Error("{Error}", result.StandardError);
                return false;
            }

            // Get JSON output
            json = result.StandardOutput;
            return true;
        }

        public bool GetFfProbeInfoText(string fileName, out string text)
        {
            // Get media info using default output
            string commandline = $"-hide_banner \"{fileName}\"";
            int exitCode = Command(commandline, out _, out text);
            return exitCode == 0;
        }

        public static bool GetMediaPropsFromJson(
            string json,
            string fileName,
            out MediaProps mediaProps
        )
        {
            // Parser type is FfProbe
            mediaProps = new MediaProps(ToolType.FfProbe);

            // Populate the MediaProps object from the JSON string
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
                            mediaProps.Video.Add(VideoProps.Create(fileName, track));
                            break;
                        case "audio":
                            mediaProps.Audio.Add(AudioProps.Create(fileName, track));
                            break;
                        case "subtitle":
                            mediaProps.Subtitle.Add(SubtitleProps.Create(fileName, track));
                            break;
                        default:
                            Log.Warning(
                                "FfMpegToolJsonSchema : Unknown track type : {CodecType} : {FileName}",
                                track.CodecType,
                                fileName
                            );
                            break;
                    }
                }

                // Errors, any unsupported tracks
                mediaProps.HasErrors = mediaProps.Unsupported;

                // Unwanted tags
                mediaProps.HasTags = HasUnwantedTags(ffProbe.Format.Tags);

                // Duration in seconds
                mediaProps.Duration = TimeSpan.FromSeconds(ffProbe.Format.Duration);

                // Container type
                mediaProps.Container = ffProbe.Format.FormatName;

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
}
