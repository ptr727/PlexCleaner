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
        // Get configuration settings
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

        // Check for new tools and verify existing tools
        if (!Tools.CheckForNewTools() || !Tools.VerifyTools())
        {
            return -1;
        }

        // Process all files in the directory
        if (!FileEx.EnumerateDirectory(_filePath, out List<FileInfo> fileInfoList, out _))
        {
            return -1;
        }

        fileInfoList.ForEach(fileInfo => { });

        return 0;
    }
}
