﻿using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;

namespace PlexCleaner;

public class CommandLineOptions
{
    public string SettingsFile { get; set; }
    public List<string> MediaFiles { get; set; }
    public string LogFile { get; set; }
    public bool LogAppend { get; set; }
    public bool LogWarning { get; set; }
    public bool TestSnippets { get; set; }
    public bool Parallel { get; set; }
    public int ThreadCount { get; set; }
    public bool Debug { get; set; }
    public string SchemaFile { get; set; }
    public bool PreProcess { get; set; }
    public string ResultsFile { get; set; }
    public bool QuickScan { get; set; }

    // TODO: How to override --version?
    // https://github.com/dotnet/command-line-api/issues/2009
    // https://github.com/dotnet/command-line-api/issues/1691

    public static RootCommand CreateRootCommand()
    {
        // Root command
        RootCommand command = new(
            "Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin, etc."
        );

        // Global options applying to all commands

        // Path to the log file
        command.AddGlobalOption(
            new Option<string>("--logfile") { Description = "Path to log file" }
        );

        // Append to log vs. overwrite
        command.AddGlobalOption(
            new Option<bool>("--logappend") { Description = "Append to existing log file" }
        );

        // Log warnings and errors
        command.AddGlobalOption(
            new Option<bool>("--logwarning") { Description = "Log warnings and errors only" }
        );

        // Wait for debugger to attach
        command.AddGlobalOption(
            new Option<bool>("--debug") { Description = "Wait for debugger to attach" }
        );

        // Commands

        // Create default settings
        command.AddCommand(CreateDefaultSettingsCommand());

        // Check for new tools
        command.AddCommand(CreateCheckForNewToolsCommand());

        // Process files
        command.AddCommand(CreateProcessCommand());

        // Monitor and process files
        command.AddCommand(CreateMonitorCommand());

        // Re-Multiplex files
        command.AddCommand(CreateReMuxCommand());

        // Re-Encode files
        command.AddCommand(CreateReEncodeCommand());

        // De-Interlace files
        command.AddCommand(CreateDeInterlaceCommand());

        // Remove subtitles
        command.AddCommand(CreateRemoveSubtitlesCommand());

        // Remove closed captions
        command.AddCommand(CreateRemoveClosedCaptionsCommand());

        // Verify files
        command.AddCommand(CreateVerifyCommand());

        // Create sidecar files
        command.AddCommand(CreateCreateSidecarCommand());

        // Update sidecar files
        command.AddCommand(CreateUpdateSidecarCommand());

        // Print version information
        command.AddCommand(CreateGetVersionInfoCommand());

        // Print sidecar files
        command.AddCommand(CreateGetSidecarInfoCommand());

        // Print tag-map
        command.AddCommand(CreateGetTagMapCommand());

        // Print media info
        command.AddCommand(CreateGetMediaInfoCommand());

        // Print tool info
        command.AddCommand(CreateGetToolInfoCommand());

        // Create JSON schema
        command.AddCommand(CreateCreateSchemaCommand());

        return command;
    }

    private static Command CreateCreateSchemaCommand()
    {
        // Create settings JSON schema file
        Command command = new("createschema")
        {
            Description = "Write settings schema to file",
            Handler = CommandHandler.Create(s_createSchemaFunc),
        };

        // Schema file name
        command.AddOption(
            new Option<string>("--schemafile")
            {
                Description = "Path to schema file",
                IsRequired = true,
            }
        );

        return command;
    }

    private static Command CreateDefaultSettingsCommand()
    {
        // Create default settings file
        Command command = new("defaultsettings")
        {
            Description = "Write default values to settings file",
            Handler = CommandHandler.Create(s_defaultSettingsFunc),
        };

        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        return command;
    }

    private static Command CreateCheckForNewToolsCommand()
    {
        // Check for new tools
        Command command = new("checkfornewtools")
        {
            Description = "Check for new tool versions and download if newer",
            Handler = CommandHandler.Create(s_checkForNewToolsFunc),
        };

        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        return command;
    }

