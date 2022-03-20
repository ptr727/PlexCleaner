using InsaneGenius.Utilities;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

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

public class FfMpegTool : MediaTool
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
        // E.g. Windows : "ffmpeg version 4.3.1-2020-11-19-full_build-www.gyan.dev Copyright (c) 2000-2020 the FFmpeg developers"
        // E.g. Linux : "ffmpeg version 4.3.1-1ubuntu0~20.04.sav1 Copyright (c) 2000-2020 the FFmpeg developers"
        string[] lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        // Extract the short version number
        // Match word for ffmpeg or ffprobe
        const string pattern = @"([^\s]+)\ version\ (?<version>.*?)-";
        Regex regex = new(pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
        Match match = regex.Match(lines[0]);
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
            // https://www.ffmpeg.org/download.html
            // https://www.gyan.dev/ffmpeg/builds/packages/

            // Load the release version page
            // https://www.gyan.dev/ffmpeg/builds/release-version
            using HttpClient httpClient = new();
            mediaToolInfo.Version = httpClient.GetStringAsync("https://www.gyan.dev/ffmpeg/builds/release-version").Result;

            // Create download URL and the output filename using the version number
            mediaToolInfo.FileName = $"ffmpeg-{mediaToolInfo.Version}-full_build.7z";
            mediaToolInfo.Url = $"https://www.gyan.dev/ffmpeg/builds/packages/{mediaToolInfo.FileName}";
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

        try
        {
            // https://www.ffmpeg.org/download.html
            // https://johnvansickle.com/ffmpeg/

            // Load the release version page
            // https://johnvansickle.com/ffmpeg/release-readme.txt
            using HttpClient httpClient = new();
            string readmePage = httpClient.GetStringAsync("https://johnvansickle.com/ffmpeg/release-readme.txt").Result;

            // Read each line until we find the build and version lines
            // build: ffmpeg-5.0-amd64-static.tar.xz
            // version: 5.0
            using StringReader sr = new(readmePage);
            string buildLine = "", versionLine = "";
            while (true)
            {
                // Read the line and trim whitespace
                string line = sr.ReadLine();
                if (line == null)
                {
                    // No more lines to read
                    break;
                }
                line = line.Trim();

                // See if the line starts with "version:" or "build:"
                if (line.IndexOf("version:", StringComparison.Ordinal) == 0)
                {
                    versionLine = line;
                }
                if (line.IndexOf("build:", StringComparison.Ordinal) == 0)
                {
                    buildLine = line;
                }

                // Do we have both lines
                if (!string.IsNullOrEmpty(versionLine) &&
                    !string.IsNullOrEmpty(buildLine))
                { 
                    // Done
                    break;
                }
            }

            // Did we find the version and build
            if (string.IsNullOrEmpty(versionLine) ||
                string.IsNullOrEmpty(buildLine))
            {
                throw new NotImplementedException();
            }

            // Extract the build and version number from the lines
            const string versionPattern = @"version:\ (?<version>.*?)";
            const string buildPattern = @"build:\ (?<build>.*?)";
            Regex regex = new(versionPattern);
            Match match = regex.Match(versionLine);
            Debug.Assert(match.Success);
            mediaToolInfo.Version = match.Groups["version"].Value;
            regex = new Regex(buildPattern);
            match = regex.Match(buildLine);
            Debug.Assert(match.Success);
            mediaToolInfo.FileName = match.Groups["build"].Value;

            // Create download URL and the output filename
            // E.g. https://johnvansickle.com/ffmpeg/releases/ffmpeg-release-amd64-static.tar.xz
            // E.g. https://johnvansickle.com/ffmpeg/releases/ffmpeg-5.0-amd64-static.tar.xz
            mediaToolInfo.Url = $"https://johnvansickle.com/ffmpeg/releases/{mediaToolInfo.FileName}";
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            return false;
        }
        return true;
    }

    public override bool Update(string updateFile)
    {
        // TODO: This only works for Windows

        // FfMpeg archives have versioned folders in the zip file
        // The 7Zip -spe option does not work for zip files
        // https://sourceforge.net/p/sevenzip/discussion/45798/thread/8cb61347/
        // We need to extract to the root tools folder, that will create a subdir, then rename to the destination folder
        string extractPath = Tools.GetToolsRoot();

        // Extract the update file
        Log.Logger.Information("Extracting {UpdateFile} ...", updateFile);
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

    protected override string GetSubFolder()
    {
        return "bin";
    }

    public bool VerifyMedia(string filename, out string error)
    {
        // https://trac.ffmpeg.org/ticket/6375
        // Too many packets buffered for output stream 0:1
        // Set max_muxing_queue_size to large value to work around issue

        // Use null muxer, no stats, report errors
        // https://trac.ffmpeg.org/wiki/Null

        // Create the FfMpeg commandline and execute
        string snippet = Program.Config.VerifyOptions.VerifyDuration == 0 ? "" : $"-ss 0 -t {Program.Config.VerifyOptions.VerifyDuration}";
        if (Program.Options.TestSnippets)
        {
            snippet = Snippet;
        }
        string commandline = $"{GlobalOptions} -i \"{filename}\" {OutputOptions} {snippet} -hide_banner -nostats -loglevel error -xerror -f null -";
        // Limit captured output to 5 lines
        int exitCode = Command(commandline, 5, out string _, out error);

        // Test exitCode and stderr errors
        return exitCode == 0 && error.Length == 0;
    }

    public bool ReMuxToMkv(string inputName, MediaInfo keep, string outputName)
    {
        if (keep == null)
        {
            return ReMuxToMkv(inputName, outputName);
        }

        // Delete output file
        FileEx.DeleteFile(outputName);

        // Create an input and output map
        CreateFfMpegMap(keep, out string input, out string output);

        // Remux using map
        string snippet = Program.Options.TestSnippets ? Snippet : "";
        string commandline = $"{GlobalOptions} -i \"{inputName}\" {OutputOptions} {snippet} {input} {output} -f matroska \"{outputName}\"";
        int exitCode = Command(commandline);
        return exitCode == 0;
    }

    public bool ReMuxToMkv(string inputName, string outputName)
    {
        // Delete output file
        FileEx.DeleteFile(outputName);

        // Remux and copy all streams
        string snippet = Program.Options.TestSnippets ? Snippet : "";
        string commandline = $"{GlobalOptions} -i \"{inputName}\" {OutputOptions} {snippet} -map 0 -codec copy -f matroska \"{outputName}\"";
        int exitCode = Command(commandline);
        return exitCode == 0;
    }

    private static void CreateFfMpegMap(MediaInfo keep, out string input, out string output)
    {
        // Remux only
        MediaInfo reEncode = new(ToolType.FfProbe);
        CreateFfMpegMap("", 0, "", keep, reEncode, out input, out output);
    }

    private static void CreateFfMpegMap(string videoCodec, int videoQuality, string audioCodec, MediaInfo keep, MediaInfo reEncode, out string input, out string output)
    {
        // Verify correct data type
        Debug.Assert(keep.Parser == ToolType.FfProbe);
        Debug.Assert(reEncode.Parser == ToolType.FfProbe);

        // Create an input and output map
        // Order by video, audio, and subtitle
        // Each ordered by id, to keep the original order
        List<TrackInfo> videoList = new();
        videoList.AddRange(keep.Video);
        videoList.AddRange(reEncode.Video);
        videoList = videoList.OrderBy(item => item.Id).ToList();

        List<TrackInfo> audioList = new();
        audioList.AddRange(keep.Audio);
        videoList.AddRange(reEncode.Audio);
        audioList = audioList.OrderBy(item => item.Id).ToList();

        List<TrackInfo> subtitleList = new();
        subtitleList.AddRange(keep.Subtitle);
        videoList.AddRange(reEncode.Subtitle);
        subtitleList = subtitleList.OrderBy(item => item.Id).ToList();

        // Create a map list of all the input streams we want in the output
        List<TrackInfo> trackList = new();
        trackList.AddRange(videoList);
        trackList.AddRange(audioList);
        trackList.AddRange(subtitleList);
        StringBuilder sb = new();
        trackList.ForEach(item => sb.Append($"-map 0:{item.Id} "));
        input = sb.ToString();
        input = input.Trim();

        // Set the output stream types for each input map item
        // The order has to match the input order
        sb.Clear();
        int videoIndex = 0;
        int audioIndex = 0;
        int subtitleIndex = 0;
        foreach (TrackInfo info in trackList)
        {
            // Copy or encode
            if (info.GetType() == typeof(VideoInfo))
            {
                sb.Append(info.State == TrackInfo.StateType.Keep
                    ? $"-c:v:{videoIndex++} copy "
                    : $"-c:v:{videoIndex++} {videoCodec} -crf {videoQuality} -preset medium ");
            }
            else if (info.GetType() == typeof(AudioInfo))
            {
                sb.Append(info.State == TrackInfo.StateType.Keep
                    ? $"-c:a:{audioIndex++} copy "
                    : $"-c:a:{audioIndex++} {audioCodec} ");
            }
            else if (info.GetType() == typeof(SubtitleInfo))
            {
                // No re-encoding of subtitles, just copy
                sb.Append($"-c:s:{subtitleIndex++} copy ");
            }
        }
        output = sb.ToString();
        output = output.Trim();
    }

    private bool ConvertToMkv(string inputName, string videoCodec, int videoQuality, string audioCodec, MediaInfo keep, MediaInfo reEncode, string outputName)
    {
        // Simple encoding of audio and video and passthrough of other tracks
        if (keep == null || reEncode == null)
        {
            return ConvertToMkv(inputName, videoCodec, videoQuality, audioCodec, outputName);
        }

        // Delete output file
        FileEx.DeleteFile(outputName);

        // Create an input and output map
        CreateFfMpegMap(videoCodec, videoQuality, audioCodec, keep, reEncode, out string input, out string output);

        // TODO: Error with some PGS subtitles
        // https://trac.ffmpeg.org/ticket/2622
        //  [matroska,webm @ 000001d77fb61ca0] Could not find codec parameters for stream 2 (Subtitle: hdmv_pgs_subtitle): unspecified size
        //  Consider increasing the value for the 'analyzeduration' and 'probesize' options

        // Convert using map
        string snippet = Program.Options.TestSnippets ? Snippet : "";
        string commandline = $"{GlobalOptions} -i \"{inputName}\" {OutputOptions} {snippet} {input} {output} -f matroska \"{outputName}\"";
        int exitCode = Command(commandline);
        return exitCode == 0;
    }
    public bool ConvertToMkv(string inputName, MediaInfo keep, MediaInfo reEncode, string outputName)
    {
        // Use defaults
        return ConvertToMkv(inputName,
            Program.Config.ConvertOptions.EnableH265Encoder ? H265Codec : H264Codec,
            Program.Config.ConvertOptions.VideoEncodeQuality,
            Program.Config.ConvertOptions.AudioEncodeCodec,
            keep,
            reEncode,
            outputName);
    }

    private bool ConvertToMkv(string inputName, string videoCodec, int videoQuality, string audioCodec, string outputName)
    {
        // Delete output file
        FileEx.DeleteFile(outputName);

        // Encode video and audio, copy subtitle streams
        string snippet = Program.Options.TestSnippets ? Snippet : "";
        string commandline = $"{GlobalOptions} -i \"{inputName}\" {OutputOptions} {snippet} -map 0 -c:v {videoCodec} -crf {videoQuality} -preset medium -c:a {audioCodec} -c:s copy -f matroska \"{outputName}\"";
        int exitCode = Command(commandline);
        return exitCode == 0;
    }

    public bool ConvertToMkv(string inputName, string videoCodec, int videoQuality, string outputName)
    {
        // Delete output file
        FileEx.DeleteFile(outputName);

        // Encode video, copy audio and subtitle streams
        string snippet = Program.Options.TestSnippets ? Snippet : "";
        string commandline = $"{GlobalOptions} -i \"{inputName}\" {OutputOptions} {snippet} -map 0 -c:v {videoCodec} -crf {videoQuality} -preset medium -c:a copy -c:s copy -f matroska \"{outputName}\"";
        int exitCode = Command(commandline);
        return exitCode == 0;
    }

    public bool ConvertToMkv(string inputName, string outputName)
    {
        // Use defaults
        return ConvertToMkv(inputName,
            Program.Config.ConvertOptions.EnableH265Encoder ? H265Codec : H264Codec,
            Program.Config.ConvertOptions.VideoEncodeQuality,
            Program.Config.ConvertOptions.AudioEncodeCodec,
            outputName);
    }

    public bool RemoveClosedCaptions(string inputName, string outputName)
    {
        // Delete output file
        FileEx.DeleteFile(outputName);

        // Remove SEI NAL units, e.g. EIA-608, from video stream using -bsf:v "filter_units=remove_types=6"
        // https://ffmpeg.org/ffmpeg-bitstream-filters.html#filter_005funits
        string snippet = Program.Options.TestSnippets ? Snippet : "";
        string commandline = $"{GlobalOptions} -i \"{inputName}\" {OutputOptions} {snippet} -map 0 -c copy -bsf:v \"filter_units=remove_types=6\" -f matroska \"{outputName}\"";
        int exitCode = Command(commandline);
        return exitCode == 0;
    }

    public bool RemoveMetadata(string inputName, string outputName)
    {
        // Delete output file
        FileEx.DeleteFile(outputName);

        // Remove all metadata using -map_metadata -1
        string snippet = Program.Options.TestSnippets ? Snippet : "";
        string commandline = $"{GlobalOptions} -i \"{inputName}\" {OutputOptions} {snippet} -map_metadata -1 -map 0 -c copy -f matroska \"{outputName}\"";
        int exitCode = Command(commandline);
        return exitCode == 0;
    }

    public bool GetIdetInfo(string filename, out FfMpegIdetInfo idetInfo)
    {
        idetInfo = null;
        return GetIdetInfoText(filename, out string text) &&
               GetIdetInfoFromText(text, out idetInfo);
    }

    private bool GetIdetInfoText(string inputName, out string text)
    {
        // Use Idet to get frame statistics
        // https://ffmpeg.org/ffmpeg-filters.html#idet
        // http://www.aktau.be/2013/09/22/detecting-interlaced-video-with-ffmpeg/

        // Null output is platform specific
        // https://trac.ffmpeg.org/wiki/Null
        string nullOut = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "-y NUL" : "-y /dev/null";

        // Run idet filter
        string snippet = Program.Config.VerifyOptions.IdetDuration == 0 ? "" : $"-t 0 -ss {Program.Config.VerifyOptions.IdetDuration}";
        if (Program.Options.TestSnippets)
        {
            snippet = Snippet;
        }
        string commandline = $"{GlobalOptions} -i \"{inputName}\" {OutputOptions} {snippet} -hide_banner -nostats -xerror -filter:v idet -an -f rawvideo {nullOut}";
        // Limit captured output to 5 lines
        int exitCode = Command(commandline, 5, out string _, out text);
        return exitCode == 0;
    }

    private static bool GetIdetInfoFromText(string text, out FfMpegIdetInfo idetInfo)
    {
        if (string.IsNullOrEmpty(text))
        {
            throw new ArgumentNullException(nameof(text));
        }

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

            // Pattern
            const string repeatedFields = @"\[Parsed_idet_0\ \@\ (.*?)\]\ Repeated\ Fields:\ Neither:(?<repeated_neither>.*?)Top:(?<repeated_top>.*?)Bottom:(?<repeated_bottom>.*?)$";
            const string singleFrame = @"\[Parsed_idet_0\ \@\ (.*?)\]\ Single\ frame\ detection:\ TFF:(?<single_tff>.*?)BFF:(?<single_bff>.*?)Progressive:(?<single_prog>.*?)Undetermined:(?<single_und>.*?)$";
            const string multiFrame = @"\[Parsed_idet_0\ \@\ (.*?)\]\ Multi\ frame\ detection:\ TFF:(?<multi_tff>.*?)BFF:(?<multi_bff>.*?)Progressive:(?<multi_prog>.*?)Undetermined:(?<multi_und>.*?)$";

            // We need to match in LF not CRLF mode else $ does not work as expected
            const string pattern = $"{repeatedFields}\n{singleFrame}\n{multiFrame}";
            string textLf = text.Replace("\r\n", "\n", StringComparison.Ordinal);

            // Match
            Regex regex = new(pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
            Match match = regex.Match(textLf);
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

    private const string H264Codec = "libx264";
    private const string H265Codec = "libx265";
    private const string Snippet = "-ss 0 -t 180";
    private const string GlobalOptions = "-analyzeduration 2147483647 -probesize 2147483647";
    private const string OutputOptions = "-max_muxing_queue_size 1024 -abort_on empty_output";
}