using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Stream;
using CliWrap;
using CliWrap.Buffered;
using Serilog;

// https://ffmpeg.org/ffprobe.html

// ffprobe [options] input_url

namespace PlexCleaner;

public partial class FfProbe
{
    public class Tool : MediaTool
    {
        // "Undesirable" tags
        private static readonly List<string> s_undesirableTags = ["statistics"];

        // Skip empty stderr, e.g. a cancelled process, to avoid logging an empty "" line
        internal static void LogErrorOutput(string error)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                Log.Error("{Error}", error);
            }
        }

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
                    async packet => await Task.FromResult(packetFunc(packet))
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
                PipeTarget stdOutTarget = PipeTarget.Create(
                    async (stream, cancellationToken) =>
                    {
                        // Read the stream
                        using Utf8JsonAsyncStreamReader jsonStreamReader = new(stream);
                        ArgumentNullException.ThrowIfNull(jsonStreamReader);
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
                                    .GetString()!
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
                                    FfMpegToolJsonSchema.Packet? packet =
                                        await jsonStreamReader.DeserializeAsync<FfMpegToolJsonSchema.Packet>(
                                            JsonReadOptions,
                                            cancellationToken
                                        );

                                    if (packet == null || !await packetFunc(packet))
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
                Log.Debug(
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
            catch (Exception e) when (Log.Logger.LogAndHandle(e))
            {
                return (false, string.Empty);
            }
        }

        public bool GetClosedCaptions(string fileName, out bool hasClosedCaptions)
        {
            // Detect EIA-608/CTA-708 closed captions embedded in the video stream
            // analyze_frames decodes frames to surface the stream-level closed_captions flag
            hasClosedCaptions = false;
            Command command = GetBuilder()
                .GlobalOptions(options => options.Default().HideBanner().LogLevelError())
                .FfProbeOptions(options =>
                    options
                        .SelectStreams("v:0")
                        .AnalyzeFrames()
                        .ReadIntervalFrames(
                            Program.Options.QuickScan ? Program.QuickScanFrameCount : 0
                        )
                        .ShowEntries("stream=closed_captions")
                        .OutputFormatJson()
                        .InputFile(fileName)
                )
                .Build();

            // Execute command
            Log.Debug("Getting closed caption info : {FileName}", fileName);
            if (!Execute(command, false, true, out BufferedCommandResult result))
            {
                return false;
            }
            if (result.ExitCode != 0)
            {
                Log.Error("Failed to get closed caption info : {FileName}", fileName);
                return LogFailedResult(result);
            }

            // Any video stream reporting closed captions, FromJson throws on malformed output
            try
            {
                FfMpegToolJsonSchema.ClosedCaptionsProbe probe =
                    FfMpegToolJsonSchema.ClosedCaptionsProbe.FromJson(result.StandardOutput);
                hasClosedCaptions = probe.Streams.Any(stream => stream.ClosedCaptions != 0);
            }
            catch (Exception e) when (Log.Logger.LogAndHandle(e))
            {
                return false;
            }
            return true;
        }

        public bool GetStreamTimings(
            string fileName,
            out Dictionary<int, (double Start, double Duration)> timings
        )
        {
            // Per-stream start and duration, used to verify a timestamp repair preserved A/V sync
            timings = [];
            Command command = GetBuilder()
                .GlobalOptions(options => options.Default().HideBanner().LogLevelError())
                .FfProbeOptions(options =>
                    options
                        .ShowStreams()
                        .ShowEntries("stream=index,start_time,duration")
                        .OutputFormatJson()
                        .InputFile(fileName)
                )
                .Build();

            // Execute command
            Log.Debug("Getting stream timings : {FileName}", fileName);
            if (!Execute(command, false, true, out BufferedCommandResult result))
            {
                return false;
            }
            if (result.ExitCode != 0)
            {
                Log.Error("Failed to get stream timings : {FileName}", fileName);
                return LogFailedResult(result);
            }

            // FromJson throws on malformed output
            try
            {
                FfMpegToolJsonSchema.StreamTimingsProbe probe =
                    FfMpegToolJsonSchema.StreamTimingsProbe.FromJson(result.StandardOutput);
                foreach (FfMpegToolJsonSchema.StreamTiming stream in probe.Streams)
                {
                    timings[stream.Index] = (stream.StartTime, stream.Duration);
                }
            }
            catch (Exception e) when (Log.Logger.LogAndHandle(e))
            {
                return false;
            }
            return true;
        }

        public bool GetAnalysisPackets(
            string fileName,
            Func<FfMpegToolJsonSchema.Packet, bool> packetFunc,
            bool quickScan
        )
        {
            // Build command line
            Command command = GetBuilder()
                .GlobalOptions(options => options.Default().HideBanner().LogLevelError())
                .FfProbeOptions(options =>
                    options
                        .SeekStop(quickScan ? Program.QuickScanTimeSpan : TimeSpan.Zero)
                        .ShowPackets()
                        .OutputFormatJson()
                        .InputFile(fileName)
                )
                .Build();

            // Get packet list
            Log.Debug("Getting analysis packets : {FileName}", fileName);
            if (!GetPackets(command, packetFunc, out string error))
            {
                Log.Error("Failed to get analysis packets : {FileName}", fileName);
                LogErrorOutput(error);
                return false;
            }
            return true;
        }

        public bool GetMediaProps(string fileName, out MediaProps mediaProps)
        {
            mediaProps = null!;
            return GetMediaPropsJson(fileName, out string json)
                && GetMediaPropsFromJson(json, fileName, out mediaProps);
        }

        public bool GetMediaPropsJson(string fileName, out string json)
        {
            // Do not use analyze_frames, it would add closed_captions, film_grain, nb_read_frames but forces full decode of every stream

            // Build command line
            json = string.Empty;
            Command command = GetBuilder()
                .GlobalOptions(options => options.Default().HideBanner().LogLevelError())
                .FfProbeOptions(options =>
                    options.ShowStreams().ShowFormat().OutputFormatJson().InputFile(fileName)
                )
                .Build();

            // Execute command
            Log.Debug("{ToolType} : Getting media info : {FileName}", GetToolType(), fileName);
            if (!Execute(command, false, true, out BufferedCommandResult result))
            {
                return false;
            }
            if (result.ExitCode != 0)
            {
                Log.Error(
                    "{ToolType} : Failed to get media info : {FileName}",
                    GetToolType(),
                    fileName
                );
                return LogFailedResult(result);
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
            mediaProps = new MediaProps(ToolType.FfProbe, fileName);
            try
            {
                // Deserialize
                FfMpegToolJsonSchema.FfProbe ffProbe = FfMpegToolJsonSchema.FfProbe.FromJson(json);
                if (ffProbe.Tracks.Count == 0)
                {
                    Log.Error(
                        "{ToolType} : Container not supported : Container: {Container}, Tracks: {Tracks} : {FileName}",
                        mediaProps.Parser,
                        ffProbe.Format.FormatName,
                        ffProbe.Tracks.Count,
                        fileName
                    );
                    return false;
                }

                // Container type
                mediaProps.Container = ffProbe.Format.FormatName;

                // Tracks
                foreach (FfMpegToolJsonSchema.Track track in ffProbe.Tracks)
                {
                    // Process by track type
                    switch (track.CodecType.ToLowerInvariant())
                    {
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
                        case "subtitle":
                            SubtitleProps subtitleProps = new(mediaProps);
                            if (subtitleProps.Create(track))
                            {
                                mediaProps.Subtitle.Add(subtitleProps);
                            }
                            break;
                        default:
                            Log.Warning(
                                "{Parser} : Unknown track type : {CodecType} : {FileName}",
                                mediaProps.Parser,
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

                // TODO: Chapters
                // TODO: Attachments
            }
            catch (Exception e) when (Log.Logger.LogAndHandle(e))
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
    }

    public static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        AllowTrailingCommas = true,
        IncludeFields = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        ReadCommentHandling = JsonCommentHandling.Skip,
        TypeInfoResolver = FfMpegToolJsonContext.Default,
    };
}
