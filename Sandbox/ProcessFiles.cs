using System.Diagnostics;
using InsaneGenius.Utilities;
using PlexCleaner;
using Serilog;

namespace Sandbox;

// Settings:
/*
{
    "ProcessFiles": {
        "settingsfile": "plexcleaner.json",
        "filepath": "D:\\Test"
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
        _settingsFile = settings["settingsfile"];
        _filePath = settings["filepath"];
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
        PlexCleaner.Program.Options.TestSnippets = true;
        PlexCleaner.Program.Options.Parallel = true;
        PlexCleaner.Program.Options.ThreadCount = 0;
        Program.SetRuntimeOptions();

        _ = PlexCleaner.Process.ProcessFilesDriver(
            fileList,
            nameof(VerifyClosedCaptions),
            fileName =>
            {
                // Handle only MKV files
                if (!MkvMergeTool.IsMkvFile(fileName))
                {
                    return true;
                }

                // Get media information
                ProcessFile processFile = new(fileName);
                if (!processFile.GetMediaInfo())
                {
                    return false;
                }

                // Get closed captions
                if (
                    !Tools.FfProbe.GetSubCcPacketInfo(
                        fileName,
                        out List<FfMpegToolJsonSchema.Packet> packetList
                    )
                )
                {
                    // Error
                    return false;
                }

                // Any packets means there are subtitles present in the video stream
                bool ffProbe = packetList.Count > 0;
                bool sideCar = processFile.State.HasFlag(SidecarFile.StatesType.ClearedCaptions);
                if (ffProbe != sideCar)
                {
                    Log.Warning(
                        "Closed Captions state does not match : ffProbe: {FFprobe}, Sidecar: {Sidecar} : {FileName}",
                        ffProbe,
                        sideCar,
                        fileName
                    );
                }

                return true;
            }
        );
    }
}
