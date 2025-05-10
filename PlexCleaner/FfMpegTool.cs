using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using InsaneGenius.Utilities;
using Serilog;

// https://ffmpeg.org/ffmpeg.html
// https://trac.ffmpeg.org/wiki/Map
// https://trac.ffmpeg.org/wiki/Encode/H.264
// https://trac.ffmpeg.org/wiki/Encode/H.265

// FfMpeg logs to stderr, not stdout

// TODO: When using quickscan select a portion of the middle of the file vs, the beginning.

// Typical commandline:
// ffmpeg [global_options] {[input_file_options] -i input_url} ... {[output_file_options] output_url}

namespace PlexCleaner;

public partial class FfMpegTool : MediaTool
{
    public override ToolFamily GetToolFamily() => ToolFamily.FfMpeg;

    public override ToolType GetToolType() => ToolType.FfMpeg;

    protected override string GetToolNameWindows() => "ffmpeg.exe";

    protected override string GetToolNameLinux() => "ffmpeg";

    public override bool GetInstalledVersion(out MediaToolInfo mediaToolInfo)
    {
        // Initialize
        mediaToolInfo = new MediaToolInfo(this);

        // Get version
        const string commandline = "-version";
        int exitCode = Command(commandline, out string output, out string error);
        if (exitCode != 0 || error.Length > 0)
        {
            return false;
        }

        // First line of stderr as version
        // Windows : "ffmpeg version 4.3.1-2020-11-19-full_build-www.gyan.dev Copyright (c) 2000-2020 the FFmpeg developers"
        // Ubuntu: "ffmpeg version 4.3.1-1ubuntu0~20.04.sav1 Copyright (c) 2000-2020 the FFmpeg developers"
        // Arch: "ffmpeg version n6.0 Copyright (c) 2000-2023 the FFmpeg developers"
        string[] lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        // Extract the short version number
        // Match word for ffmpeg or ffprobe
        Match match = InstalledVersionRegex().Match(lines[0]);
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

    protected override string GetSubFolder() => "bin";

    public static string GetStartStopSplit(TimeSpan timeStart, TimeSpan timeEnd) =>
        $"-ss {(int)timeStart.TotalSeconds} -t {(int)timeEnd.TotalSeconds}";

    public static string GetStartSplit(TimeSpan timeSpan) => $"-ss {(int)timeSpan.TotalSeconds}";

    public static string GetStopSplit(TimeSpan timeSpan) => $"-t {(int)timeSpan.TotalSeconds}";

    public bool VerifyMedia(string fileName) => VerifyMedia(fileName, out _);

    public bool VerifyMedia(string fileName, out string error)
    {
        // Default options
        StringBuilder commandline = new();
        _ = commandline.Append($"{GlobalOptions} ");

        // Quiet
        _ = commandline.Append($"{SilentOptions} ");

        // Quickscan
        if (Program.Options.QuickScan)
        {
            _ = commandline.Append(
                CultureInfo.InvariantCulture,
                $"{GetStopSplit(Program.QuickScanTimeSpan)} "
            );
        }

        // Input filename
        _ = commandline.Append(CultureInfo.InvariantCulture, $"-i \"{fileName}\" ");

        // Default output options
        _ = commandline.Append($"{OutputOptions} ");

        // Null output muxer and exit immediately on error
        // https://trac.ffmpeg.org/wiki/Null
        _ = commandline.Append("-loglevel error -xerror -f null -");

        // Execute and limit captured output to last 5 lines
        int exitCode = Command(commandline.ToString(), 5, out _, out error);

        // Test exitCode and stderr errors
        return exitCode == 0 && error.Length == 0;
    }

    public bool ReMuxToMkv(string inputName, string outputName, out string error) =>
        ReMuxToFormat(inputName, outputName, "matroska", out error);

    public bool ReMuxToFormat(string inputName, string outputName, string format, out string error)
    {
        // Delete output file
        _ = FileEx.DeleteFile(outputName);

        // Default options
        StringBuilder commandline = new();
        _ = commandline.Append($"{GlobalOptions} ");

        // Quiet
        _ = commandline.Append(CultureInfo.InvariantCulture, $"{SilentOptions} -loglevel error ");

        // Add -fflags +genpts to generate missing timestamps
        // [mpegts @ 0x5713ff02ab40] first pts and dts value must be set
        // av_interleaved_write_frame(): Invalid data found when processing input
        // [matroska @ 0x604976cd9dc0] Can't write packet with unknown timestamp
        // av_interleaved_write_frame(): Invalid argument
        _ = commandline.Append("-fflags +genpts ");

        // Snippets
        if (Program.Options.TestSnippets)
        {
            _ = commandline.Append(
                CultureInfo.InvariantCulture,
                $"{GetStopSplit(Program.SnippetTimeSpan)} "
            );
        }

        // Input filename
        _ = commandline.Append(CultureInfo.InvariantCulture, $"-i \"{inputName}\" ");

        // Default output options
        _ = commandline.Append($"{OutputOptions} ");

        // ReMux and copy all streams to specific format
        _ = commandline.Append(
            CultureInfo.InvariantCulture,
            $"-map 0 -c copy -f {format} \"{outputName}\""
        );

        // Execute
        int exitCode = Command(commandline.ToString(), 5, out _, out error);
        return exitCode == 0;
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
        trackList.ForEach(item => sb.Append(CultureInfo.InvariantCulture, $"-map 0:{item.Id} "));
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

        // Default options
        StringBuilder commandline = new();
        _ = commandline.Append($"{GlobalOptions} ");

        // Quiet
        _ = commandline.Append($"{SilentOptions} ");

        // Encoding options
        if (!string.IsNullOrEmpty(Program.Config.ConvertOptions.FfMpegOptions.Global))
        {
            _ = commandline.Append(
                CultureInfo.InvariantCulture,
                $"{Program.Config.ConvertOptions.FfMpegOptions.Global} "
            );
        }

        // Snippets
        if (Program.Options.TestSnippets)
        {
            _ = commandline.Append(
                CultureInfo.InvariantCulture,
                $"{GetStopSplit(Program.SnippetTimeSpan)} "
            );
        }

        // Input filename
        _ = commandline.Append(CultureInfo.InvariantCulture, $"-i \"{inputName}\" ");

        // Default output options
        _ = commandline.Append($"{OutputOptions} ");

        // Input and output map
        _ = commandline.Append(CultureInfo.InvariantCulture, $"{inputMap} {outputMap} ");

        // Output to MKV
        _ = commandline.Append(CultureInfo.InvariantCulture, $"-f matroska \"{outputName}\"");

        // Execute
        int exitCode = Command(commandline.ToString(), 5, out _, out error);
        return exitCode == 0;
    }

    public bool ConvertToMkv(string inputName, string outputName, out string error)
    {
        // Delete output file
        _ = FileEx.DeleteFile(outputName);

        // Default options
        StringBuilder commandline = new();
        _ = commandline.Append($"{GlobalOptions} ");

        // Quiet
        _ = commandline.Append($"{SilentOptions} ");

        // Encoding options
        if (!string.IsNullOrEmpty(Program.Config.ConvertOptions.FfMpegOptions.Global))
        {
            _ = commandline.Append(
                CultureInfo.InvariantCulture,
                $"{Program.Config.ConvertOptions.FfMpegOptions.Global} "
            );
        }

        // Snippets
        if (Program.Options.TestSnippets)
        {
            _ = commandline.Append(
                CultureInfo.InvariantCulture,
                $"{GetStopSplit(Program.SnippetTimeSpan)} "
            );
        }

        // Input filename
        _ = commandline.Append(CultureInfo.InvariantCulture, $"-i \"{inputName}\" ");

        // Default output options
        _ = commandline.Append($"{OutputOptions} ");

        // Process all tracks
        _ = commandline.Append("-map 0 ");

        // Convert video
        // E.g. -c:v libx264 -crf 20 -preset medium
        _ = commandline.Append(
            CultureInfo.InvariantCulture,
            $"-c:v {Program.Config.ConvertOptions.FfMpegOptions.Video} "
        );

        // Convert audio
        // E.g. -c:a ac3
        _ = commandline.Append(
            CultureInfo.InvariantCulture,
            $"-c:a {Program.Config.ConvertOptions.FfMpegOptions.Audio} "
        );

        // Copy subtitles
        _ = commandline.Append("-c:s copy ");

        // Output to MKV
        _ = commandline.Append(CultureInfo.InvariantCulture, $"-f matroska \"{outputName}\"");

        // Execute
        int exitCode = Command(commandline.ToString(), 5, out _, out error);
        return exitCode == 0;
    }

    public bool RemoveNalUnits(string inputName, int nalUnit, string outputName, out string error)
    {
        // Delete output file
        _ = FileEx.DeleteFile(outputName);

        // Default options
        StringBuilder commandline = new();
        _ = commandline.Append($"{GlobalOptions} ");

        // Quiet
        _ = commandline.Append($"{SilentOptions} ");

        // Snippets
        if (Program.Options.TestSnippets)
        {
            _ = commandline.Append(
                CultureInfo.InvariantCulture,
                $"{GetStopSplit(Program.SnippetTimeSpan)} "
            );
        }

        // Input filename
        _ = commandline.Append(CultureInfo.InvariantCulture, $"-i \"{inputName}\" ");

        // Default output options
        _ = commandline.Append($"{OutputOptions} ");

        // Remove SEI NAL units e.g. EIA-608 and CTA-708 content
        // https://ffmpeg.org/ffmpeg-bitstream-filters.html#filter_005funits
        _ = commandline.Append(
            CultureInfo.InvariantCulture,
            $"-map 0 -c copy -bsf:v \"filter_units=remove_types={nalUnit}\" "
        );

        // Output to MKV
        _ = commandline.Append(CultureInfo.InvariantCulture, $"-f matroska \"{outputName}\"");

        // Execute
        int exitCode = Command(commandline.ToString(), 5, out _, out error);
        return exitCode == 0;
    }

    public bool RemoveMetadata(string inputName, string outputName, out string error)
    {
        // Delete output file
        _ = FileEx.DeleteFile(outputName);

        // Default options
        StringBuilder commandline = new();
        _ = commandline.Append($"{GlobalOptions} ");

        // Quiet
        _ = commandline.Append($"{SilentOptions} ");

        // Snippets
        if (Program.Options.TestSnippets)
        {
            _ = commandline.Append(
                CultureInfo.InvariantCulture,
                $"{GetStopSplit(Program.SnippetTimeSpan)} "
            );
        }

        // Input filename
        _ = commandline.Append(CultureInfo.InvariantCulture, $"-i \"{inputName}\" ");

        // Default output options
        _ = commandline.Append($"{OutputOptions} ");

        // Remove all metadata using -map_metadata -1
        _ = commandline.Append(CultureInfo.InvariantCulture, $"-map_metadata -1 -map 0 -c copy ");

        // Output to MKV
        _ = commandline.Append(CultureInfo.InvariantCulture, $"-f matroska \"{outputName}\"");

        // Execute
        int exitCode = Command(commandline.ToString(), 5, out _, out error);
        return exitCode == 0;
    }

    public bool GetIdetText(string inputName, out string text)
    {
        // Use Idet to get frame statistics
        // https://ffmpeg.org/ffmpeg-filters.html#idet

        // Counting can report stream errors, keep going, do not use -xerror
        // [h264 @ 0x55ec750529c0] Invalid NAL unit size (106673 > 27162).
        // [h264 @ 0x55ec750529c0] Error splitting the input into NAL units.
        // Error while decoding stream #0:0: Invalid data found when processing input

        // Default options
        StringBuilder commandline = new();
        _ = commandline.Append($"{GlobalOptions} ");

        // Quiet
        _ = commandline.Append($"{SilentOptions} ");

        // Quickscan
        if (Program.Options.QuickScan)
        {
            _ = commandline.Append(
                CultureInfo.InvariantCulture,
                $"{GetStopSplit(Program.QuickScanTimeSpan)} "
            );
        }

        // Ignore audio, subtitles, data streams
        _ = commandline.Append("-an -sn -dn ");

        // Input filename
        _ = commandline.Append(CultureInfo.InvariantCulture, $"-i \"{inputName}\" ");

        // Default output options
        _ = commandline.Append($"{OutputOptions} ");

        // Run idet filter on video stream output to null muxer
        _ = commandline.Append(CultureInfo.InvariantCulture, $"-vf idet -f null -");

        // Execute and limit captured output to 10 lines to get stats
        int exitCode = Command(commandline.ToString(), 10, out _, out text);
        return exitCode == 0;
    }

    public const string SilentOptions = "-nostats";

    // https://trac.ffmpeg.org/ticket/6375
    // Too many packets buffered for output stream 0:1
    // Set max_muxing_queue_size to large value to work around issue
    // TODO: Issue is reported fixed, to be verified
    public const string OutputOptions = "-max_muxing_queue_size 1024 -abort_on empty_output";

    // https://trac.ffmpeg.org/ticket/2622
    // Error with some PGS subtitles
    // [matroska,webm @ 000001d77fb61ca0] Could not find codec parameters for stream 2 (Subtitle: hdmv_pgs_subtitle): unspecified size
    // Consider increasing the value for the 'analyzeduration' and 'probesize' options
    // TODO: Issue is reported fixed, to be verified
    public const string GlobalOptions = "-analyzeduration 2G -probesize 2G -hide_banner";

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
            .FirstOrDefault(item => item.format.Equals(format, StringComparison.OrdinalIgnoreCase))
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
