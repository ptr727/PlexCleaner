using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;

namespace PlexCleaner;

// TODO: Migrate to System.CommandLine Beta 5
// https://github.com/dotnet/command-line-api/issues/2576
// https://learn.microsoft.com/en-us/dotnet/standard/commandline/migration-guide-2.0.0-beta5

// TODO: NamingConventionBinder is being deprecated, alternatives:
// https://github.com/mayuki/Cocona
// https://github.com/Cysharp/ConsoleAppFramework
// https://github.com/spectreconsole/spectre.console
// https://github.com/dotmake-build/command-line
// https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-providers#command-line-configuration-provider

// TODO: https://github.com/dotnet/command-line-api/issues/2593

public class CommandLineOptions
{
    // Default delegates for command handlers, overridden in tests
    internal static Func<CommandLineOptions, int> s_removeClosedCaptionsFunc =
        Program.RemoveClosedCaptionsCommand;

    internal static Func<CommandLineOptions, int> s_getToolInfoFunc = Program.GetToolInfoCommand;
    internal static Func<CommandLineOptions, int> s_getMediaInfoFunc = Program.GetMediaInfoCommand;

    internal static Func<CommandLineOptions, int> s_testMediaInfoFunc =
        Program.TestMediaInfoCommand;

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

    public static RootCommand CreateRootCommand()
    {
        // Root command
        // TODO: .Net Format thinks this is a collection initializer?
        // https://github.com/dotnet/command-line-api/issues/2597
#pragma warning disable IDE0028 // Simplify collection initialization
        RootCommand command = new(
            "Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin, etc."
        );
#pragma warning restore IDE0028 // Simplify collection initialization

        // Global options applying to all commands

        // Path to the log file
        command.Options.Add(
            new Option<string>("--logfile") { Description = "Path to log file", Recursive = true }
        );

        // Append to log vs. overwrite
        command.Options.Add(
            new Option<bool>("--logappend")
            {
                Description = "Append to existing log file",
                Recursive = true,
            }
        );

        // Log warnings and errors
        command.Options.Add(
            new Option<bool>("--logwarning")
            {
                Description = "Log warnings and errors only",
                Recursive = true,
            }
        );

        // Wait for debugger to attach
        command.Options.Add(
            new Option<bool>("--debug")
            {
                Description = "Wait for debugger to attach",
                Recursive = true,
            }
        );

        // Commands

        // Create default settings
        command.Subcommands.Add(CreateDefaultSettingsCommand());

        // Check for new tools
        command.Subcommands.Add(CreateCheckForNewToolsCommand());

        // Process files
        command.Subcommands.Add(CreateProcessCommand());

        // Monitor and process files
        command.Subcommands.Add(CreateMonitorCommand());

        // Re-Multiplex files
        command.Subcommands.Add(CreateReMuxCommand());

        // Re-Encode files
        command.Subcommands.Add(CreateReEncodeCommand());

        // De-Interlace files
        command.Subcommands.Add(CreateDeInterlaceCommand());

        // Remove subtitles
        command.Subcommands.Add(CreateRemoveSubtitlesCommand());

        // Remove closed captions
        command.Subcommands.Add(CreateRemoveClosedCaptionsCommand());

        // Verify files
        command.Subcommands.Add(CreateVerifyCommand());

        // Create sidecar files
        command.Subcommands.Add(CreateCreateSidecarCommand());

        // Update sidecar files
        command.Subcommands.Add(CreateUpdateSidecarCommand());

        // Print version information
        command.Subcommands.Add(CreateGetVersionInfoCommand());

        // Print sidecar files
        command.Subcommands.Add(CreateGetSidecarInfoCommand());

        // Print tag-map
        command.Subcommands.Add(CreateGetTagMapCommand());

        // Print media info
        command.Subcommands.Add(CreateGetMediaInfoCommand());

        // Print tool info
        command.Subcommands.Add(CreateGetToolInfoCommand());

        // Test media info
        command.Subcommands.Add(CreateTestMediaInfoCommand());

        // Create JSON schema
        command.Subcommands.Add(CreateCreateSchemaCommand());

        return command;
    }

    private static Command CreateCreateSchemaCommand()
    {
        // Create settings JSON schema file
        Command command = new("createschema")
        {
            Description = "Write JSON settings schema to file",
            Action = CommandHandler.Create(s_createSchemaFunc),
        };

        // Schema file name
        command.Options.Add(
            new Option<string>("--schemafile")
            {
                Description = "Path to schema file",
                Required = true,
            }
        );

        return command;
    }