    private static Command CreateProcessCommand()
    {
        // Process files
        Command command = new("process")
        {
            Description = "Process media files",
            Handler = CommandHandler.Create(s_processFunc),
        };

        // Process options
        CreateProcessCommandOptions(command);

        // Results file name
        command.AddOption(
            new Option<string>("--resultsfile") { Description = "Path to results file" }
        );

        // Create short video clips
        command.AddOption(
            new Option<bool>("--testsnippets") { Description = "Create short media file clips" }
        );

        return command;
    }

    private static void CreateProcessCommandOptions(Command command)
    {
        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        // Parallel processing
        command.AddOption(CreateParallelOption());

        // Parallel processing thread count
        command.AddOption(CreateThreadCountOption());

        // Scan only part of the file
        command.AddOption(CreateQuickScanOption());
    }

    private static Command CreateMonitorCommand()
    {
        // Monitor and process files
        Command command = new("monitor")
        {
            Description = "Monitor for file changes and process changed media files",
            Handler = CommandHandler.Create(s_monitorFunc),
        };

        // Process options
        CreateProcessCommandOptions(command);

        //  Pre-process
        command.AddOption(
            new Option<bool>("--preprocess") { Description = "Pre-process all monitored folders" }
        );

        return command;
    }

    private static Command CreateReMuxCommand()
    {
        // Re-Mux files
        Command command = new("remux")
        {
            Description = "Re-Multiplex media files",
            Handler = CommandHandler.Create(s_reMuxFunc),
        };

        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        // Scan only part of the file
        command.AddOption(CreateQuickScanOption());

        return command;
    }

    private static Command CreateReEncodeCommand()
    {
        // Re-Encode files
        Command command = new("reencode")
        {
            Description = "Re-Encode media files",
            Handler = CommandHandler.Create(s_reEncodeFunc),
        };

        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        // Scan only part of the file
        command.AddOption(CreateQuickScanOption());

        return command;
    }

    private static Command CreateDeInterlaceCommand()
    {
        // DeInterlace files
        Command command = new("deinterlace")
        {
            Description = "De-Interlace media files",
            Handler = CommandHandler.Create(s_deInterlaceFunc),
        };

        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        // Scan only part of the file
        command.AddOption(CreateQuickScanOption());

        return command;
    }

    private static Command CreateVerifyCommand()
    {
        // Verify files
        Command command = new("verify")
        {
            Description = "Verify media files",
            Handler = CommandHandler.Create(s_verifyFunc),
        };

        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        // Scan only part of the file
        command.AddOption(CreateQuickScanOption());

        return command;
    }

    private static Command CreateCreateSidecarCommand()
    {
        // Create sidecar files
        Command command = new("createsidecar")
        {
            Description = "Create new sidecar files",
            Handler = CommandHandler.Create(s_createSidecarFunc),
        };

        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        return command;
    }

    private static Command CreateGetSidecarInfoCommand()
    {
        // Read sidecar files
        Command command = new("getsidecarinfo")
        {
            Description = "Print sidecar file information",
            Handler = CommandHandler.Create(s_getSidecarInfoFunc),
        };

        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        return command;
    }

    private static Command CreateUpdateSidecarCommand()
    {
        // Create sidecar files
        Command command = new("updatesidecar")
        {
            Description = "Update existing sidecar files",
            Handler = CommandHandler.Create(s_updateSidecarFunc),
        };

        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        return command;
    }

    private static Command CreateGetTagMapCommand()
    {
        // Create tag-map
        Command command = new("gettagmap")
        {
            Description = "Print media information tag-map",
            Handler = CommandHandler.Create(s_getTagMapFunc),
        };

        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        return command;
    }

    private static Command CreateGetMediaInfoCommand()
    {
        // Print media info
        Command command = new("getmediainfo")
        {
            Description = "Print media information using sidecar files",
            Handler = CommandHandler.Create(s_getMediaInfoFunc),
        };

        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        return command;
    }

    private static Command CreateGetToolInfoCommand()
    {
        // Print tool info
        Command command = new("gettoolinfo")
        {
            Description = "Print media information using media tools",
            Handler = CommandHandler.Create(s_getToolInfoFunc),
        };

        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        return command;
    }

