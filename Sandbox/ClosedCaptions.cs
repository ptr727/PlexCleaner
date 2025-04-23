using System.Diagnostics;
using InsaneGenius.Utilities;
using Medallion.Shell;
using PlexCleaner;
using Serilog;

namespace Sandbox;

// Settings:
/*
{
    "ClosedCaptions": {
        "ccextractor": "ccextractor.exe",
        "filepath": "C:\\Temp\\Test\\ClosedCaptions",
        "processextensions": ".ts,.mp4,.mkv"
    }
}
*/

public class ClosedCaptions
{
    private readonly string _ccExtractor;
    private readonly string _filePath;
    private readonly List<string> _processExtensionList;

    public ClosedCaptions(Program program)
    {
        // Get settings
        Dictionary<string, string>? settings = program.GetSettingsDictionary(
            nameof(ClosedCaptions)
        );
        Debug.Assert(settings is not null);
        _ccExtractor = settings["ccextractor"];
        _filePath = settings["filepath"];
        _processExtensionList = [.. settings["processextensions"].Split(',').Select(s => s.Trim())];
    }

    public int Test()
    {
        // Get tools
        if (!Tools.CheckForNewTools() || !Tools.VerifyTools())
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
                ReadEIA608(fileInfo);
                ReadSubCC(fileInfo);
                WriteSubCC(fileInfo);
                ReadMediaInfo(fileInfo);
                ReadTrimMediaInfo(fileInfo);
                ReadCCExtractor(fileInfo);
                ReadFFmpegPipeCCExtractor(fileInfo);
                ReadFFmpegTempCCExtractor(fileInfo);
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

    private static void ReadEIA608(FileInfo fileInfo)
    {
        string escapedPath = EscapeMovieFilterPath(fileInfo.FullName);
        string commandline =
            $"-loglevel error -f lavfi -i \"movie={escapedPath},readeia608\" -show_entries frame=best_effort_timestamp_time,duration_time:frame_tags=lavfi.readeia608.0.line,lavfi.readeia608.0.cc,lavfi.readeia608.1.line,lavfi.readeia608.1.cc -print_format csv";
        // $"-loglevel error -f lavfi -i \"movie={escapedPath},readeia608\" -show_entries frame=best_effort_timestamp_time,duration_time,side_data_list:frame_tags=lavfi.readeia608.0.line,lavfi.readeia608.0.cc,lavfi.readeia608.1.line,lavfi.readeia608.1.cc -print_format:compact=1 json";
        // $"-loglevel error -f lavfi -i \"movie={escapedPath},readeia608\" -show_entries frame=best_effort_timestamp_time:frame_tags=lavfi.readeia608.0.line,lavfi.readeia608.0.cc,lavfi.readeia608.1.line,lavfi.readeia608.1.cc -print_format csv";
        // $"-loglevel error -f lavfi -i \"movie={escapedPath},readeia608\" -show_entries frame=best_effort_timestamp_time,tags,side_data_list -print_format json";

        Log.Information("Reading EIA608 data from {FilePath}", fileInfo.Name);
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
            Log.Error("Error reading EIA608 data : {Error}", error);
            return;
        }

        string outputFile = Path.ChangeExtension(fileInfo.FullName, "readeia608.csv");
        Log.Information("Writing EIA608 data to {OutputFile}", outputFile);
        File.WriteAllText(outputFile, output);
    }

    private static void ReadSubCC(FileInfo fileInfo)
    {
        string escapedPath = EscapeMovieFilterPath(fileInfo.FullName);
        string commandline =
            $"-loglevel error -select_streams s:0 -f lavfi -i \"movie={escapedPath}[out0+subcc]\" -show_packets -print_format json";
        // $"-loglevel error -f lavfi -i \"movie={escapedPath}[out0+subcc]\" -show_frames -print_format json";

        Log.Information("Reading SubCC data from {FilePath}", fileInfo.Name);
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
            Log.Error("Error reading SubCC data : {Error}", error);
            return;
        }

