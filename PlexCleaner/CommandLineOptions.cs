using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Runtime.InteropServices;

namespace PlexCleaner
{
    public class CommandLineOptions
    {
        public string SettingsFile { get; set; }
        public List<string> MediaFiles { get; set; }
        public string LogFile { get; set; }
        public bool LogAppend { get; set; }
        public bool TestSnippets { get; set; }
        public bool TestNoModify { get; set; }

        public static RootCommand CreateRootCommand()
        {
            // Root command and global options
            RootCommand rootCommand = new RootCommand("Utility to optimize media files for DirectPlay on Plex");
            AddGlobalOptions(rootCommand);

            // Create default settings
            rootCommand.AddCommand(CreateDefaultSettingsCommand());

            // Check for new tools
            // Windows only
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                rootCommand.AddCommand(CreateCheckForNewToolsCommand());

            // Process files
            rootCommand.AddCommand(CreateProcessCommand());

            // Monitor and process files
            rootCommand.AddCommand(CreateMonitorCommand());

            // Re-multiplex files
            rootCommand.AddCommand(CreateReMuxCommand());

            // Re-Encode files
            rootCommand.AddCommand(CreateReEncodeCommand());

            // De-interlace files
            rootCommand.AddCommand(CreateDeInterlaceCommand());

            // Verify files
            rootCommand.AddCommand(CreateVerifyCommand());

            // Write sidecar files
            rootCommand.AddCommand(CreateCreateSidecarCommand());

            // Read sidecar files
            rootCommand.AddCommand(CreateGetSidecarCommand());

            // Create tag-map
            rootCommand.AddCommand(CreateGetTagMapCommand());

            // Print media info
            rootCommand.AddCommand(CreateGetMediaInfoCommand());

            // Calculate bitrate info
            rootCommand.AddCommand(CreateGetBitrateInfoCommand());

            return rootCommand;
        }

