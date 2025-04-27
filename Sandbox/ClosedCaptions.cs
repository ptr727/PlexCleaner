using System.Diagnostics;
using InsaneGenius.Utilities;
using PlexCleaner;
using Serilog;

namespace Sandbox;

// Settings:
/*
{
    "ClosedCaptions": {
        "CCExtractor": "ccextractor.exe",
        "FilePath": "C:\\Temp\\Test\\ClosedCaptions",
        "ProcessExtensions": ".ts,.mp4,.mkv"
    }
}
*/

public class ClosedCaptions
{
    private readonly string _ccExtractor;
    private readonly string _filePath;
    private readonly List<string> _processExtensionList;
    private readonly TimeSpan _timeSpan = TimeSpan.FromSeconds(30);

    public ClosedCaptions(Program program)
    {
        // Get settings
        Dictionary<string, string>? settings = program.GetSettingsDictionary(
            nameof(ClosedCaptions)
        );
        Debug.Assert(settings is not null);
        _ccExtractor = settings["CCExtractor"];
        _filePath = settings["FilePath"];
        _processExtensionList = [.. settings["ProcessExtensions"].Split(',').Select(s => s.Trim())];
    }

    public int Test()
    {
        // Get tools
        if (!Tools.VerifyTools() && !Tools.CheckForNewTools())
        {
            return -1;
        }

        // Get files
        if (!FileEx.EnumerateDirectory(_filePath, out List<FileInfo> fileInfoList, out _))
        {
            return -1;
        }

        // Process files
        fileInfoList.ForEach(fileInfo =>
        {
            if (_processExtensionList.Contains(fileInfo.Extension))
            {
                // Skip snippet files
                if (fileInfo.Name.Contains("_snip", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                // Get info from original file
                _ = MediaInfoToJson(
                    fileInfo.FullName,
                    Path.ChangeExtension(fileInfo.FullName, "mediainfo.json")
                );
                _ = CcExtractorToText(
                    fileInfo.FullName,
                    Path.ChangeExtension(fileInfo.FullName, "ccextractor.txt")
                );
                _ = FfProbeSubCcToJson(
                    fileInfo.FullName,
                    Path.ChangeExtension(fileInfo.FullName, "ffprobe_subcc.json")
                );
                _ = FfProbeEia608ToJson(
                    fileInfo.FullName,
                    Path.ChangeExtension(fileInfo.FullName, "ffprobe_eia608.json")
                );
                // FfProbeAnalyzeFramesToJson(fileInfo.FullName, Path.ChangeExtension(fileInfo.FullName, "ffprobe_analyzeframes.json"));
                _ = FfMpegToSrt(
                    fileInfo.FullName,
                    Path.ChangeExtension(fileInfo.FullName, "ffmpeg.srt")
                );

                // Create file snippets and get info
                string fileName = Path.ChangeExtension(fileInfo.FullName, "mkvmerge_snip.mkv");
                _ = MkvMergeToSnippet(fileInfo.FullName, fileName);
                _ = MediaInfoToJson(
                    fileName,
                    Path.ChangeExtension(fileInfo.FullName, "mkvmerge_snip_mediainfo.json")
                );
                fileName = Path.ChangeExtension(fileInfo.FullName, "ffmpeg_snip.ts");
                _ = FfMpegToSnippet(fileInfo.FullName, fileName);
                _ = MediaInfoToJson(
                    fileName,
                    Path.ChangeExtension(fileInfo.FullName, "ffmpeg_snip_mediainfo.json")
                );
                _ = CcExtractorToText(
                    fileName,
                    Path.ChangeExtension(fileInfo.FullName, "ffmpeg_snip_ccextractor.txt")
                );
            }
        });

        return 0;
    }

    // https://superuser.com/questions/1893137/how-to-quote-a-file-name-containing-single-quotes-in-ffmpeg-ffprobe-movie-filena
    private static string EscapeMovieFilterPath(string path) =>
        path.Replace(@"\", @"/")
            .Replace(@":", @"\\:")
            .Replace(@"'", @"\\\'")
            .Replace(@",", @"\\\,");

    private bool FfMpegToSnippet(string inFile, string outFile)
    {
        Log.Information("FFmpeg remuxing {InFile} to {OutFile}", inFile, outFile);
        string commandline =
            // From online discussion?
            // a53 defaults 1, sn do not encode subtitles, an doe not encode audio?
            // $"-t {(int)_timeSpan.TotalSeconds} -i \"{inFile}\" -map 0:v:0 -c:v:0 copy -a53cc 1 -an -sn -y -f mpegts \"{outFile}\"";
            //
            // Map all tracks and copy streams output in TS format
            // $"-fflags +genpts -t {(int)_timeSpan.TotalSeconds} -i \"{inFile}\" -map 0 -c copy -y -f mpegts \"{outFile}\"";

            // Amp only video and copy streams output in TS format
            $"-fflags +genpts -t {(int)_timeSpan.TotalSeconds} -i \"{inFile}\" -map 0:v -c copy -y -f mpegts \"{outFile}\"";
        int ret = ProcessEx.Execute(
            Tools.FfMpeg.GetToolPath(),
            commandline,
            false,
            0,
            out string _,
            out string error
        );
        if (ret is not 0 and not 1)
        {
            Log.Error("FFmpeg error : {Error}", error);
            return false;
        }
        return true;
    }

    private bool MkvMergeToSnippet(string inFile, string outFile)
    {
        Log.Information("MkvMerge remuxing {InFile} to {OutFile}", inFile, outFile);
        string commandline =
            $"--split parts:-{(int)_timeSpan.TotalSeconds}s --output \"{outFile}\" \"{inFile}\"";
        int ret = ProcessEx.Execute(
            Tools.MkvMerge.GetToolPath(),
            commandline,
            false,
            0,
            out string _,
            out string error
        );
        if (ret is not 0 and not 1)
        {
            Log.Error("MkvMerge error : {Error}", error);
            return false;
        }
        return true;
    }

    private static bool MediaInfoToJson(string inFile, string outFile)
    {
        Log.Information("MediaInfo JSON from {InFile} to {OutFile}", inFile, outFile);
        string commandline = $"--Output=JSON \"{inFile}\"";
        int ret = ProcessEx.Execute(
            Tools.MediaInfo.GetToolPath(),
            commandline,
            false,
            0,
            out string output,
            out string error
        );
        if (ret != 0)
        {
            Log.Error("MediaInfo error : {Error}", error);
            return false;
        }

        File.WriteAllText(outFile, output);
        return true;
    }

    private static bool FfProbeEia608ToJson(string inFile, string outFile)
    {
        Log.Information("FFprobe EIA608 JSON from {InFile} to {OutFile}", inFile, outFile);
        string commandline =
            $"-loglevel error -f lavfi -i \"movie={EscapeMovieFilterPath(inFile)},readeia608\" -show_entries frame=best_effort_timestamp_time,duration_time:frame_tags=lavfi.readeia608.0.line,lavfi.readeia608.0.cc,lavfi.readeia608.1.line,lavfi.readeia608.1.cc -print_format json";
        int ret = ProcessEx.Execute(
            Tools.FfProbe.GetToolPath(),
            commandline,
            false,
            0,
            out string output,
            out string error
        );
        if (ret != 0)
        {
            Log.Error("FFprobe error : {Error}", error);
            return false;
        }

        File.WriteAllText(outFile, output);
        return true;
    }

    private static bool FfProbeSubCcToJson(string inFile, string outFile)
    {
        Log.Information("FFprobe SUBCC JSON from {InFile} to {OutFile}", inFile, outFile);
        string commandline =
            $"-loglevel error -select_streams s:0 -f lavfi -i \"movie={EscapeMovieFilterPath(inFile)}[out0+subcc]\" -show_packets -print_format json";
        int ret = ProcessEx.Execute(
            Tools.FfProbe.GetToolPath(),
            commandline,
            false,
            0,
            out string output,
            out string error
        );
        if (ret != 0)
        {
            Log.Error("FFprobe error : {Error}", error);
            return false;
        }

        File.WriteAllText(outFile, output);
        return true;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members"
    )]
    private bool FfProbeAnalyzeFramesToJson(string inFile, string outFile)
    {
        // Not yet released in v7.1.1 as of writing
        // https://github.com/FFmpeg/FFmpeg/commit/90af8e07b02e690a9fe60aab02a8bccd2cbf3f01
        Log.Information("FFprobe AnalyzeFrames JSON from {InFile} to {OutFile}", inFile, outFile);
        string commandline =
            $"-loglevel error -show_streams -analyze_frames -read_intervals %+{(int)_timeSpan.TotalSeconds} \"{inFile}\" -print_format json";
        int ret = ProcessEx.Execute(
            Tools.FfProbe.GetToolPath(),
            commandline,
            false,
            0,
            out string output,
            out string error
        );
        if (ret != 0)
        {
            Log.Error("FFprobe error : {Error}", error);
            return false;
        }

        File.WriteAllText(outFile, output);
        return true;
    }

    private bool CcExtractorToText(string inFile, string outFile)
    {
        Log.Information("CCExtractor TEXT from {InFile} to {OutFile}", inFile, outFile);
        string commandline = $"\"{inFile}\" -in=ts -12 -out=report";
        int ret = ProcessEx.Execute(
            _ccExtractor,
            commandline,
            false,
            0,
            out string output,
            out string error
        );
        if (ret is not 0 and not 10)
        {
            Log.Error("CCExtractor error : {Error}", error);
            return false;
        }

        File.WriteAllText(outFile, output);
        return true;
    }

    private static bool FfMpegToSrt(string inFile, string outFile)
    {
        Log.Information("FFmpeg SRT from {InFile} to {OutFile}", inFile, outFile);
        string commandline =
            $"-abort_on empty_output -y -f lavfi -i \"movie={EscapeMovieFilterPath(inFile)}[out0+subcc]\" -map 0:s -c:s srt \"{outFile}\"";
        int ret = ProcessEx.Execute(
            Tools.FfMpeg.GetToolPath(),
            commandline,
            false,
            0,
            out string _,
            out string error
        );
        if (ret != 0)
        {
            Log.Error("FFmpeg error : {Error}", error);
            return false;
        }
        return true;
    }
}
