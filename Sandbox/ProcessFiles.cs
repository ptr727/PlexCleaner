using System.Diagnostics;
using InsaneGenius.Utilities;
using PlexCleaner;
using Serilog;

namespace Sandbox;

// Settings:
/*
{
    "ProcessFiles": {
        "SettingsFile": "PlexCleaner.json",
        "FilePath": "D:\\Test"
    }
}
*/

public class ProcessFiles
{
    private readonly string _settingsFile;
    private readonly string _filePath;

    public ProcessFiles(Program program)
    {
        // Get settings
        Dictionary<string, string>? settings = program.GetSettingsDictionary(nameof(ProcessFiles));
        Debug.Assert(settings is not null);
        _settingsFile = settings["SettingsFile"];
        _filePath = settings["FilePath"];
    }

    public int Test()
    {
        // Load config from JSON or use defaults
        string? settingsFile = Program.GetSettingsFilePath(_settingsFile);
        if (settingsFile != null)
        {
            Log.Information("Loading settings from : {SettingsFile}", settingsFile);
            ConfigFileJsonSchema configSchema = ConfigFileJsonSchema.FromFile(settingsFile);
            if (configSchema == null || !configSchema.VerifyValues())
            {
                Log.Error("Failed to load settings : {FileName}", settingsFile);
                return -1;
            }
            PlexCleaner.Program.Config = configSchema;
        }

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
        List<string> fileList = [.. fileInfoList.Select(fileInfo => fileInfo.FullName)];

        // Verify closed captions
        VerifyClosedCaptions(fileList);

        return 0;
    }

    public void VerifyClosedCaptions(List<string> fileList)
    {
        // Speed up processing
        PlexCleaner.Program.Options.TestSnippets = false;
        PlexCleaner.Program.Options.Parallel = true;
        PlexCleaner.Program.Options.ThreadCount = 0;
        Program.SetRuntimeOptions();

        Lock resultLock = new();
        List<string> resultList = [];
        _ = ProcessDriver.ProcessFiles(
            fileList,
            nameof(VerifyClosedCaptions),
            true,
            fileName =>
            {
                // Get media information
                ProcessFile processFile = new(fileName);
                if (!processFile.GetMediaInfo())
                {
                    return false;
                }

                // Must have video stream
                if (processFile.FfProbeInfo.Video.Count == 0)
                {
                    Log.Warning("No video stream found : {FileName}", processFile.FileInfo.Name);
                    return false;
                }

                /*
                [mpegts @ 0x5713ff02ab40] first pts and dts value must be set
                av_interleaved_write_frame(): Invalid data found when processing input
                https://superuser.com/questions/710008/how-to-get-rid-of-ffmpeg-pts-has-no-value-error

                [matroska @ 0x604976cd9dc0] Can't write packet with unknown timestamp
                av_interleaved_write_frame(): Invalid argument
                https://stackoverflow.com/questions/66013483/cant-write-packet-with-unknown-timestamp-av-interleaved-write-frame-invalid

                -fflags +genpts
                */

                // Write a snippet of the file to a temp file
                FileInfo tempFile = new(Path.ChangeExtension(fileName, "snip.temp"));
                string ffmpegCommandline =
                    $"-hide_banner -no_stats -loglevel error -t 30 -fflags +genpts -i \"{fileName}\" -map 0:v:0 -c:v:0 copy -a53cc 1 -an -sn -y -f mpegts \"{tempFile}\"";
                Log.Information(
                    "Remuxing {FilePath} to temp file {TempFilePath}",
                    processFile.FileInfo.Name,
                    tempFile
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
                    // Error
                    Log.Error("Error writing temp file : {Error}", error);
                    tempFile.Delete();
                    return false;
                }

                // Get closed captions
                if (
                    !Tools.FfProbe.GetSubCcPacketInfo(
                        tempFile.FullName,
                        out List<FfMpegToolJsonSchema.Packet> packetList
                    )
                )
                {
                    // Error
                    tempFile.Delete();
                    return false;
                }
                tempFile.Delete();

                // Any packets means there are subtitles present in the video stream
                bool ffProbe = packetList.Count > 0;
                bool sideCar = processFile.State.HasFlag(SidecarFile.StatesType.ClearedCaptions);
                if (
                    ffProbe != sideCar
                    && processFile.State.HasFlag(SidecarFile.StatesType.Verified)
                )
                {
                    Log.Warning(
                        "Closed Captions state does not match : ffProbe: {FFprobe}, Sidecar: {Sidecar} : {FileName}",
                        ffProbe,
                        sideCar,
                        fileName
                    );
                    lock (resultLock)
                    {
                        resultList.Add(fileName);
                    }
                }

                return true;
            }
        );

        resultList.Sort();
        resultList.ForEach(fileName =>
        {
            Log.Information("Closed Captions state mismatch : {FileName}", fileName);
        });
    }
}
