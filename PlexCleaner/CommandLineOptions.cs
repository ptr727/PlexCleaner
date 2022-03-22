using System;
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
    public bool TestSnippets { get; set; }
    public bool TestNoModify { get; set; }
    public int ReProcess { get; set; }

    public static RootCommand CreateRootCommand()
    {
        // Root command and global options
        RootCommand command = new("Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin");
        AddGlobalOptions(command);

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

        // Deinterlace files
        command.AddCommand(CreateDeInterlaceCommand());

        // Write sidecar files
        command.AddCommand(CreateCreateSidecarCommand());

        // Read sidecar files
        command.AddCommand(CreateGetSidecarInfoCommand());

        // Create tag-map
        command.AddCommand(CreateGetTagMapCommand());

        // Print media info
        command.AddCommand(CreateGetMediaInfoCommand());

        // Print tool info
        command.AddCommand(CreateGetToolInfoCommand());

        // Remove subtitles
        command.AddCommand(CreateRemoveSubtitlesCommand());

        return command;
    }

    private static void AddGlobalOptions(RootCommand command)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        // Path to the settings file, required
        command.AddGlobalOption(
            new Option<string>("--settingsfile")
            {
                Description = "Path to settings file",
                IsRequired = true
            });

        // Path to the log file, optional
        command.AddGlobalOption(
            new Option<string>("--logfile")
            {
                Description = "Path to log file",
                IsRequired = false
            });

        // Append to log vs. overwrite, optional
        command.AddGlobalOption(
            new Option<bool>("--logappend")
            {
                Description = "Append to the log file vs. default overwrite",
                IsRequired = false
            });
    }

    private static Command CreateDefaultSettingsCommand()
    {
        // Create default settings file
        return new Command("defaultsettings")
        {
            Description = "Write default values to settings file",
            Handler = CommandHandler.Create<CommandLineOptions>(Program.WriteDefaultSettingsCommand)
        };
    }

    private static Command CreateCheckForNewToolsCommand()
    {
        // Check for new tools
        return new Command("checkfornewtools")
        {
            Description = "Check for and download new tools",
            Handler = CommandHandler.Create<CommandLineOptions>(Program.CheckForNewToolsCommand)
        };
    }

    private static Command CreateProcessCommand()
    {
        // Process files
        Command command = new("process")
        {
            Description = "Process media files",
            Handler = CommandHandler.Create<CommandLineOptions>(Program.ProcessCommand)
        };

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        // Create short video clips, optional
        command.AddOption(
            new Option<bool>("--testsnippets")
            {
                Description = "Create short video clips, useful during testing",
                IsRequired = false
            });

        //  Do not make any modifications, optional
        command.AddOption(
            new Option<bool>("--testnomodify")
            {
                Description = "Do not make any modifications, useful during testing",
                IsRequired = false
            });

        //  Re-process level, optional
        command.AddOption(
            new Option<int>("--reprocess")
            {
                Description = "Re-process level, 0 = none (default), 1 = some, 2 = all",
                IsRequired = false
            });

        return command;
    }

    private static Command CreateMonitorCommand()
    {
        // Monitor and process files
        Command command = new("monitor")
        {
            Description = "Monitor and process media file changes in folders",
            Handler = CommandHandler.Create<CommandLineOptions>(Program.MonitorCommand)
        };

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

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

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

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        return command;
    }

    private static Command CreateDeInterlaceCommand()
    {
        // Deinterlace files
        Command command = new("deinterlace")
        {
            Description = "Deinterlace media files",
            Handler = CommandHandler.Create<CommandLineOptions>(Program.DeInterlaceCommand)
        };

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

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

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        return command;
    }

    private static Command CreateGetSidecarInfoCommand()
    {
        // Read sidecar files
        Command command = new("getsidecarinfo")
        {
            Description = "Print sidecar file attribute information",
            Handler = CommandHandler.Create<CommandLineOptions>(Program.GetSidecarInfoCommand)
        };

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        return command;
    }

    private static Command CreateGetTagMapCommand()
    {
        // Create tag-map
        Command command = new("gettagmap")
        {
            Description = "Print attribute tag-map created from media files",
            Handler = CommandHandler.Create<CommandLineOptions>(Program.GetTagMapCommand)
        };

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        return command;
    }

    private static Command CreateGetMediaInfoCommand()
    {
        // Print media info
        Command command = new("getmediainfo")
        {
            Description = "Print media file attribute information",
            Handler = CommandHandler.Create<CommandLineOptions>(Program.GetMediaInfoCommand)
        };

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        return command;
    }

    private static Command CreateGetToolInfoCommand()
    {
        // Print tool info
        Command command = new("gettoolinfo")
        {
            Description = "Print tool file attribute information",
            Handler = CommandHandler.Create<CommandLineOptions>(Program.GetToolInfoCommand)
        };

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        return command;
    }

    private static Command CreateRemoveSubtitlesCommand()
    {
        // Remove subtitles
        Command command = new("removesubtitles")
        {
            Description = "Remove all subtitles",
            Handler = CommandHandler.Create<CommandLineOptions>(Program.RemoveSubtitlesCommand)
        };

        // Media files or folders option
        command.AddOption(CreateMediaFilesOption());

        return command;
    }

    private static Option CreateMediaFilesOption()
    {
        // Media files or folders option
        return new Option<List<string>>("--mediafiles")
        {
            Description = "Media file or folder to process, repeat for multiples",
            IsRequired = true
        };
    }
}
