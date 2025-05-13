using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;
using InsaneGenius.Utilities;
using Serilog;

// https://ffmpeg.org/ffmpeg.html
// https://trac.ffmpeg.org/wiki/Map
// https://trac.ffmpeg.org/wiki/Encode/H.264
// https://trac.ffmpeg.org/wiki/Encode/H.265

// TODO: When using quickscan select a portion of the middle of the file vs, the beginning.

// Typical commandline:
// ffmpeg [global_options] {[input_file_options] -i input_url} ... {[output_file_options] output_url}

namespace PlexCleaner;

public partial class FfMpeg
{
    public partial class FfMpegTool : MediaTool
    {
        public override ToolFamily GetToolFamily() => ToolFamily.FfMpeg;

        public override ToolType GetToolType() => ToolType.FfMpeg;

        protected override string GetToolNameWindows() => "ffmpeg.exe";

        protected override string GetToolNameLinux() => "ffmpeg";

        protected override string GetSubFolder() => "bin";

        public IGlobalOptions GetFfMpegBuilder() => FfMpegBuilder.Create(GetToolPath());

        public override bool GetInstalledVersion(out MediaToolInfo mediaToolInfo)
        {
            // Get file info
            mediaToolInfo = new MediaToolInfo(this) { FileName = GetToolPath() };
            if (File.Exists(mediaToolInfo.FileName))
            {
                FileInfo fileInfo = new(mediaToolInfo.FileName);
                mediaToolInfo.ModifiedTime = fileInfo.LastWriteTimeUtc;
                mediaToolInfo.Size = fileInfo.Length;
            }

            // Get version info
            Command command = FfMpegBuilder.Version(GetToolPath());
            return Execute(command, out BufferedCommandResult result)
                && result.ExitCode == 0
                && result.StandardError.Length == 0
                && ParseVersion(result.StandardOutput, mediaToolInfo);
        }

        public static bool ParseVersion(string version, MediaToolInfo mediaToolInfo)
        {
            // First line of stderr as version
            // "ffmpeg version 4.3.1-2020-11-19-full_build-www.gyan.dev Copyright (c) 2000-2020 the FFmpeg developers"
            // "ffmpeg version 4.3.1-1ubuntu0~20.04.sav1 Copyright (c) 2000-2020 the FFmpeg developers"
            // "ffprobe version 7.1.1-full_build-www.gyan.dev Copyright (c) 2007-2025 the FFmpeg developers"
            // "ffprobe version 5.1.6-0+deb12u1 Copyright (c) 2007-2024 the FFmpeg developers"
            string[] lines = version.Split(
                Environment.NewLine,
                StringSplitOptions.RemoveEmptyEntries
            );

            // Extract the short version number
            // Match word for ffmpeg or ffprobe
            Match match = InstalledVersionRegex().Match(lines[0]);
            Debug.Assert(match.Success);
            mediaToolInfo.Version = match.Groups["version"].Value;
            Debug.Assert(Version.TryParse(mediaToolInfo.Version, out _));

            return true;
        }

