using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;

// https://ffmpeg.org/ffmpeg.html

// ffmpeg [global_options] {[input_file_options] -i input_url} ... {[output_file_options] output_url}

// TODO: When using quickscan select a portion of the middle of the file

namespace PlexCleaner;

public partial class FfMpeg
{
    public const string DefaultVideoOptions = "libx264 -crf 22 -preset medium";
    public const string DefaultAudioOptions = "ac3";

    // Common format tags
    private const string H264Format = "h264";
    private const string H265Format = "hevc";
    private const string MPEG2Format = "mpeg2video";

    // SEI NAL units for EIA-608 and CTA-708 content
    private static readonly List<(string format, int nalunit)> s_sEINalUnitList =
    [
        (H264Format, 6),
        (H265Format, 39),
        (MPEG2Format, 178),
    ];

    public static int GetNalUnit(string format) =>
        // Get SEI NAL unit based on video format
        // H264 = 6, H265/HEVC = 39, MPEG2 = 178
        // Return default(int) if not found
        s_sEINalUnitList
            .FirstOrDefault(item => item.format.Equals(format, StringComparison.OrdinalIgnoreCase))
            .nalunit;

    public partial class Tool : MediaTool
    {
        public override ToolFamily GetToolFamily() => ToolFamily.FfMpeg;

        public override ToolType GetToolType() => ToolType.FfMpeg;

        protected override string GetToolNameWindows() => "ffmpeg.exe";

        protected override string GetToolNameLinux() => "ffmpeg";

        protected override string GetSubFolder() => "bin";

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

            // "ffmpeg version 4.3.1-2020-11-19-full_build-www.gyan.dev Copyright (c) 2000-2020 the FFmpeg developers"
            // "ffmpeg version 4.3.1-1ubuntu0~20.04.sav1 Copyright (c) 2000-2020 the FFmpeg developers"
            // "ffprobe version 7.1.1-full_build-www.gyan.dev Copyright (c) 2007-2025 the FFmpeg developers"
            // "ffprobe version 5.1.6-0+deb12u1 Copyright (c) 2007-2024 the FFmpeg developers"

            // Parse version
            string[] lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Match match = InstalledVersionRegex().Match(lines[0]);
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
                // https://github.com/GyanD/codexffmpeg
                const string repo = "GyanD/codexffmpeg";
                if (!GetLatestGitHubRelease(repo, out string version))
                {
                    return false;
                }
                mediaToolInfo.Version = version;

                // Create the filename using the version number
                // ffmpeg-6.0-full_build.7z
                mediaToolInfo.FileName = $"ffmpeg-{mediaToolInfo.Version}-full_build.7z";

