using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Stream;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;
using InsaneGenius.Utilities;
using Serilog;

// https://ffmpeg.org/ffprobe.html

// ffprobe [options] input_url

namespace PlexCleaner;

public partial class FfProbe
{
    public class Tool : MediaTool
    {
        public override ToolFamily GetToolFamily() => ToolFamily.FfMpeg;

        public override ToolType GetToolType() => ToolType.FfProbe;

        protected override string GetToolNameWindows() => "ffprobe.exe";

        protected override string GetToolNameLinux() => "ffprobe";

        protected override string GetSubFolder() => "bin";

        public IGlobalOptions GetBuilder() => Builder.Create(GetToolPath());

        public override bool GetInstalledVersion(out MediaToolInfo mediaToolInfo)
        {
            // Get version info
            mediaToolInfo = new MediaToolInfo(this) { FileName = GetToolPath() };
            Command command = Builder.Version(GetToolPath());
            return Execute(command, out BufferedCommandResult result)
                && result.ExitCode == 0
                && FfMpeg.Tool.GetVersion(result.StandardOutput, mediaToolInfo);
        }

        protected override bool GetLatestVersionWindows(out MediaToolInfo mediaToolInfo) =>
            throw new NotImplementedException();

        public bool GetPackets(
            Command command,
            Func<FfMpegToolJsonSchema.Packet, bool> packetFunc,
            out string error
        )
        {
            // Wrap async function in a task
            (bool result, string error) result = GetPacketsAsync(
                    command,
                    async (packet) => await Task.FromResult(packetFunc(packet))
                )
                .GetAwaiter()
                .GetResult();
            error = result.error;
            return result.result;
        }

        public async Task<(bool result, string error)> GetPacketsAsync(
            Command command,
            Func<FfMpegToolJsonSchema.Packet, Task<bool>> packetFunc
        )
        {
            int processId = -1;
            try
            {
                // Pipe target to deserialize JSON packets
                List<FfMpegToolJsonSchema.Packet> packetList = [];
                PipeTarget stdOutTarget = PipeTarget.Create(
                    async (stream, cancellationToken) =>
                    {
                        // Read the stream
                        Utf8JsonAsyncStreamReader jsonStreamReader = new(stream);
                        while (await jsonStreamReader.ReadAsync(cancellationToken))
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                return;
                            }

                            // Read until packets property
                            if (
                                jsonStreamReader.TokenType == JsonTokenType.PropertyName
                                && jsonStreamReader
                                    .GetString()
                                    .Equals("packets", StringComparison.OrdinalIgnoreCase)
                            )
                            {
                                // Expect array start
                                if (
                                    !await jsonStreamReader.ReadAsync(cancellationToken)
                                    || jsonStreamReader.TokenType != JsonTokenType.StartArray
                                )
                                {
                                    // Unexpected
                                    break;
                                }

                                // Read the packet objects
                                while (await jsonStreamReader.ReadAsync(cancellationToken))
                                {
                                    if (cancellationToken.IsCancellationRequested)
                                    {
                                        return;
                                    }

                                    // Expect object start
                                    if (jsonStreamReader.TokenType != JsonTokenType.StartObject)
                                    {
                                        // Or end of array if empty array
                                        if (jsonStreamReader.TokenType != JsonTokenType.EndArray)
                                        {
                                            break;
                                        }

                                        // Unexpected token
                                        break;
                                    }

                                    // Send packet to delegate
                                    // A false returns means delegate does not want any more packets
                                    if (
                                        !await packetFunc(
                                            await jsonStreamReader.DeserializeAsync<FfMpegToolJsonSchema.Packet>(
                                                ConfigFileJsonSchema.JsonReadOptions,
                                                cancellationToken
                                            )
                                        )
                                    )
                                    {
                                        // Done
                                        break;
                                    }
                                }

                                // Done
                                break;
                            }
                        }
                    }
                );

                // Pipe target to capture standard error
                StringBuilder stdErrBuilder = new();
                PipeTarget stdErrTarget = ToStringSummary(stdErrBuilder);

                // Setup redirection
                CommandTask<CommandResult> task = command
                    .WithStandardOutputPipe(stdOutTarget)
                    .WithStandardErrorPipe(stdErrTarget)
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteAsync(CancellationToken.None, Program.CancelToken());
                processId = task.ProcessId;
                Log.Information(
                    "Executing {ToolType} : ProcessId: {ProcessId}, Arguments: {Arguments}",
                    GetToolType(),
                    processId,
                    command.Arguments
                );

