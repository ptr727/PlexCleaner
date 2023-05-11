using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.Linq;

namespace PlexCleaner;

public class CommandLineOptions
{
    public string SettingsFile { get; set; }
    public List<string> MediaFiles { get; set; }
    public string LogFile { get; set; }
    public bool LogAppend { get; set; }
    public bool LogWarning { get; set; }
    public bool TestSnippets { get; set; }
    public bool TestNoModify { get; set; }
    public bool ReVerify { get; set; }
    public bool Parallel { get; set; }
    public int ThreadCount { get; set; }
    public bool Debug { get; set; }
    public string SchemaFile { get; set; }

    public static int Invoke()
    {
        // TODO: Remove first argument for compatibility
        // https://github.com/dotnet/command-line-api/issues/1781
        RootCommand rootCommand = CreateRootCommand();
        return rootCommand.Invoke(CommandLineStringSplitter.Instance.Split(Environment.CommandLine).ToArray()[1..]);
    }

    public static RootCommand CreateRootCommand()
    {
        // Root command
        RootCommand command = new("Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin");

        // Global options applying to all commands
        command.AddGlobalOption(CreateLogFileOption());
        command.AddGlobalOption(CreateLogAppendOption());
        command.AddGlobalOption(CreateLogWarningOption());
        command.AddGlobalOption(CreateDebugOption());

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

        // Create sidecar files
        command.AddCommand(CreateCreateSidecarCommand());

        // Update sidecar files
        command.AddCommand(CreateUpdateSidecarCommand());

        // Print version information
        command.AddCommand(CreateGetVersionInfoCommand());

        // Print sidecar files
        command.AddCommand(CreateGetSidecarCommand());

        // Print tag-map
        command.AddCommand(CreateGetTagMapCommand());

        // Print media info
        command.AddCommand(CreateGetMediaInfoCommand());

        // Print tool info
        command.AddCommand(CreateGetToolInfoCommand());

        // Create JSON schema
        command.AddCommand(CreateJsonSchemaCommand());

        return command;
    }

    private static Command CreateJsonSchemaCommand()
    {
        // Create settings JSON schema file
        Command command = new Command("createschema")
        {
            Description = "Write settings schema to file",
            Handler = CommandHandler.Create<CommandLineOptions>(Program.CreateJsonSchemaCommand)
        };

        // Schema file name
        command.AddOption(
            new Option<string>("--schemafile")
            {
                Description = "Path to schema file",
                IsRequired = true
            });

        return command;
    }

    private static Command CreateDefaultSettingsCommand()
    {
        // Create default settings file
        Command command = new Command("defaultsettings")
        {
            Description = "Write default values to settings file",
            Handler = CommandHandler.Create<CommandLineOptions>(Program.WriteDefaultSettingsCommand)
        };

        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        return command;
    }

    private static Command CreateCheckForNewToolsCommand()
    {
        // Check for new tools
        Command command = new Command("checkfornewtools")
        {
            Description = "Check for new tool versions and download if newer",
            Handler = CommandHandler.Create<CommandLineOptions>(Program.CheckForNewToolsCommand)
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
            Handler = CommandHandler.Create<CommandLineOptions>(Program.ProcessCommand)
        };

        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        // Create short video clips
        command.AddOption(CreateTestSnippetsOption());

        //  Do not make any modifications
        command.AddOption(CreateTestNoModifyOption());

        // Parallel processing
        command.AddOption(
            new Option<bool>("--parallel")
            {
                Description = "Enable parallel processing"
            });

        // Parallel processing thread count
        command.AddOption(
            new Option<int>("--threadcount")
        {
            Description = "Number of threads to use for parallel processing"
        });

        //  Re-verify
        command.AddOption(
            new Option<bool>("--reverify")
            {
                Description = "Re-verify and repair media files in the VerifyFailed state"
            });

        return command;
    }

    private static Command CreateMonitorCommand()
    {
        // Monitor and process files
        Command command = new("monitor")
        {
            Description = "Monitor for file changes and process changed media files",
            Handler = CommandHandler.Create<CommandLineOptions>(Program.MonitorCommand)
        };

        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        return command;
    }