                // Get the GitHub download Uri
                mediaToolInfo.Url = GitHubRelease.GetDownloadUri(
                    repo,
                    mediaToolInfo.Version,
                    mediaToolInfo.FileName
                );
            }
            catch (Exception e) when (Log.Logger.LogAndHandle(e))
            {
                return false;
            }
            return true;
        }

        public override bool Update(string updateFile)
        {
            // FfMpeg archives have versioned folders in the zip file
            // The 7Zip -spe option does not work for zip files
            // https://sourceforge.net/p/sevenzip/discussion/45798/thread/8cb61347/
            // We need to extract to the root tools folder, that will create a subdir, then rename to the destination folder
            string extractPath = Tools.GetToolsRoot();

            // Extract the update file
            Log.Information("Extracting {UpdateFile} ...", updateFile);
            if (!Tools.SevenZip.UnZip(updateFile, extractPath))
            {
                return false;
            }

            // Delete the tool destination directory
            string toolPath = GetToolFolder();
            if (Directory.Exists(toolPath))
            {
                Directory.Delete(toolPath, true);
            }

            // Build the versioned out folder from the downloaded filename
            // E.g. ffmpeg-3.4-win64-static.zip to .\Tools\FFmpeg\ffmpeg-3.4-win64-static
            extractPath = Tools.CombineToolPath(Path.GetFileNameWithoutExtension(updateFile));

            // Rename the extract folder to the tool folder
            // E.g. ffmpeg-3.4-win64-static to .\Tools\FFMpeg
            Directory.Move(extractPath, toolPath);

            return true;
        }

        public VerifyResult VerifyMedia(string fileName)
        {
            // Build command line
            Command command = GetBuilder()
                // Exit on error
                .GlobalOptions(options => options.Default().ExitOnError())
                .InputOptions(options => options.Default().QuickScan().InputFile(fileName))
                .OutputOptions(options => options.Default().NullOutput())
                .Build();

            // Execute command: ffmpeg can exit 0 yet report stream errors on stderr
            // Classify stderr line by line as it streams to keep memory bounded, e.g. non-monotonic-DTS file emits a warning per packet
            VerifyClassifier.Accumulator classifier = new();
            if (!ExecuteStreamStdErr(command, classifier.Add, out int exitCode))
            {
                // Process could not run
                return VerifyResult.DecodeError;
            }

            // A non-zero exit is always a failure, fail closed even if stderr shows only the timestamp warning
            VerifyResult verifyResult = classifier.Result;
            if (exitCode != 0)
            {
                verifyResult = VerifyResult.DecodeError;
            }
            if (verifyResult == VerifyResult.DecodeError)
            {
                // Log the unique error lines, a silent non-zero exit has none so omit the empty field.
                // Include the operation and file name to match the tool-failure logging convention
                // (see MediaTool.LogFailedResult); VerifyMedia streams stderr so it logs inline rather
                // than through that helper.
                string error = CleanForLog(string.Join(" | ", classifier.Errors));
                if (string.IsNullOrEmpty(error))
                {
                    Log.Error(
                        "Failed execution of {ToolType} : {Operation:l} : ExitCode: {ExitCode} : {FileName}",
                        GetToolType(),
                        nameof(VerifyMedia),
                        exitCode,
                        fileName
                    );
                }
                else
                {
                    Log.Error(
                        "Failed execution of {ToolType} : {Operation:l} : ExitCode: {ExitCode} : {Error} : {FileName}",
                        GetToolType(),
                        nameof(VerifyMedia),
                        exitCode,
                        error,
                        fileName
                    );
                }
            }
            return verifyResult;
        }

        public bool ReMuxToMkv(string inputName, string outputName) =>
            ReMuxToFormat(inputName, outputName, "matroska");

        public bool ReMuxToFormat(string inputName, string outputName, string format)
        {
            // Delete output file
            File.Delete(outputName);

            // Build command line
            Command command = GetBuilder()
                .GlobalOptions(options => options.Default())
                .InputOptions(options => options.Default().InputFile(inputName))
                .OutputOptions(options =>
                    options.MapAllCodecCopy().Default().Format(format).OutputFile(outputName)
                )
                .Build();

            // Execute command
            return Execute(command, true, true, out BufferedCommandResult result)
                && (result.ExitCode == 0 || LogFailedResult(result, inputName));
        }

        private static void CreateTrackArgs(
            SelectMediaProps selectMediaProps,
            out string inputMap,
            out string outputMap
        )
        {
            // TODO: Add to builder

            // Verify correct media type
            Debug.Assert(selectMediaProps.Selected.Parser == ToolType.FfProbe);
            Debug.Assert(selectMediaProps.NotSelected.Parser == ToolType.FfProbe);

            // Create a list of all the tracks ordered by Id
            // Selected is ReEncode, state is expected to be ReEncode
            // NotSelected is Keep, state is expected to be Keep
            List<TrackProps> trackList = selectMediaProps.GetTrackList();

            // Create the input map using all tracks referenced by id
            // https://trac.ffmpeg.org/wiki/Map
            StringBuilder sb = new();
            trackList.ForEach(item =>
                sb.Append(CultureInfo.InvariantCulture, $"-map 0:{item.Id} ")
            );
            inputMap = sb.ToString();
            inputMap = inputMap.Trim();

            // Create the output map using the same order as the input map
            // Output tracks are referenced by the track type relative index
            // http://ffmpeg.org/ffmpeg.html#Stream-specifiers
            _ = sb.Clear();
            int videoIndex = 0;
            int audioIndex = 0;
            int subtitleIndex = 0;
            foreach (TrackProps trackProps in trackList)
            {
                // Copy or encode
                _ = trackProps switch
                {
                    VideoProps videoProps => sb.Append(
                        videoProps.State == TrackProps.StateType.Keep
                            ? $"-c:v:{videoIndex++} copy "
                            : $"-c:v:{videoIndex++} {Program.Config.ConvertOptions.FfMpegOptions.Video} "
                    ),
                    AudioProps audioProps => sb.Append(
                        audioProps.State == TrackProps.StateType.Keep
                            ? $"-c:a:{audioIndex++} copy "
                            : $"-c:a:{audioIndex++} {Program.Config.ConvertOptions.FfMpegOptions.Audio} "
                    ),
                    SubtitleProps => sb.Append(
                        CultureInfo.InvariantCulture,
                        $"-c:s:{subtitleIndex++} copy "
                    ),
                    _ => throw new NotImplementedException(),
                };
            }
            outputMap = sb.ToString();
            outputMap = outputMap.Trim();
        }

        // Parse an ffmpeg -progress line to a fraction, or null. out_time_us and out_time_ms are microseconds.
        internal static double? ParseProgressFraction(string line, long durationUs)
        {
            int separator = line.IndexOf('=');
            if (separator <= 0)
            {
                return null;
            }
            string value = line[(separator + 1)..];
            return line[..separator] switch
            {
                "progress" when value == "end" => 1.0,
                "out_time_us"
                or "out_time_ms"
                    when durationUs > 0
                        && long.TryParse(value, CultureInfo.InvariantCulture, out long microseconds)
                        && microseconds > 0 => (double)microseconds / durationUs,
                _ => null,
            };
        }

        private bool ExecuteEncodeWithProgress(Command command, string inputName)
        {
            Metrics.FileSink? sink = Metrics.CurrentFileSink;
            return ExecuteStreamStdOut(
                    command,
                    line =>
                    {
                        double? fraction = ParseProgressFraction(line, sink?.DurationUs ?? 0);
                        if (fraction.HasValue)
                        {
                            Metrics.ReportFileFraction(sink, fraction.Value);
                        }
                    },
                    out int exitCode,
                    out string standardError
                ) && (exitCode == 0 || LogFailedResult(exitCode, standardError, inputName));
        }

        public bool ConvertToMkv(
            string inputName,
            SelectMediaProps? selectMediaProps,
            string outputName
        )
        {
            if (selectMediaProps == null)
            {
                // No track selection, use default conversion
                return ConvertToMkv(inputName, outputName);
            }

            // Delete output file
            File.Delete(outputName);

            // Create an input and output ignore or copy or convert track map
            // Selected is ReEncode
            // NotSelected is Keep
            CreateTrackArgs(selectMediaProps, out string inputMap, out string outputMap);

            // Build command line
            Command command = GetBuilder()
                .GlobalOptions(options =>
                    options
                        .Default()
                        .Progress()
                        .Add(Program.Config.ConvertOptions.FfMpegOptions.Global)
                )
                .InputOptions(options => options.Default().TestSnippets().InputFile(inputName))
                .OutputOptions(options =>
                    options
                        .Add(inputMap)
                        .Add(outputMap)
                        .Default()
                        .FormatMatroska()
                        .OutputFile(outputName)
                )
                .Build();

            // Execute command
            return ExecuteEncodeWithProgress(command, inputName);
        }

        public bool ConvertToMkv(string inputName, string outputName)
        {
            // Delete output file
            File.Delete(outputName);

            // Build command line
            Command command = GetBuilder()
                .GlobalOptions(options =>
                    options
                        .Default()
                        .Progress()
                        .Add(Program.Config.ConvertOptions.FfMpegOptions.Global)
                )
                .InputOptions(options => options.Default().TestSnippets().InputFile(inputName))
                .OutputOptions(options =>
                    options
                        .MapAll()
                        .CodecVideo(Program.Config.ConvertOptions.FfMpegOptions.Video)
                        .CodecAudio(Program.Config.ConvertOptions.FfMpegOptions.Audio)
                        .CodecSubtitle("copy")
                        .Default()
                        .FormatMatroska()
                        .OutputFile(outputName)
                )
                .Build();

            // Execute command
            return ExecuteEncodeWithProgress(command, inputName);
        }

        public bool SetTimestamps(string inputName, string outputName)
        {
            // Losslessly rewrite audio packet timestamps to be strictly monotonic using the setts bitstream filter
            // https://ffmpeg.org/ffmpeg-bitstream-filters.html#setts
            // Audio only, the expression forces PTS monotonic which is safe where PTS equals DTS, applying
            // it to video would reorder B-frames, a video-only DTS break instead fails the caller's re-verify

            // Delete output file
            File.Delete(outputName);

            // Build command line, the escaped comma separates the setts option arguments
            // No TestSnippets: this lossless stream-copy repair must produce the full file so the
            // byte-identical gate and re-verify validate the whole file, not an unrepresentative snippet
            Command command = GetBuilder()
                .GlobalOptions(options => options.Default())
                .InputOptions(options => options.Default().InputFile(inputName))
                .OutputOptions(options =>
                    options
                        .MapAllCodecCopy()
                        .BitstreamFilterAudio(
                            "\"setts=pts=max(PTS\\,PREV_OUTPTS+1):dts=max(DTS\\,PREV_OUTDTS+1)\""
                        )
                        .Default()
                        .FormatMatroska()
                        .OutputFile(outputName)
                )
                .Build();

            // Execute command
            return Execute(command, true, true, out BufferedCommandResult result)
                && (result.ExitCode == 0 || LogFailedResult(result, inputName));
        }

        public bool GetStreamHashes(string fileName, out Dictionary<int, string> streamHashes)
        {
            // Streamhash muxer hashes payload only, not timestamps, so an identical hash proves setts changed timestamps and nothing else
            streamHashes = [];

            // Build command line, stream copy to the streamhash muxer written to stdout
            Command command = GetBuilder()
                .GlobalOptions(options => options.Default())
                .InputOptions(options => options.Default().InputFile(fileName))
                .OutputOptions(options =>
                    options.MapAllCodecCopy().Format("streamhash").Add("-hash").Add("md5").Add("-")
                )
                .Build();

            // Execute command, capturing full stdout
            if (!Execute(command, false, true, out BufferedCommandResult result))
            {
                return false;
            }
            if (result.ExitCode != 0)
            {
                return LogFailedResult(result, fileName);
            }

            // Parse lines of the form "index,type,md5=value"
            foreach (
                string line in result.StandardOutput.Split(
                    '\n',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                )
            )
            {
                string[] parts = line.Split(',', 3);
                if (
                    parts.Length == 3
                    && int.TryParse(parts[0], out int index)
                    && !streamHashes.ContainsKey(index)
                )
                {
                    // Keep type and hash so a stream reorder or payload change is detected
                    streamHashes[index] = $"{parts[1]},{parts[2]}";
                }
            }
            return streamHashes.Count > 0;
        }

        public bool RemoveNalUnits(string inputName, int nalUnit, string outputName)
        {
            // Remove SEI NAL units e.g. EIA-608 and CTA-708 content
            // https://ffmpeg.org/ffmpeg-bitstream-filters.html#filter_005funits

            // Delete output file
            File.Delete(outputName);

            // Build command line
            Command command = GetBuilder()
                .GlobalOptions(options => options.Default())
                .InputOptions(options => options.Default().InputFile(inputName))
                .OutputOptions(options =>
                    options
                        .MapAllCodecCopy()
                        .BitstreamFilterVideo($"\"filter_units=remove_types={nalUnit}\"")
                        .Default()
                        .FormatMatroska()
                        .OutputFile(outputName)
                )
                .Build();

            // Execute command
            return Execute(command, true, true, out BufferedCommandResult result)
                && (result.ExitCode == 0 || LogFailedResult(result, inputName));
        }

        public bool GetIdetText(string fileName, out string text)
        {
            // Use Idet to get frame statistics
            // https://ffmpeg.org/ffmpeg-filters.html#idet

            // Build command line
            text = string.Empty;
            Command command = GetBuilder()
                // Leave loglevel at default to get idet output, do not use -loglevel error
                // Counting can report stream errors, keep going, do not use -xerror
                .GlobalOptions(options => options.HideBanner().NoStats().AbortOnEmptyOutput())
                .InputOptions(options => options.Default().InputFile(fileName).QuickScan())
                .OutputOptions(options =>
                    options.NoAudio().NoSubtitles().NoData().VideoFilter("idet").NullOutput()
                )
                .Build();

            // Execute command
            if (!Execute(command, true, true, out BufferedCommandResult result))
            {
                return false;
            }
            text = result.StandardError.Trim();
            return result.ExitCode == 0 || LogFailedResult(result, fileName);
        }

        [GeneratedRegex(
            @"version\D+(?<version>([0-9]+(\.[0-9]+)+))",
            RegexOptions.IgnoreCase | RegexOptions.Multiline
        )]
        public static partial Regex InstalledVersionRegex();
    }
}