    private static Command CreateDefaultSettingsCommand()
    {
        // Create default settings file
        Command command = new("defaultsettings")
        {
            Description = "Create JSON configuration file using default settings",
            Action = CommandHandler.Create(s_defaultSettingsFunc),
        };

        // Settings file name
        command.Options.Add(CreateSettingsFileOption());

        return command;
    }

    private static Command CreateCheckForNewToolsCommand()
    {
        // Check for new tools
        Command command = new("checkfornewtools")
        {
            Description = "Check for new tool versions and download if newer",
            Action = CommandHandler.Create(s_checkForNewToolsFunc),
        };

        // Settings file name
        command.Options.Add(CreateSettingsFileOption());

        return command;
    }

    private static Command CreateProcessCommand()
    {
        // Process files
        Command command = new("process")
        {
            Description = "Process media files",
            Action = CommandHandler.Create(s_processFunc),
        };

        // Process options
        CreateProcessCommandOptions(command);

        // Results file name
        command.Options.Add(
            new Option<string>("--resultsfile") { Description = "Path to results file" }
        );

        // Create short video clips
        command.Options.Add(
            new Option<bool>("--testsnippets") { Description = "Create short media file clips" }
        );

        return command;
    }

    private static void CreateProcessCommandOptions(Command command)
    {
        // Settings file name
        command.Options.Add(CreateSettingsFileOption());

        // Media files or folders option
        command.Options.Add(CreateMediaFilesOption());

        // Parallel processing
        command.Options.Add(CreateParallelOption());

        // Parallel processing thread count
        command.Options.Add(CreateThreadCountOption());

        // Scan only part of the file
        command.Options.Add(CreateQuickScanOption());
    }

    private static Command CreateMonitorCommand()
    {
        // Monitor and process files
        Command command = new("monitor")
        {
            Description = "Monitor for file changes and process changed media files",
            Action = CommandHandler.Create(s_monitorFunc),
        };

        // Process options
        CreateProcessCommandOptions(command);

        //  Pre-process
        command.Options.Add(
            new Option<bool>("--preprocess") { Description = "Pre-process all monitored folders" }
        );

        return command;
    }

    private static Command CreateReMuxCommand()
    {
        // Re-Mux files
        Command command = new("remux")
        {
            Description = "Conditionally re-multiplex media files",
            Action = CommandHandler.Create(s_reMuxFunc),
        };

        // Settings file name
        command.Options.Add(CreateSettingsFileOption());

        // Media files or folders option
        command.Options.Add(CreateMediaFilesOption());

        // Scan only part of the file
        command.Options.Add(CreateQuickScanOption());

        return command;
    }

    private static Command CreateReEncodeCommand()
    {
        // Re-Encode files
        Command command = new("reencode")
        {
            Description = "Conditionally re-encode media files",
            Action = CommandHandler.Create(s_reEncodeFunc),
        };

        // Settings file name
        command.Options.Add(CreateSettingsFileOption());

        // Media files or folders option
        command.Options.Add(CreateMediaFilesOption());

        // Scan only part of the file
        command.Options.Add(CreateQuickScanOption());

        return command;
    }

    private static Command CreateDeInterlaceCommand()
    {
        // DeInterlace files
        Command command = new("deinterlace")
        {
            Description = "De-interlace the video stream if interlaced",
            Action = CommandHandler.Create(s_deInterlaceFunc),
        };

        // Settings file name
        command.Options.Add(CreateSettingsFileOption());

        // Media files or folders option
        command.Options.Add(CreateMediaFilesOption());

        // Scan only part of the file
        command.Options.Add(CreateQuickScanOption());

        return command;
    }

    private static Command CreateVerifyCommand()
    {
        // Verify files
        Command command = new("verify")
        {
            Description = "Verify media container and stream integrity",
            Action = CommandHandler.Create(s_verifyFunc),
        };

        // Settings file name
        command.Options.Add(CreateSettingsFileOption());

        // Media files or folders option
        command.Options.Add(CreateMediaFilesOption());

        // Scan only part of the file
        command.Options.Add(CreateQuickScanOption());

        return command;
    }

    private static Command CreateCreateSidecarCommand()
    {
        // Create sidecar files
        Command command = new("createsidecar")
        {
            Description = "Create new sidecar files",
            Action = CommandHandler.Create(s_createSidecarFunc),
        };

        // Settings file name
        command.Options.Add(CreateSettingsFileOption());

        // Media files or folders option
        command.Options.Add(CreateMediaFilesOption());

        return command;
    }

