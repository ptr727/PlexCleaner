using InsaneGenius.Utilities;
using Medallion.Shell;
using Serilog;

namespace Sandbox;

public class ClosedCaptions
{
    private static readonly string s_ffProbe = @"C:\Users\piete\Source\Repos\ptr727\PlexCleaner\PlexCleaner\bin\Debug\net9.0\Tools\FfMpeg\bin\ffprobe.exe";
    private static readonly string s_ffMpeg = @"C:\Users\piete\Source\Repos\ptr727\PlexCleaner\PlexCleaner\bin\Debug\net9.0\Tools\FfMpeg\bin\ffmpeg.exe";
    private static readonly string s_mediaInfo = @"C:\Users\piete\Source\Repos\ptr727\PlexCleaner\PlexCleaner\bin\Debug\net9.0\Tools\MediaInfo\MediaInfo.exe";
    private static readonly string s_ccExtractor = @"C:\Users\piete\Downloads\CCExtractor_win_portable\ccextractorwinfull.exe";
    private static readonly string s_testDir = @"D:\CC";
    private static readonly string[] s_sourceArray = [".mkv", ".mp4", ".ts", ".avi", ".mov", ".mpg"];

    public static int Test()
    {
        if (!FileEx.EnumerateDirectory(s_testDir, out List<FileInfo> fileInfoList, out _))
        {
            return -1;
        }

        fileInfoList.ForEach(fileInfo =>
        {
            if (s_sourceArray.Contains(fileInfo.Extension))
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
    private static string EscapeMovieFilterPath(string path) => path.Replace(@"\", @"/").Replace(@":", @"\\:").Replace(@"'", @"\\\'").Replace(@",", @"\\\,");

    private static void ReadEIA608(FileInfo fileInfo)
    {
        string escapedPath = EscapeMovieFilterPath(fileInfo.FullName);
        string commandline = $"-loglevel error -f lavfi -i \"movie={escapedPath},readeia608\" -show_entries frame=best_effort_timestamp_time,duration_time:frame_tags=lavfi.readeia608.0.line,lavfi.readeia608.0.cc,lavfi.readeia608.1.line,lavfi.readeia608.1.cc -print_format csv";
        // $"-loglevel error -f lavfi -i \"movie={escapedPath},readeia608\" -show_entries frame=best_effort_timestamp_time,duration_time,side_data_list:frame_tags=lavfi.readeia608.0.line,lavfi.readeia608.0.cc,lavfi.readeia608.1.line,lavfi.readeia608.1.cc -print_format:compact=1 json";
        // $"-loglevel error -f lavfi -i \"movie={escapedPath},readeia608\" -show_entries frame=best_effort_timestamp_time:frame_tags=lavfi.readeia608.0.line,lavfi.readeia608.0.cc,lavfi.readeia608.1.line,lavfi.readeia608.1.cc -print_format csv";
        // $"-loglevel error -f lavfi -i \"movie={escapedPath},readeia608\" -show_entries frame=best_effort_timestamp_time,tags,side_data_list -print_format json";

        Log.Logger.Information("Reading EIA608 data from {FilePath}", fileInfo.Name);
        int ret = ProcessEx.Execute(s_ffProbe, commandline, false, 0, out string output, out string error);
        if (ret != 0)
        {
            Log.Logger.Error("Error reading EIA608 data : {Error}", error);
            return;
        }

        string outputFile = Path.ChangeExtension(fileInfo.FullName, "readeia608.csv");
        Log.Logger.Information("Writing EIA608 data to {OutputFile}", outputFile);
        File.WriteAllText(outputFile, output);
    }

    private static void ReadSubCC(FileInfo fileInfo)
    {
        string escapedPath = EscapeMovieFilterPath(fileInfo.FullName);
        string commandline = $"-loglevel error -select_streams s:0 -f lavfi -i \"movie={escapedPath}[out0+subcc]\" -show_packets -print_format json";
        // $"-loglevel error -f lavfi -i \"movie={escapedPath}[out0+subcc]\" -show_frames -print_format json";

        Log.Logger.Information("Reading SubCC data from {FilePath}", fileInfo.Name);
        int ret = ProcessEx.Execute(s_ffProbe, commandline, false, 0, out string output, out string error);
        if (ret != 0)
        {
            Log.Logger.Error("Error reading SubCC data : {Error}", error);
            return;
        }

        string outputFile = Path.ChangeExtension(fileInfo.FullName, "subcc.json");
        Log.Logger.Information("Writing SubCC data to {OutputFile}", outputFile);
        File.WriteAllText(outputFile, output);
    }

    private static void WriteSubCC(FileInfo fileInfo)
    {
        string escapedPath = EscapeMovieFilterPath(fileInfo.FullName);
        FileInfo outputFileInfo = new(Path.ChangeExtension(fileInfo.FullName, "subcc.srt"));
        string commandline = $"-abort_on empty_output -y -f lavfi -i \"movie={escapedPath}[out0+subcc]\" -map 0:s -c:s srt \"{outputFileInfo.FullName}\"";

        Log.Logger.Information("Extracting SubCC data from {FilePath} to {OutputFile}", fileInfo.Name, outputFileInfo.Name);
        int ret = ProcessEx.Execute(s_ffMpeg, commandline, false, 0, out string _, out string error);
        if (ret != 0)
        {
            Log.Logger.Error("Error writing SubCC data : {Error}", error);
        }
        Log.Logger.Information("SubCC data written to {OutputFile}", outputFileInfo.Name);
    }

    private static void ReadMediaInfo(FileInfo fileInfo)
    {
        string commandline = $"--Output=JSON \"{fileInfo.FullName}\"";

        Log.Logger.Information("Reading MediaInfo data from {FilePath}", fileInfo.Name);
        int ret = ProcessEx.Execute(s_mediaInfo, commandline, false, 0, out string output, out string error);
        if (ret != 0)
        {
            Log.Logger.Error("Error reading MediaInfo data : {Error}", error);
            return;
        }

        string outputFile = Path.ChangeExtension(fileInfo.FullName, "mediainfo.json");
        Log.Logger.Information("Writing MediaInfo data to {OutputFile}", outputFile);
        File.WriteAllText(outputFile, output);
    }

    private static void ReadTrimMediaInfo(FileInfo fileInfo)
    {
        FileInfo tempFile = new(Path.ChangeExtension(fileInfo.FullName, "temp.ts"));
        string ffmpegCommandline = $"-hide_banner -report -loglevel error -i \"{fileInfo.FullName}\" -map 0:v:0 -c:v:0 copy -a53cc 1 -an -sn -y -t 30 -f mpegts \"{tempFile.FullName}\"";
        string mediainfoCommandline = $"--Output=JSON \"{tempFile.FullName}\"";

        Log.Logger.Information("Remuxing {FilePath} to temp short TS file {TempFilePath}", fileInfo.Name, tempFile.Name);
        int ret = ProcessEx.Execute(s_ffMpeg, ffmpegCommandline, false, 0, out string _, out string error);
        if (ret != 0)
        {
            Log.Logger.Error("Error writing temp TS file : {Error}", error);
            tempFile.Delete();
            return;
        }

        Log.Logger.Information("Reading MediaInfo data from {FilePath}", tempFile.Name);
        ret = ProcessEx.Execute(s_mediaInfo, mediainfoCommandline, false, 0, out string output, out error);
        tempFile.Delete();
        if (ret != 0)
        {
            Log.Logger.Error("Error reading MediaInfo data : {Error}", error);
            return;
        }

        string outputFile = Path.ChangeExtension(fileInfo.FullName, "trim.mediainfo.json");
        Log.Logger.Information("Writing MediaInfo data to {OutputFile}", outputFile);
        File.WriteAllText(outputFile, output);
    }

    private static void ReadCCExtractor(FileInfo fileInfo)
    {
        // https://github.com/CCExtractor/ccextractor/blob/master/src/lib_ccx/ccx_common_common.h

        string commandline = $"-12 -out=report \"{fileInfo.FullName}\"";

        Log.Logger.Information("Reading CCExtractor data from {FilePath}", fileInfo.Name);
        int ret = ProcessEx.Execute(s_ccExtractor, commandline, false, 0, out string output, out string error);
        if (ret is not 0 and not 10)
        {
            Log.Logger.Error("Error reading CCExtractor data : {Error}", error);
            return;
        }

        string outputFile = Path.ChangeExtension(fileInfo.FullName, "ccextractor.txt");
        Log.Logger.Information("Writing CCExtractor data to {OutputFile}", outputFile);
        File.WriteAllText(outputFile, output);
    }

    private static void ReadFFmpegPipeCCExtractor(FileInfo fileInfo)
    {
        List<string> ffmpegCommandline = ["-hide_banner", "-report", "-loglevel", "error", "-i", $"{fileInfo.FullName}", "-map", "0:v:0", "-c:v:0", "copy", "-a53cc", "1", "-an", "-sn", "-f", "mpegts", "-"];
        List<string> ccextractorCommandline = ["-", "-in=ts", "-out=report", "-stdout", "--no_progress_bar"];

        Log.Logger.Information("Reading CCExtractor data using FFmpeg from {FilePath}", fileInfo.Name);
        Task<CommandResult> result;
        try
        {
            result = Command.Run(s_ffMpeg, ffmpegCommandline)
                .PipeTo(Command.Run(s_ccExtractor, ccextractorCommandline))
                .Task;
            result.Wait();
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Exception reading CCExtractor data");
            return;
        }

        int ret = result.Result.ExitCode;
        if (ret is not 0 and not 10)
        {
            Log.Logger.Error("Error reading CCExtractor data : {Error}", result.Result.StandardError);
            return;
        }

        string outputFile = Path.ChangeExtension(fileInfo.FullName, "ffmpeg.ccextractor.txt");
        Log.Logger.Information("Writing CCExtractor data to {OutputFile}", outputFile);
        File.WriteAllText(outputFile, result.Result.StandardOutput);
    }

    private static void ReadFFmpegTempCCExtractor(FileInfo fileInfo)
    {
        FileInfo tempFile = new(Path.ChangeExtension(fileInfo.FullName, "temp.ts"));
        string ffmpegCommandline = $"-hide_banner -report -loglevel error -i \"{fileInfo.FullName}\" -map 0:v:0 -c:v:0 copy -a53cc 1 -an -sn -y -f mpegts \"{tempFile.FullName}\"";
        string ccextractorCommandline = $"\"{tempFile.FullName}\" -in=ts -12 -out=report";

        Log.Logger.Information("Remuxing {FilePath} to temp TS file {TempFilePath}", fileInfo.Name, tempFile.Name);
        int ret = ProcessEx.Execute(s_ffMpeg, ffmpegCommandline, false, 0, out string _, out string error);
        if (ret != 0)
        {
            Log.Logger.Error("Error writing temp TS file : {Error}", error);
            tempFile.Delete();
            return;
        }

        Log.Logger.Information("Reading CCExtractor data from {FilePath}", tempFile.Name);
        ret = ProcessEx.Execute(s_ccExtractor, ccextractorCommandline, false, 0, out string output, out error);
        tempFile.Delete();
        if (ret is not 0 and not 10)
        {
            Log.Logger.Error("Error reading CCExtractor data : {Error}", error);
            return;
        }

        string outputFile = Path.ChangeExtension(fileInfo.FullName, "temp.ccextractor.txt");
        Log.Logger.Information("Writing CCExtractor data to {OutputFile}", outputFile);
        File.WriteAllText(outputFile, output);
    }
}

