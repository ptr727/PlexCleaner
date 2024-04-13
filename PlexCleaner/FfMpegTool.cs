using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using InsaneGenius.Utilities;
using Serilog;

// https://ffmpeg.org/ffmpeg.html
// https://trac.ffmpeg.org/wiki/Map
// https://trac.ffmpeg.org/wiki/Encode/H.264
// https://trac.ffmpeg.org/wiki/Encode/H.265

// FfMpeg logs to stderr, not stdout
// "By default the program logs to stderr. If coloring is supported by the terminal, colors are used to mark errors and warnings."
// Power events, e.g. sleep, can result in an invalid argument error
// TODO: Figure out how to get ffmpeg more resilient to power events
// TODO: Figure out how to capture logs while still allowing ffmpeg to print in color

// Typical commandline:
// ffmpeg [global_options] {[input_file_options] -i input_url} ... {[output_file_options] output_url}

namespace PlexCleaner;

public partial class FfMpegTool : MediaTool
{
    public override ToolFamily GetToolFamily()
    {
        return ToolFamily.FfMpeg;
    }

    public override ToolType GetToolType()
    {
        return ToolType.FfMpeg;
    }

    protected override string GetToolNameWindows()
    {
        return "ffmpeg.exe";
    }

    protected override string GetToolNameLinux()
    {
        return "ffmpeg";
    }

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