                // Execute command
                CommandResult result = await task;
                return (result.ExitCode == 0, stdErrBuilder.ToString().Trim());
            }
            catch (OperationCanceledException)
            {
                Log.Error(
                    "Cancelled execution of {ToolType} : ProcessId: {ProcessId}, Arguments: {Arguments}",
                    GetToolType(),
                    processId,
                    command.Arguments
                );
                return (false, string.Empty);
            }
            catch (Exception e)
                when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
            {
                return (false, string.Empty);
            }
        }

        public bool GetSubCcPackets(
            string fileName,
            Func<FfMpegToolJsonSchema.Packet, bool> packetFunc
        )
        {
            // Quickscan
            // -t and read_intervals do not work with the subcc filter
            // https://superuser.com/questions/1893673/how-to-time-limit-the-input-stream-duration-when-using-movie-filenameout0subcc
            // ReMux using FFmpeg to a snippet file then scan the snippet file
            Command command;
            if (Program.Options.QuickScan)
            {
                // Keep in sync with FfMpegTool.ReMuxToFormat()

                // Create a temp filename based on the input name
                string tempName = Path.ChangeExtension(fileName, ".tmp13");
                Debug.Assert(fileName != tempName);
                _ = FileEx.DeleteFile(tempName);

                // Use Matroska for snippet format as it supports more stream formats
                // E.g. DVCPRO video streams can be muxed into MKV but not into TS
                // [mpegts @ 000001543cf744c0] Stream 0, codec dvvideo, is muxed as a private data stream and may not be recognized upon reading.

                // Build command line
                command = Tools
                    .FfMpeg.GetBuilder()
                    .GlobalOptions(options => options.Default())
                    .InputOptions(options =>
                        options.Default().SeekStop(Program.QuickScanTimeSpan).InputFile(fileName)
                    )
                    .OutputOptions(options =>
                        options.MapAllCodecCopy().Default().FormatMatroska().OutputFile(tempName)
                    )
                    .Build();

                // Execute command
                Log.Information("Creating temp media file : {TempFileName}", tempName);
                if (!Tools.FfMpeg.Execute(command, true, true, out BufferedCommandResult result))
                {
                    Log.Error("Failed to create temp media file : {TempFileName}", tempName);
                    Log.Error("{Error}", result.StandardError.Trim());
                    _ = FileEx.DeleteFile(tempName);
                    return false;
                }

                // Use the temp file as the input file
                fileName = tempName;
            }

            // Build command line
            // Get packet info using subcc filter
            // https://www.ffmpeg.org/ffmpeg-devices.html#Options-10
            command = GetBuilder()
                .GlobalOptions(options => options.Default().HideBanner().LogLevelError())
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
            bool ret = GetPackets(command, packetFunc, out string error);
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

        public bool GetBitratePackets(
            string fileName,
            Func<FfMpegToolJsonSchema.Packet, bool> packetFunc
        )
        {
            // Build command line
            Command command = GetBuilder()
                .GlobalOptions(options => options.Default().HideBanner().LogLevelError())
                .FfProbeOptions(options =>
                    options.QuickScan().ShowPackets().OutputFormatJson().InputFile(fileName)
                )
                .Build();

            // Get packet list
            Log.Information("Getting bitrate packets : {FileName}", fileName);
            if (!GetPackets(command, packetFunc, out string error))
            {
                Log.Error("Failed to get bitrate packets : {FileName}", fileName);
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
            Command command = GetBuilder()
                .GlobalOptions(options => options.Default().HideBanner().LogLevelError())
                .FfProbeOptions(options =>
                    options.ShowStreams().ShowFormat().OutputFormatJson().InputFile(fileName)
                )
                .Build();

            // Execute command
            Log.Information(
                "{ToolType} : Getting media info : {FileName}",
                GetToolType(),
                fileName
            );
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

            // Get JSON output
            json = result.StandardOutput;
            return true;
        }

        public static bool GetMediaPropsFromJson(
            string json,
            string fileName,
            out MediaProps mediaProps
        )
        {
            // Populate the MediaProps object from the JSON string
            mediaProps = new MediaProps(ToolType.FfProbe);
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
