using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;

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
            RootCommand rootCommand = new RootCommand("Utility to optimize media files for DirectPlay on Plex.");
            AddGlobalOptions(rootCommand);

            // Create default settings
            rootCommand.AddCommand(CreateDefaultSettingsCommand());

            // Check for new tools
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
            rootCommand.AddCommand(CreateWriteSidecarCommand());

            // Create tag-map
            rootCommand.AddCommand(CreateCreateTagMapCommand());

            // Print media info
            rootCommand.AddCommand(CreatePrintMediaInfoCommand());

            return rootCommand;
        }

        private static void AddGlobalOptions(RootCommand rootCommand)
        {
            if (rootCommand == null)
                throw new ArgumentNullException(nameof(rootCommand));

            // Path to the settings file, required
            rootCommand.AddOption(
                new Option<string>("--settingsfile")
                {
                    Description = "Path to settings file.",
                    Required = true
                });

            // Path to the log file, optional
            rootCommand.AddOption(
                new Option<string>("--logfile")
                {
                    Description = "Path to log file.",
                    Required = false
                });

            // Append to log vs. overwrite, optional
            rootCommand.AddOption(
                new Option<bool>("--logappend")
                {
                    Description = "Append to the log file vs. default overwrite.",
                    Required = false
                });
        }

        private static Command CreateDefaultSettingsCommand()
        {
            // Create default settings file
            return new Command("defaultsettings")
            {
                Description = "Write default values to settings file.",
                Handler = CommandHandler.Create<CommandLineOptions>(Program.WriteDefaultSettingsCommand)
            };
        }

        private static Command CreateCheckForNewToolsCommand()
        {
            // Check for new tools
            return new Command("checkfornewtools")
                {
                    Description = "Check for new tools and download if available.",
                    Handler = CommandHandler.Create<CommandLineOptions>(Program.CheckForNewToolsCommand)
                };
        }

        private static Command CreateProcessCommand()
        {
            // Process files
            Command processCommand =
                new Command("process")
                {
                    Description = "Process media files.",
                    Handler = CommandHandler.Create<CommandLineOptions>(Program.ProcessCommand)
                };

            // Media files or folders option
            processCommand.AddOption(CreateMediaFilesOption());

            // Create short video clips, optional
            processCommand.AddOption(
                new Option<bool>("--testsnippets")
                {
                    Description = "Create short video clips, useful during testing.",
                    Required = false
                });

            //  Do not make any modifications, optional
            processCommand.AddOption(
                new Option<bool>("--testnomodify")
                {
                    Description = "Do not make any modifications, useful during testing.",
                    Required = false
                });

            return processCommand;
        }

        private static Command CreateMonitorCommand()
        {
            // Monitor and process files
            Command monitorCommand =
                new Command("monitor")
                {
                    Description = "Monitor for changes in folders and process any changed files.",
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
                    Description = "Re-Encode media files.",
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
                    Description = "De-Interlace media files.",
                    Handler = CommandHandler.Create<CommandLineOptions>(Program.DeInterlaceCommand)
                };

            // Media files or folders option
            deinterlaceCommand.AddOption(CreateMediaFilesOption());

            return deinterlaceCommand;
        }

        private static Command CreateWriteSidecarCommand()
        {
            // Write sidecar files
            Command writesidecarCommand =
                new Command("writesidecar")
                {
                    Description = "Write sidecar files for media files.",
                    Handler = CommandHandler.Create<CommandLineOptions>(Program.WriteSidecarCommand)
                };

            // Media files or folders option
            writesidecarCommand.AddOption(CreateMediaFilesOption());

            return writesidecarCommand;
        }

        private static Command CreateCreateTagMapCommand()
        {
            // Create tag-map
            Command createtagmapCommand =
                new Command("createtagmap")
                {
                    Description = "Create a tag-map from media files.",
                    Handler = CommandHandler.Create<CommandLineOptions>(Program.CreateTagMapCommand)
                };

            // Media files or folders option
            createtagmapCommand.AddOption(CreateMediaFilesOption());

            return createtagmapCommand;
        }

        private static Command CreatePrintMediaInfoCommand()
        {
            // Print media info
            Command printmediainfoCommand =
                new Command("printmediainfo")
                {
                    Description = "Print info for media files.",
                    Handler = CommandHandler.Create<CommandLineOptions>(Program.PrintMediaInfoCommand)
                };

            // Media files or folders option
            printmediainfoCommand.AddOption(CreateMediaFilesOption());

            return printmediainfoCommand;
        }

        private static Command CreateVerifyCommand()
        {
            // Print media info
            Command printmediainfoCommand =
                new Command("verify")
                {
                    Description = "Verify media files.",
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
                    Description = "List of media files or folders.",
                    Required = true
                };
        }
    }
}