        // First line as version
        // Windows : "ffmpeg version 4.3.1-2020-11-19-full_build-www.gyan.dev Copyright (c) 2000-2020 the FFmpeg developers"
        // Ubuntu: "ffmpeg version 4.3.1-1ubuntu0~20.04.sav1 Copyright (c) 2000-2020 the FFmpeg developers"
        // Arch: "ffmpeg version n6.0 Copyright (c) 2000-2023 the FFmpeg developers"
        var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        // Extract the short version number
        // Match word for ffmpeg or ffprobe
        var match = InstalledVersionRegex().Match(lines[0]);
        Debug.Assert(match.Success);
        mediaToolInfo.Version = match.Groups["version"].Value;

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
            mediaToolInfo.Url = GitHubRelease.GetDownloadUri(repo, mediaToolInfo.Version, mediaToolInfo.FileName);
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            return false;
        }
        return true;
    }

    protected override bool GetLatestVersionLinux(out MediaToolInfo mediaToolInfo)
    {
        // Initialize            
        mediaToolInfo = new MediaToolInfo(this);

        // TODO:
        return false;
    }

    public override bool Update(string updateFile)
    {
        // TODO: This only works for Windows

        // FfMpeg archives have versioned folders in the zip file
        // The 7Zip -spe option does not work for zip files
        // https://sourceforge.net/p/sevenzip/discussion/45798/thread/8cb61347/
        // We need to extract to the root tools folder, that will create a subdir, then rename to the destination folder
        var extractPath = Tools.GetToolsRoot();

        // Extract the update file
        Log.Logger.Information("Extracting {UpdateFile} ...", updateFile);
        if (!Tools.SevenZip.UnZip(updateFile, extractPath))
        {
            return false;
        }

        // Delete the tool destination directory
        var toolPath = GetToolFolder();
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

    protected override string GetSubFolder()
    {
        return "bin";
    }

    public bool VerifyMedia(string fileName, out string error)
    {
        // https://trac.ffmpeg.org/ticket/6375
        // Too many packets buffered for output stream 0:1
        // Set max_muxing_queue_size to large value to work around issue

        // Use null muxer, no stats, report errors
        // https://trac.ffmpeg.org/wiki/Null

        // Build commandline
        StringBuilder commandline = new();
        CreateDefaultArgs(fileName, commandline, false);

        // Null muxer and exit immediately on error (-xerror)
        commandline.Append("-hide_banner -nostats -loglevel error -xerror -f null -");

        // Execute and limit captured output to last 5 lines
        var exitCode = Command(commandline.ToString(), 5, out _, out error);

        // Test exitCode and stderr errors
        return exitCode == 0 && error.Length == 0;
    }

    public bool ReMuxToMkv(string inputName, string outputName)
    {
        // Delete output file
        FileEx.DeleteFile(outputName);

        // Build commandline
        StringBuilder commandline = new();
        CreateDefaultArgs(inputName, commandline, false);

        // Remux and copy all streams to MKV
        commandline.Append($"-map 0 -codec copy -f matroska \"{outputName}\"");

        // Execute
        var exitCode = Command(commandline.ToString());
        return exitCode == 0;
    }

    private static void CreateTrackArgs(SelectMediaInfo selectMediaInfo, out string inputMap, out string outputMap)
    {
        // Verify correct media type
        Debug.Assert(selectMediaInfo.Selected.Parser == ToolType.FfProbe);
        Debug.Assert(selectMediaInfo.NotSelected.Parser == ToolType.FfProbe);

        // Create a list of all the tracks ordered by Id
        // Selected is ReEncode, state is expected to be ReEncode
        // NotSelected is Keep, state is expected to be Keep
        List<TrackInfo> trackList = selectMediaInfo.GetTrackList();

        // Create the input map using all tracks referenced by id
        // https://trac.ffmpeg.org/wiki/Map
        StringBuilder sb = new();
        trackList.ForEach(item => sb.Append($"-map 0:{item.Id} "));
        inputMap = sb.ToString();
        inputMap = inputMap.Trim();

        // Create the output map using the same order as the input map
        // Output tracks are referenced by the track type relative index
        // http://ffmpeg.org/ffmpeg.html#Stream-specifiers
        sb.Clear();
        int videoIndex = 0;
        int audioIndex = 0;
        int subtitleIndex = 0;
        foreach (var trackInfo in trackList)
        {
            // Copy or encode
            switch (trackInfo)
            {
                case VideoInfo videoInfo:
                    sb.Append(videoInfo.State == TrackInfo.StateType.Keep
                        ? $"-c:v:{videoIndex++} copy "
                        : $"-c:v:{videoIndex++} {Program.Config.ConvertOptions.FfMpegOptions.Video} ");
                    break;
                case AudioInfo audioInfo:
                    sb.Append(audioInfo.State == TrackInfo.StateType.Keep
                        ? $"-c:a:{audioIndex++} copy "
                        : $"-c:a:{audioIndex++} {Program.Config.ConvertOptions.FfMpegOptions.Audio} ");
                    break;
                case SubtitleInfo:
                    // No re-encoding of subtitles, just copy
                    sb.Append($"-c:s:{subtitleIndex++} copy ");
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
        outputMap = sb.ToString();
        outputMap = outputMap.Trim();
    }

    public bool ConvertToMkv(string inputName, SelectMediaInfo selectMediaInfo, string outputName)
    {
        // Delete output file
        FileEx.DeleteFile(outputName);

        // Create an input and output ignore or copy or convert track map
        // Selected is ReEncode
        // NotSelected is Keep
        CreateTrackArgs(selectMediaInfo, out var inputMap, out var outputMap);

        // TODO: Error with some PGS subtitles
        // https://trac.ffmpeg.org/ticket/2622
        //  [matroska,webm @ 000001d77fb61ca0] Could not find codec parameters for stream 2 (Subtitle: hdmv_pgs_subtitle): unspecified size
        //  Consider increasing the value for the 'analyzeduration' and 'probesize' options

        // Build commandline
        StringBuilder commandline = new();
        CreateDefaultArgs(inputName, commandline, true);

        // Input and output map
        commandline.Append($"{inputMap} {outputMap} ");

        // Output to MKV
        commandline.Append($"-f matroska \"{outputName}\"");

        // Execute
        var exitCode = Command(commandline.ToString());
        return exitCode == 0;
    }


    public bool ConvertToMkv(string inputName, string outputName)
    {
        // Delete output file
        FileEx.DeleteFile(outputName);

        // Build commandline
        StringBuilder commandline = new();
        CreateDefaultArgs(inputName, commandline, true);

        // Process all tracks
        commandline.Append("-map 0 ");

        // Convert video
        // E.g. -c:v libx264 -crf 20 -preset medium
        commandline.Append($"-c:v {Program.Config.ConvertOptions.FfMpegOptions.Video} ");

        // Convert audio
        // E.g. -c:a ac3
        commandline.Append($"-c:a {Program.Config.ConvertOptions.FfMpegOptions.Audio} ");

        // Copy subtitles
        commandline.Append("-c:s copy ");

        // Output to MKV
        commandline.Append($"-f matroska \"{outputName}\"");

        // Execute
        var exitCode = Command(commandline.ToString());
        return exitCode == 0;
    }

    public bool RemoveClosedCaptions(string inputName, string outputName)
    {
        // Delete output file
        FileEx.DeleteFile(outputName);

        // Build commandline
        StringBuilder commandline = new();
        CreateDefaultArgs(inputName, commandline, false);

        // Remove SEI NAL units, e.g. EIA-608, from video stream using -bsf:v "filter_units=remove_types=6"
        // https://ffmpeg.org/ffmpeg-bitstream-filters.html#filter_005funits
        commandline.Append($"-map 0 -c copy -bsf:v \"filter_units=remove_types=6\" -f matroska \"{outputName}\"");

        // Execute
        var exitCode = Command(commandline.ToString());
        return exitCode == 0;
    }

    public bool RemoveMetadata(string inputName, string outputName)
    {
        // Delete output file
        FileEx.DeleteFile(outputName);

        // Build commandline
        StringBuilder commandline = new();
        CreateDefaultArgs(inputName, commandline, false);

        // Remove all metadata using -map_metadata -1
        commandline.Append($"-map_metadata -1 -map 0 -c copy -f matroska \"{outputName}\"");

        // Execute
        var exitCode = Command(commandline.ToString());
        return exitCode == 0;
    }

    public bool GetIdetInfo(string fileName, out FfMpegIdetInfo idetInfo, out string error)
    {
        // Get idet output and parse
        idetInfo = null;
        error = null;
        if (!GetIdetInfoText(fileName, out var text) || !GetIdetInfoFromText(text, out idetInfo))
        {
            error = text;
            return false;
        }
        return true;
    }

    private bool GetIdetInfoText(string inputName, out string text)
    {
        // Use Idet to get frame statistics
        // https://ffmpeg.org/ffmpeg-filters.html#idet
        // http://www.aktau.be/2013/09/22/detecting-interlaced-video-with-ffmpeg/

        // Null output is platform specific
        // https://trac.ffmpeg.org/wiki/Null
        var nullOut = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "-y NUL" : "-y /dev/null";

        // Build commandline
        StringBuilder commandline = new();
        CreateDefaultArgs(inputName, commandline, false);

        // Counting can report stream errors, keep going, do not use -xerror
        // [h264 @ 0x55ec750529c0] Invalid NAL unit size (106673 > 27162).
        // [h264 @ 0x55ec750529c0] Error splitting the input into NAL units.
        // Error while decoding stream #0:0: Invalid data found when processing input
        // frame=44164 fps=409 q=-0.0 Lsize=119242800kB time=00:30:41.96 bitrate=530323.3kbits/s dup=1 drop=0 speed=17.1x
        // video:119242800kB audio:0kB subtitle:0kB other streams:0kB global headers:0kB muxing overhead: 0.000000%
        // [Parsed_idet_0 @ 0x55ec7698bd00] Repeated Fields: Neither: 44163 Top:     0 Bottom:     0
        // [Parsed_idet_0 @ 0x55ec7698bd00] Single frame detection: TFF:    34 BFF:    33 Progressive: 29439 Undetermined: 14657
        // [Parsed_idet_0 @ 0x55ec7698bd00] Multi frame detection: TFF:    41 BFF:    99 Progressive: 43999 Undetermined:    24

        // Run idet filter
        commandline.Append($"-hide_banner -nostats -filter:v idet -an -f rawvideo {nullOut}");

        // Execute and limit captured output to 5 lines to just get stats
        var exitCode = Command(commandline.ToString(), 5, out _, out text);
        return exitCode == 0;
    }

    private static bool GetIdetInfoFromText(string text, out FfMpegIdetInfo idetInfo)
    {
        // Init
        idetInfo = new FfMpegIdetInfo();

        // Parse the text
        try
        {
            // Example:
            // frame= 2048 fps=294 q=-0.0 Lsize= 6220800kB time=00:01:21.92 bitrate=622080.0kbits/s speed=11.8x
            // video:6220800kB audio:0kB subtitle:0kB other streams:0kB global headers:0kB muxing overhead: 0.000000%
            // [Parsed_idet_0 @ 00000234e42d0440] Repeated Fields: Neither:  2049 Top:     0 Bottom:     0
            // [Parsed_idet_0 @ 00000234e42d0440] Single frame detection: TFF:     0 BFF:     0 Progressive:  1745 Undetermined:   304
            // [Parsed_idet_0 @ 00000234e42d0440] Multi frame detection: TFF:     0 BFF:     0 Progressive:  2021 Undetermined:    28

            // We need to match in LF not CRLF mode else $ does not work as expected
            var textLf = text.Replace("\r\n", "\n", StringComparison.Ordinal);

            // Match
            var match = IdetRegex().Match(textLf);
            Debug.Assert(match.Success);

            // Get the frame counts
            idetInfo.RepeatedFields.Neither = int.Parse(match.Groups["repeated_neither"].Value.Trim(), CultureInfo.InvariantCulture);
            idetInfo.RepeatedFields.Top = int.Parse(match.Groups["repeated_top"].Value.Trim(), CultureInfo.InvariantCulture);
            idetInfo.RepeatedFields.Bottom = int.Parse(match.Groups["repeated_bottom"].Value.Trim(), CultureInfo.InvariantCulture);

            idetInfo.SingleFrame.Tff = int.Parse(match.Groups["single_tff"].Value.Trim(), CultureInfo.InvariantCulture);
            idetInfo.SingleFrame.Bff = int.Parse(match.Groups["single_bff"].Value.Trim(), CultureInfo.InvariantCulture);
            idetInfo.SingleFrame.Progressive = int.Parse(match.Groups["single_prog"].Value.Trim(), CultureInfo.InvariantCulture);
            idetInfo.SingleFrame.Undetermined = int.Parse(match.Groups["single_und"].Value.Trim(), CultureInfo.InvariantCulture);

            idetInfo.MultiFrame.Tff = int.Parse(match.Groups["multi_tff"].Value.Trim(), CultureInfo.InvariantCulture);
            idetInfo.MultiFrame.Bff = int.Parse(match.Groups["multi_bff"].Value.Trim(), CultureInfo.InvariantCulture);
            idetInfo.MultiFrame.Progressive = int.Parse(match.Groups["multi_prog"].Value.Trim(), CultureInfo.InvariantCulture);
            idetInfo.MultiFrame.Undetermined = int.Parse(match.Groups["multi_und"].Value.Trim(), CultureInfo.InvariantCulture);
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            return false;
        }
        return true;
    }

    private static void CreateDefaultArgs(string inputName, StringBuilder commandline, bool encoding)
    {
        // Add custom global options when encoding
        if (encoding && !string.IsNullOrEmpty(Program.Config.ConvertOptions.FfMpegOptions.Global))
        {
            commandline.Append($"{GlobalOptions} {Program.Config.ConvertOptions.FfMpegOptions.Global} ");
        }
        else
        {
            commandline.Append($"{GlobalOptions} ");
        }

        // Test snippets
        if (Program.Options.TestSnippets)
        {
            // https://trac.ffmpeg.org/wiki/Seeking#Cuttingsmallsections
            commandline.Append($"-ss 0 -t {(int)Program.SnippetTimeSpan.TotalSeconds} ");
        }

        // Input filename
        commandline.Append($"-i \"{inputName}\" ");

        // Output options
        commandline.Append($"{OutputOptions} ");

        // Minimize output when running in parallel mode
        if (Program.Options.Parallel)
        {
            commandline.Append($"{ParallelOptions} ");
        }
    }

    public const string ParallelOptions = "-hide_banner -nostats";
    public const string OutputOptions = "-max_muxing_queue_size 1024 -abort_on empty_output";
    public const string GlobalOptions = "-analyzeduration 2147483647 -probesize 2147483647";
    public const string DefaultVideoOptions = "libx264 -crf 22 -preset medium";
    public const string DefaultAudioOptions = "ac3";

    private const string InstalledVersionPattern = @"version\D+(?<version>([0-9]+(\.[0-9]+)+))";
    [GeneratedRegex(InstalledVersionPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    public static partial Regex InstalledVersionRegex();

    private const string IdetRepeatedFields = @"\[Parsed_idet_0\ \@\ (.*?)\]\ Repeated\ Fields:\ Neither:(?<repeated_neither>.*?)Top:(?<repeated_top>.*?)Bottom:(?<repeated_bottom>.*?)$";
    private const string IdetSingleFrame = @"\[Parsed_idet_0\ \@\ (.*?)\]\ Single\ frame\ detection:\ TFF:(?<single_tff>.*?)BFF:(?<single_bff>.*?)Progressive:(?<single_prog>.*?)Undetermined:(?<single_und>.*?)$";
    private const string IdetMultiFrame = @"\[Parsed_idet_0\ \@\ (.*?)\]\ Multi\ frame\ detection:\ TFF:(?<multi_tff>.*?)BFF:(?<multi_bff>.*?)Progressive:(?<multi_prog>.*?)Undetermined:(?<multi_und>.*?)$";
    private const string IdetPattern = $"{IdetRepeatedFields}\n{IdetSingleFrame}\n{IdetMultiFrame}";
    [GeneratedRegex(IdetPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    public static partial Regex IdetRegex();
}