    private static Command CreateGetSidecarInfoCommand()
    {
        // Read sidecar files
        Command command = new("getsidecarinfo")
        {
            Description = "Print sidecar file information",
            Action = CommandHandler.Create(s_getSidecarInfoFunc),
        };

        // Settings file name
        command.Options.Add(CreateSettingsFileOption());

        // Media files or folders option
        command.Options.Add(CreateMediaFilesOption());

        return command;
    }

    private static Command CreateUpdateSidecarCommand()
    {
        // Create sidecar files
        Command command = new("updatesidecar")
        {
            Description = "Create or update sidecar files",
            Action = CommandHandler.Create(s_updateSidecarFunc),
        };

        // Settings file name
        command.Options.Add(CreateSettingsFileOption());

        // Media files or folders option
        command.Options.Add(CreateMediaFilesOption());

        return command;
    }

    private static Command CreateGetTagMapCommand()
    {
        // Create tag-map
        Command command = new("gettagmap")
        {
            Description = "Print media file attribute mappings",
            Action = CommandHandler.Create(s_getTagMapFunc),
        };

        // Settings file name
        command.Options.Add(CreateSettingsFileOption());

        // Media files or folders option
        command.Options.Add(CreateMediaFilesOption());

        return command;
    }

    private static Command CreateGetMediaInfoCommand()
    {
        // Print media info
        Command command = new("getmediainfo")
        {
            Description = "Print media file information",
            Action = CommandHandler.Create(s_getMediaInfoFunc),
        };

        // Settings file name
        command.Options.Add(CreateSettingsFileOption());

        // Media files or folders option
        command.Options.Add(CreateMediaFilesOption());

        return command;
    }

    private static Command CreateTestMediaInfoCommand()
    {
        // Print media info
        Command command = new("testmediainfo")
        {
            Description = "Test parsing media file information",
            Action = CommandHandler.Create(s_testMediaInfoFunc),
        };

        // Settings file name
        command.Options.Add(CreateSettingsFileOption());

        // Media files or folders option
        command.Options.Add(CreateMediaFilesOption());

        return command;
    }

    private static Command CreateGetToolInfoCommand()
    {
        // Print tool info
        Command command = new("gettoolinfo")
        {
            Description = "Print media tool information",
            Action = CommandHandler.Create(s_getToolInfoFunc),
        };

        // Settings file name
        command.Options.Add(CreateSettingsFileOption());

        // Media files or folders option
        command.Options.Add(CreateMediaFilesOption());

        return command;
    }

    private static Command CreateRemoveSubtitlesCommand()
    {
        // Remove subtitles
        Command command = new("removesubtitles")
        {
            Description = "Remove all subtitle tracks",
            Action = CommandHandler.Create(s_removeSubtitlesFunc),
        };

        // Settings file name
        command.Options.Add(CreateSettingsFileOption());

        // Media files or folders option
        command.Options.Add(CreateMediaFilesOption());

        return command;
    }

    private static Command CreateGetVersionInfoCommand()
    {
        // Get version information
        Command command = new("getversioninfo")
        {
            Description = "Print application and tools version information",
            Action = CommandHandler.Create(s_getVersionInfoFunc),
        };

        // Settings file name
        command.Options.Add(CreateSettingsFileOption());

        return command;
    }

    private static Command CreateRemoveClosedCaptionsCommand()
    {
        // Remove closed captions
        Command command = new("removeclosedcaptions")
        {
            Description = "Remove closed captions from video stream",
            Action = CommandHandler.Create(s_removeClosedCaptionsFunc),
        };

        // Settings file name
        command.Options.Add(CreateSettingsFileOption());

        // Media files or folders option
        command.Options.Add(CreateMediaFilesOption());

        // Parallel processing
        command.Options.Add(CreateParallelOption());

        // Parallel processing thread count
        command.Options.Add(CreateThreadCountOption());

        // Scan only part of the file
        command.Options.Add(CreateQuickScanOption());

        return command;
    }

    private static Option<List<string>> CreateMediaFilesOption() =>
        // Media files or folders option
        new("--mediafiles") { Description = "Path to media file or folder", Required = true };

    private static Option<string> CreateSettingsFileOption() =>
        // Path to the settings file
        new("--settingsfile") { Description = "Path to settings file", Required = true };

    private static Option<bool> CreateParallelOption() =>
        // Parallel processing
        new("--parallel") { Description = "Enable parallel file processing" };

    private static Option<int> CreateThreadCountOption() =>
        // Parallel processing thread count
        new("--threadcount") { Description = "Number of threads for parallel file processing" };

    private static Option<bool> CreateQuickScanOption() =>
        // Scan only parts of the file
        new("--quickscan") { Description = "Scan only part of the file" };
}