    private static Command CreateRemoveSubtitlesCommand()
    {
        // Remove subtitles
        Command command = new("removesubtitles")
        {
            Description = "Remove subtitles from media files",
            Handler = CommandHandler.Create(s_removeSubtitlesFunc),
        };

        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        return command;
    }

    private static Command CreateGetVersionInfoCommand()
    {
        // Get version information
        Command command = new("getversioninfo")
        {
            Description = "Print application and tools version information",
            Handler = CommandHandler.Create(s_getVersionInfoFunc),
        };

        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        return command;
    }

    private static Command CreateRemoveClosedCaptionsCommand()
    {
        // Remove closed captions
        Command command = new("removeclosedcaptions")
        {
            Description = "Remove closed captions from media files",
            Handler = CommandHandler.Create(s_removeClosedCaptionsFunc),
        };

        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        // Parallel processing
        command.AddOption(CreateParallelOption());

        // Parallel processing thread count
        command.AddOption(CreateThreadCountOption());

        // Scan only part of the file
        command.AddOption(CreateQuickScanOption());

        return command;
    }

    private static Option<List<string>> CreateMediaFilesOption() =>
        // Media files or folders option
        new("--mediafiles") { Description = "Path to media file or folder", IsRequired = true };

    private static Option<string> CreateSettingsFileOption() =>
        // Path to the settings file
        new("--settingsfile") { Description = "Path to settings file", IsRequired = true };

    private static Option<bool> CreateParallelOption() =>
        // Parallel processing
        new("--parallel") { Description = "Enable parallel file processing" };

    private static Option<int> CreateThreadCountOption() =>
        // Parallel processing thread count
        new("--threadcount") { Description = "Number of threads for parallel file processing" };

    private static Option<bool> CreateQuickScanOption() =>
        // Scan only parts of the file
        new("--quickscan") { Description = "Scan only part of the file" };

    // Default delegates for command handlers, overridden in tests
    internal static Func<CommandLineOptions, int> s_removeClosedCaptionsFunc =
        Program.RemoveClosedCaptionsCommand;
    internal static Func<CommandLineOptions, int> s_getToolInfoFunc = Program.GetToolInfoCommand;
    internal static Func<CommandLineOptions, int> s_getMediaInfoFunc = Program.GetMediaInfoCommand;
    internal static Func<CommandLineOptions, int> s_getTagMapFunc = Program.GetTagMapCommand;
    internal static Func<CommandLineOptions, int> s_updateSidecarFunc =
        Program.UpdateSidecarCommand;
    internal static Func<CommandLineOptions, int> s_getSidecarInfoFunc =
        Program.GetSidecarInfoCommand;
    internal static Func<CommandLineOptions, int> s_createSidecarFunc =
        Program.CreateSidecarCommand;
    internal static Func<CommandLineOptions, int> s_verifyFunc = Program.VerifyCommand;
    internal static Func<CommandLineOptions, int> s_deInterlaceFunc = Program.DeInterlaceCommand;
    internal static Func<CommandLineOptions, int> s_reEncodeFunc = Program.ReEncodeCommand;
    internal static Func<CommandLineOptions, int> s_reMuxFunc = Program.ReMuxCommand;
    internal static Func<CommandLineOptions, int> s_monitorFunc = Program.MonitorCommand;
    internal static Func<CommandLineOptions, int> s_processFunc = Program.ProcessCommand;
    internal static Func<CommandLineOptions, int> s_checkForNewToolsFunc =
        Program.CheckForNewToolsCommand;
    internal static Func<CommandLineOptions, int> s_defaultSettingsFunc =
        Program.WriteDefaultSettingsCommand;
    internal static Func<CommandLineOptions, int> s_createSchemaFunc =
        Program.CreateJsonSchemaCommand;
    internal static Func<CommandLineOptions, int> s_getVersionInfoFunc =
        Program.GetVersionInfoCommand;
    internal static Func<CommandLineOptions, int> s_removeSubtitlesFunc =
        Program.RemoveSubtitlesCommand;
}