        protected override bool GetLatestVersionWindows(out MediaToolInfo mediaToolInfo)
        {
            // Initialize
            mediaToolInfo = new MediaToolInfo(this);

            try
            {
                // Get the latest release version number from github releases
                // https://github.com/GyanD/codexffmpeg
                const string repo = "GyanD/codexffmpeg";
                mediaToolInfo.Version = GetLatestGitHubRelease(repo);

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
            catch (Exception e)
                when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
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
            if (!FileEx.DeleteDirectory(toolPath, true))
            {
                return false;
            }

            // Build the versioned out folder from the downloaded filename
            // E.g. ffmpeg-3.4-win64-static.zip to .\Tools\FFmpeg\ffmpeg-3.4-win64-static
            extractPath = Tools.CombineToolPath(Path.GetFileNameWithoutExtension(updateFile));

            // Rename the extract folder to the tool folder
            // E.g. ffmpeg-3.4-win64-static to .\Tools\FFMpeg
            return FileEx.RenameFolder(extractPath, toolPath);
        }

        public bool VerifyMedia(string fileName, out string error)
        {
            // Build command line
            error = string.Empty;
            Command command = GetFfMpegBuilder()
                // Exit on error
                .GlobalOptions(options => options.Default().ExitOnError())
                .InputOptions(options =>
                    options
                        .Default()
                        .SeekStop(
                            Program.Options.QuickScan ? Program.QuickScanTimeSpan : TimeSpan.Zero
                        )
                        .InputFile(fileName)
                )
                .OutputOptions(options => options.Default().NullOutput())
                .Build();

            // Execute command
            if (!Execute(command, true, out BufferedCommandResult result))
            {
                return false;
            }
            error = result.StandardError;
            return result.ExitCode == 0 && error.Length == 0;
        }

        public bool ReMuxToMkv(string inputName, string outputName, out string error) =>
            ReMuxToFormat(inputName, outputName, "matroska", out error);

        public bool ReMuxToFormat(
            string inputName,
            string outputName,
            string format,
            out string error
        )
        {
            // Delete output file
            _ = FileEx.DeleteFile(outputName);

            // Build command line
            error = string.Empty;
            Command command = GetFfMpegBuilder()
                .GlobalOptions(options => options.Default())
                .InputOptions(options =>
                    options
                        .Default()
                        .SeekStop(
                            Program.Options.TestSnippets ? Program.SnippetTimeSpan : TimeSpan.Zero
                        )
                        .InputFile(inputName)
                )
                .OutputOptions(options =>
                    options.MapAllCodecCopy().Default().Format(format).OutputFile(outputName)
                )
                .Build();

            // Execute command
            if (!Execute(command, true, out BufferedCommandResult result))
            {
                return false;
            }
            error = result.StandardError;
            return result.ExitCode == 0;
        }

        private static void CreateTrackArgs(
            SelectMediaProps selectMediaProps,
            out string inputMap,
            out string outputMap
        )
        {
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

        public bool ConvertToMkv(
            string inputName,
            SelectMediaProps selectMediaProps,
            string outputName,
            out string error
        )
        {
            if (selectMediaProps == null)
            {
                // No track selection, use default conversion
                return ConvertToMkv(inputName, outputName, out error);
            }

            // Delete output file
            _ = FileEx.DeleteFile(outputName);

            // Create an input and output ignore or copy or convert track map
            // Selected is ReEncode
            // NotSelected is Keep
            CreateTrackArgs(selectMediaProps, out string inputMap, out string outputMap);

            // Build command line
            error = string.Empty;
            Command command = GetFfMpegBuilder()
                .GlobalOptions(options =>
                    options.Default().Add(Program.Config.ConvertOptions.FfMpegOptions.Global)
                )
                .InputOptions(options =>
                    options
                        .Default()
                        .SeekStop(
                            Program.Options.TestSnippets ? Program.SnippetTimeSpan : TimeSpan.Zero
                        )
                        .InputFile(inputName)
                )
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
            if (!Execute(command, true, out BufferedCommandResult result))
            {
                return false;
            }
            error = result.StandardError;
            return result.ExitCode == 0;
        }

        public bool ConvertToMkv(string inputName, string outputName, out string error)
        {
            // Delete output file
            _ = FileEx.DeleteFile(outputName);

            // Build command line
            error = string.Empty;
            Command command = GetFfMpegBuilder()
                .GlobalOptions(options =>
                    options.Default().Add(Program.Config.ConvertOptions.FfMpegOptions.Global)
                )
                .InputOptions(options =>
                    options
                        .Default()
                        .SeekStop(
                            Program.Options.TestSnippets ? Program.SnippetTimeSpan : TimeSpan.Zero
                        )
                        .InputFile(inputName)
                )
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
            if (!Execute(command, true, out BufferedCommandResult result))
            {
                return false;
            }
            error = result.StandardError;
            return result.ExitCode == 0;
        }

        public bool RemoveNalUnits(
            string inputName,
            int nalUnit,
            string outputName,
            out string error
        )
        {
            // Remove SEI NAL units e.g. EIA-608 and CTA-708 content
            // https://ffmpeg.org/ffmpeg-bitstream-filters.html#filter_005funits

            // Delete output file
            _ = FileEx.DeleteFile(outputName);

            // Build command line
            error = string.Empty;
            Command command = GetFfMpegBuilder()
                .GlobalOptions(options => options.Default())
                .InputOptions(options =>
                    options
                        .Default()
                        .SeekStop(
                            Program.Options.TestSnippets ? Program.SnippetTimeSpan : TimeSpan.Zero
                        )
                        .InputFile(inputName)
                )
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
            if (!Execute(command, true, out BufferedCommandResult result))
            {
                return false;
            }
            error = result.StandardError;
            return result.ExitCode == 0;
        }

        public bool RemoveMetadata(string inputName, string outputName, out string error)
        {
            // Delete output file
            _ = FileEx.DeleteFile(outputName);

            // Build command line
            error = string.Empty;
            Command command = GetFfMpegBuilder()
                .GlobalOptions(options => options.Default())
                .InputOptions(options =>
                    options
                        .Default()
                        .SeekStop(
                            Program.Options.TestSnippets ? Program.SnippetTimeSpan : TimeSpan.Zero
                        )
                        .InputFile(inputName)
                )
                .OutputOptions(options =>
                    options
                        .MapMetadata("-1")
                        .MapAllCodecCopy()
                        .Default()
                        .FormatMatroska()
                        .OutputFile(outputName)
                )
                .Build();

            // Execute command
            if (!Execute(command, true, out BufferedCommandResult result))
            {
                return false;
            }
            error = result.StandardError;
            return result.ExitCode == 0;
        }

        public bool GetIdetText(string fileName, out string text)
        {
            // Use Idet to get frame statistics
            // https://ffmpeg.org/ffmpeg-filters.html#idet

            // Build command line
            text = string.Empty;
            Command command = GetFfMpegBuilder()
                // Leave loglevel at default to get idet output, do not use -loglevel error
                // Counting can report stream errors, keep going, do not use -xerror
                .GlobalOptions(options => options.HideBanner().NoStats().AbortOnEmptyOutput())
                .InputOptions(options =>
                    options
                        .Default()
                        .InputFile(fileName)
                        .SeekStop(
                            Program.Options.QuickScan ? Program.QuickScanTimeSpan : TimeSpan.Zero
                        )
                )
                .OutputOptions(options =>
                    options.NoAudio().NoVideo().NoData().VideoFilter("idet").NullOutput()
                )
                .Build();

            // Execute command
            if (!Execute(command, true, out BufferedCommandResult result))
            {
                return false;
            }
            text = result.StandardError;
            return result.ExitCode == 0;
        }

        public const string DefaultVideoOptions = "libx264 -crf 22 -preset medium";
        public const string DefaultAudioOptions = "ac3";

        private const string InstalledVersionPattern = @"version\D+(?<version>([0-9]+(\.[0-9]+)+))";

        [GeneratedRegex(InstalledVersionPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline)]
        public static partial Regex InstalledVersionRegex();

        public static int GetNalUnit(string format) =>
            // Get SEI NAL unit based on video format
            // H264 = 6, H265 = 9, MPEG2 = 178
            // Return default(int) if not found
            s_sEINalUnitList
                .FirstOrDefault(item =>
                    item.format.Equals(format, StringComparison.OrdinalIgnoreCase)
                )
                .nalunit;

        // Common format tags
        private const string H264Format = "h264";
        private const string H265Format = "h265";
        private const string MPEG2Format = "mpeg2video";

        // SEI NAL units for EIA-608 and CTA-708 content
        private static readonly List<(string format, int nalunit)> s_sEINalUnitList =
        [
            (H264Format, 6),
            (H265Format, 39),
            (MPEG2Format, 178),
        ];
    }
}