    private static Command CreateReMuxCommand()
    {
        // Re-Mux files
        Command command = new("remux")
        {
            Description = "Re-Multiplex media files",
            Handler = CommandHandler.Create<CommandLineOptions>(Program.ReMuxCommand)
        };

        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        // Create short video clips
        command.AddOption(CreateTestSnippetsOption());

        //  Do not make any modifications
        command.AddOption(CreateTestNoModifyOption());

        return command;
    }

    private static Command CreateReEncodeCommand()
    {
        // Re-Encode files
        Command command = new("reencode")
        {
            Description = "Re-Encode media files",
            Handler = CommandHandler.Create<CommandLineOptions>(Program.ReEncodeCommand)
        };

        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        // Create short video clips
        command.AddOption(CreateTestSnippetsOption());

        //  Do not make any modifications
        command.AddOption(CreateTestNoModifyOption());

        return command;
    }

    private static Command CreateDeInterlaceCommand()
    {
        // Deinterlace files
        Command command = new("deinterlace")
        {
            Description = "De-Interlace media files",
            Handler = CommandHandler.Create<CommandLineOptions>(Program.DeInterlaceCommand)
        };

        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        // Create short video clips
        command.AddOption(CreateTestSnippetsOption());

        //  Do not make any modifications
        command.AddOption(CreateTestNoModifyOption());

        return command;
    }

    private static Command CreateCreateSidecarCommand()
    {
        // Create sidecar files
        Command command = new("createsidecar")
        {
            Description = "Create new sidecar files",
            Handler = CommandHandler.Create<CommandLineOptions>(Program.CreateSidecarCommand)
        };

        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        return command;
    }

    private static Command CreateGetSidecarCommand()
    {
        // Read sidecar files
        Command command = new("getsidecarinfo")
        {
            Description = "Print sidecar file information",
            Handler = CommandHandler.Create<CommandLineOptions>(Program.GetSidecarCommand)
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
            Handler = CommandHandler.Create<CommandLineOptions>(Program.UpdateSidecarCommand)
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
            Handler = CommandHandler.Create<CommandLineOptions>(Program.GetTagMapCommand)
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
            Handler = CommandHandler.Create<CommandLineOptions>(Program.GetMediaInfoCommand)
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
            Handler = CommandHandler.Create<CommandLineOptions>(Program.GetToolInfoCommand)
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
            Handler = CommandHandler.Create<CommandLineOptions>(Program.RemoveSubtitlesCommand)
        };

        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        return command;
    }

    private static Command CreateGetVersionInfoCommand()
    {
        // Remove subtitles
        Command command = new("getversioninfo")
        {
            Description = "Print application and tools version information",
            Handler = CommandHandler.Create<CommandLineOptions>(Program.GetVersionInfoCommand)
        };

        // Settings file name
        command.AddOption(CreateSettingsFileOption());

        return command;
    }

    private static Option CreateMediaFilesOption()
    {
        // Media files or folders option
        return new Option<List<string>>("--mediafiles")
        {
            Description = "Path to media file or folder",
            IsRequired = true
        };
    }

    private static Option CreateSettingsFileOption()
    {
        // Path to the settings file
        return new Option<string>("--settingsfile")
        {
            Description = "Path to settings file",
            IsRequired = true
        };
    }

    private static Option CreateLogFileOption()
    {
        // Path to the log file
        return new Option<string>("--logfile")
        {
            Description = "Path to log file"
        };
    }

    private static Option CreateLogAppendOption()
    {
        // Append to log vs. overwrite
        return new Option<bool>("--logappend")
        {
            Description = "Append to existing log file"
        };
    }

    private static Option CreateLogWarningOption()
    {
        // Log warnings and errors
        return new Option<bool>("--logwarning")
        {
            Description = "Log warnings and errors only"
        };
    }

    private static Option CreateDebugOption()
    {
        // Wait for debugger to attach
        return new Option<bool>("--debug")
        {
            Description = "Wait for debugger to attach"
        };
    }

    private static Option CreateTestSnippetsOption()
    {
        // Create short video clips
        return  new Option<bool>("--testsnippets")
        {
            Description = "Create short media file clips"
        };
    }

    private static  Option CreateTestNoModifyOption()
    {
        //  Do not make any modifications
        return new Option<bool>("--testnomodify")
        {
            Description = "Do not make any media file modifications"
        };
    }
}