        string outputFile = Path.ChangeExtension(fileInfo.FullName, "subcc.json");
        Log.Information("Writing SubCC data to {OutputFile}", outputFile);
        File.WriteAllText(outputFile, output);
    }

    private static void WriteSubCC(FileInfo fileInfo)
    {
        string escapedPath = EscapeMovieFilterPath(fileInfo.FullName);
        FileInfo outputFileInfo = new(Path.ChangeExtension(fileInfo.FullName, "subcc.srt"));
        string commandline =
            $"-abort_on empty_output -y -f lavfi -i \"movie={escapedPath}[out0+subcc]\" -map 0:s -c:s srt \"{outputFileInfo.FullName}\"";

        Log.Information(
            "Extracting SubCC data from {FilePath} to {OutputFile}",
            fileInfo.Name,
            outputFileInfo.Name
        );
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
            Log.Error("Error writing SubCC data : {Error}", error);
        }
        Log.Information("SubCC data written to {OutputFile}", outputFileInfo.Name);
    }

    private static void ReadMediaInfo(FileInfo fileInfo)
    {
        string commandline = $"--Output=JSON \"{fileInfo.FullName}\"";

        Log.Information("Reading MediaInfo data from {FilePath}", fileInfo.Name);
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
            Log.Error("Error reading MediaInfo data : {Error}", error);
            return;
        }

        string outputFile = Path.ChangeExtension(fileInfo.FullName, "mediainfo.json");
        Log.Information("Writing MediaInfo data to {OutputFile}", outputFile);
        File.WriteAllText(outputFile, output);
    }

    private static void ReadTrimMediaInfo(FileInfo fileInfo)
    {
        FileInfo tempFile = new(Path.ChangeExtension(fileInfo.FullName, "temp.ts"));
        string ffmpegCommandline =
            $"-hide_banner -report -loglevel error -i \"{fileInfo.FullName}\" -map 0:v:0 -c:v:0 copy -a53cc 1 -an -sn -y -t 30 -f mpegts \"{tempFile.FullName}\"";
        string mediainfoCommandline = $"--Output=JSON \"{tempFile.FullName}\"";

        Log.Information(
            "Remuxing {FilePath} to temp short TS file {TempFilePath}",
            fileInfo.Name,
            tempFile.Name
        );
        int ret = ProcessEx.Execute(
            Tools.FfMpeg.GetToolPath(),
            ffmpegCommandline,
            false,
            0,
            out string _,
            out string error
        );
        if (ret != 0)
        {
            Log.Error("Error writing temp TS file : {Error}", error);
            tempFile.Delete();
            return;
        }

        Log.Information("Reading MediaInfo data from {FilePath}", tempFile.Name);
        ret = ProcessEx.Execute(
            Tools.MediaInfo.GetToolPath(),
            mediainfoCommandline,
            false,
            0,
            out string output,
            out error
        );
        tempFile.Delete();
        if (ret != 0)
        {
            Log.Error("Error reading MediaInfo data : {Error}", error);
            return;
        }

        string outputFile = Path.ChangeExtension(fileInfo.FullName, "trim.mediainfo.json");
        Log.Information("Writing MediaInfo data to {OutputFile}", outputFile);
        File.WriteAllText(outputFile, output);
    }

    private void ReadCCExtractor(FileInfo fileInfo)
    {
        // https://github.com/CCExtractor/ccextractor/blob/master/src/lib_ccx/ccx_common_common.h

        string commandline = $"-12 -out=report \"{fileInfo.FullName}\"";

        Log.Information("Reading CCExtractor data from {FilePath}", fileInfo.Name);
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
            Log.Error("Error reading CCExtractor data : {Error}", error);
            return;
        }

        string outputFile = Path.ChangeExtension(fileInfo.FullName, "ccextractor.txt");
        Log.Information("Writing CCExtractor data to {OutputFile}", outputFile);
        File.WriteAllText(outputFile, output);
    }

    private void ReadFFmpegPipeCCExtractor(FileInfo fileInfo)
    {
        List<string> ffmpegCommandline =
        [
            "-hide_banner",
            "-report",
            "-loglevel",
            "error",
            "-i",
            $"{fileInfo.FullName}",
            "-map",
            "0:v:0",
            "-c:v:0",
            "copy",
            "-a53cc",
            "1",
            "-an",
            "-sn",
            "-f",
            "mpegts",
            "-",
        ];
        List<string> ccextractorCommandline =
        [
            "-",
            "-in=ts",
            "-out=report",
            "-stdout",
            "--no_progress_bar",
        ];

        Log.Information("Reading CCExtractor data using FFmpeg from {FilePath}", fileInfo.Name);
        Task<CommandResult> result;
        try
        {
            result = Command
                .Run(Tools.FfMpeg.GetToolPath(), ffmpegCommandline)
                .PipeTo(Command.Run(_ccExtractor, ccextractorCommandline))
                .Task;
            result.Wait();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception reading CCExtractor data");
            return;
        }

        int ret = result.Result.ExitCode;
        if (ret is not 0 and not 10)
        {
            Log.Error("Error reading CCExtractor data : {Error}", result.Result.StandardError);
            return;
        }

        string outputFile = Path.ChangeExtension(fileInfo.FullName, "ffmpeg.ccextractor.txt");
        Log.Information("Writing CCExtractor data to {OutputFile}", outputFile);
        File.WriteAllText(outputFile, result.Result.StandardOutput);
    }

    private void ReadFFmpegTempCCExtractor(FileInfo fileInfo)
    {
        FileInfo tempFile = new(Path.ChangeExtension(fileInfo.FullName, "temp.ts"));
        string ffmpegCommandline =
            $"-hide_banner -report -loglevel error -i \"{fileInfo.FullName}\" -map 0:v:0 -c:v:0 copy -a53cc 1 -an -sn -y -f mpegts \"{tempFile.FullName}\"";
        string ccextractorCommandline = $"\"{tempFile.FullName}\" -in=ts -12 -out=report";

        Log.Information(
            "Remuxing {FilePath} to temp TS file {TempFilePath}",
            fileInfo.Name,
            tempFile.Name
        );
        int ret = ProcessEx.Execute(
            Tools.FfMpeg.GetToolPath(),
            ffmpegCommandline,
            false,
            0,
            out string _,
            out string error
        );
        if (ret != 0)
        {
            Log.Error("Error writing temp TS file : {Error}", error);
            tempFile.Delete();
            return;
        }

        Log.Information("Reading CCExtractor data from {FilePath}", tempFile.Name);
        ret = ProcessEx.Execute(
            _ccExtractor,
            ccextractorCommandline,
            false,
            0,
            out string output,
            out error
        );
        tempFile.Delete();
        if (ret is not 0 and not 10)
        {
            Log.Error("Error reading CCExtractor data : {Error}", error);
            return;
        }

        string outputFile = Path.ChangeExtension(fileInfo.FullName, "temp.ccextractor.txt");
        Log.Information("Writing CCExtractor data to {OutputFile}", outputFile);
        File.WriteAllText(outputFile, output);
    }
}