        private static void AddGlobalOptions(RootCommand rootCommand)
        {
            if (rootCommand == null)
                throw new ArgumentNullException(nameof(rootCommand));

            // Path to the settings file, required
            // IsRequired flag is ignored on global options
            // https://github.com/dotnet/command-line-api/issues/1138
            rootCommand.AddOption(
                new Option<string>("--settingsfile")
                {
                    Description = "Path to settings file",
                    IsRequired = true
                });

            // Path to the log file, optional
            rootCommand.AddOption(
                new Option<string>("--logfile")
                {
                    Description = "Path to log file",
                    IsRequired = false
                });

            // Append to log vs. overwrite, optional
            rootCommand.AddOption(
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
            Command processCommand =
                new Command("process")
                {
                    Description = "Process media files",
                    Handler = CommandHandler.Create<CommandLineOptions>(Program.ProcessCommand)
                };

            // Media files or folders option
            processCommand.AddOption(CreateMediaFilesOption());

            // Create short video clips, optional
            processCommand.AddOption(
                new Option<bool>("--testsnippets")
                {
                    Description = "Create short video clips, useful during testing",
                    IsRequired = false
                });

            //  Do not make any modifications, optional
            processCommand.AddOption(
                new Option<bool>("--testnomodify")
                {
                    Description = "Do not make any modifications, useful during testing",
                    IsRequired = false
                });

            return processCommand;
        }

        private static Command CreateMonitorCommand()
        {
            // Monitor and process files
            Command monitorCommand =
                new Command("monitor")
                {
                    Description = "Monitor and process media file changes in folders",
                    Handler = CommandHandler.Create<CommandLineOptions>(Program.MonitorCommand)
                };

            // Media files or folders option
            monitorCommand.AddOption(CreateMediaFilesOption());

            return monitorCommand;
        }

        private static Command CreateReMuxCommand()
        {
            // Re-Mux files
            Command remuxCommand =
                new Command("remux")
                {
                    Description = "Re-Multiplex media files",
                    Handler = CommandHandler.Create<CommandLineOptions>(Program.ReMuxCommand)
                };

            // Media files or folders option
            remuxCommand.AddOption(CreateMediaFilesOption());

            return remuxCommand;
        }

        private static Command CreateReEncodeCommand()
        {
            // Re-Encode files
            Command reencodeCommand =
                new Command("reencode")
                {
                    Description = "Re-Encode media files",
                    Handler = CommandHandler.Create<CommandLineOptions>(Program.ReEncodeCommand)
                };

            // Media files or folders option
            reencodeCommand.AddOption(CreateMediaFilesOption());

            return reencodeCommand;
        }

        private static Command CreateDeInterlaceCommand()
        {
            // De-interlace files
            Command deinterlaceCommand =
                new Command("deinterlace")
                {
                    Description = "De-Interlace media files",
                    Handler = CommandHandler.Create<CommandLineOptions>(Program.DeInterlaceCommand)
                };

            // Media files or folders option
            deinterlaceCommand.AddOption(CreateMediaFilesOption());

            return deinterlaceCommand;
        }

        private static Command CreateCreateSidecarCommand()
        {
            // Create sidecar files
            Command createsidecarCommand =
                new Command("createsidecar")
                {
                    Description = "Create sidecar files",
                    Handler = CommandHandler.Create<CommandLineOptions>(Program.CreateSidecarCommand)
                };

            // Media files or folders option
            createsidecarCommand.AddOption(CreateMediaFilesOption());

            return createsidecarCommand;
        }

        private static Command CreateGetSidecarCommand()
        {
            // Read sidecar files
            Command getsidecarCommand =
                new Command("getsidecar")
                {
                    Description = "Print sidecar file attribute information",
                    Handler = CommandHandler.Create<CommandLineOptions>(Program.GetSidecarCommand)
                };

            // Media files or folders option
            getsidecarCommand.AddOption(CreateMediaFilesOption());

            return getsidecarCommand;
        }

        private static Command CreateGetTagMapCommand()
        {
            // Create tag-map
            Command gettagmapCommand =
                new Command("gettagmap")
                {
                    Description = "Print attribute tag-map created from media files",
                    Handler = CommandHandler.Create<CommandLineOptions>(Program.GetTagMapCommand)
                };

            // Media files or folders option
            gettagmapCommand.AddOption(CreateMediaFilesOption());

            return gettagmapCommand;
        }

        private static Command CreateGetMediaInfoCommand()
        {
            // Print media info
            Command getmediainfoCommand =
                new Command("getmediainfo")
                {
                    Description = "Print media file attribute information",
                    Handler = CommandHandler.Create<CommandLineOptions>(Program.GetMediaInfoCommand)
                };

            // Media files or folders option
            getmediainfoCommand.AddOption(CreateMediaFilesOption());

            return getmediainfoCommand;
        }

        private static Command CreateGetBitrateInfoCommand()
        {
            // Print media info
            Command bitrateinfoCommand =
                new Command("getbitrateinfo")
                {
                    Description = "Print media file bitrate information",
                    Handler = CommandHandler.Create<CommandLineOptions>(Program.GetBitrateInfoCommand)
                };

            // Media files or folders option
            bitrateinfoCommand.AddOption(CreateMediaFilesOption());

            return bitrateinfoCommand;
        }

        private static Command CreateVerifyCommand()
        {
            // Print media info
            Command printmediainfoCommand =
                new Command("verify")
                {
                    Description = "Verify media files",
                    Handler = CommandHandler.Create<CommandLineOptions>(Program.VerifyCommand)
                };

            // Media files or folders option
            printmediainfoCommand.AddOption(CreateMediaFilesOption());

            return printmediainfoCommand;
        }

        private static Option CreateMediaFilesOption()
        {
            // Media files or folders option
            return new Option<List<string>>("--mediafiles")
                {
                    Description = "List of media files or folders",
                    IsRequired = true
                };
        }
    }
}
