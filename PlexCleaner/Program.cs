using InsaneGenius.Utilities;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;

namespace PlexCleaner
{
    internal class Program
    {
        private static int Main()
        {
            // TODO : Quoted paths ending in a \ fail to parse properly, use our own parser
            // https://github.com/gsscoder/commandline/issues/473
            RootCommand rootCommand = CreateCommandLineOptions();
            return rootCommand.Invoke(CommandLineEx.GetCommandlineArgs());
        }

        private static RootCommand CreateCommandLineOptions()
        {
            // Root command and global options
            RootCommand rootCommand = new RootCommand("Utility to optimize media files for DirectPlay on Plex.");

            // Path to the settings file must always be specified
            rootCommand.AddOption(
                new Option<string>("--settings")
                {
                    Description = "Path to settings file.",
                    Required = true
                });

            // Path to the log file is optional
            rootCommand.AddOption(
                new Option<string>("--log")
                {
                    Description = "Path to log file.",
                    Required = false
                });

            // Write defaults to settings file
            rootCommand.AddCommand(
                new Command("writedefaults")
                {
                    Description = "Write default values to settings file.",
                    Handler = CommandHandler.Create<string, string>(WriteDefaultsCommand)
                });

            // Check for new tools
            rootCommand.AddCommand(
                new Command("checkfornewtools")
                {
                    Description = "Check for new tools and download if available.",
                    Handler = CommandHandler.Create<string, string>(CheckForNewToolsCommand)
                });

            // Files or folders option
            Option filesOption =
                new Option<List<string>>("--files")
                {
                    Description = "List of files or folders.",
                    Required = true
                };

            // Process files
            Command processCommand = 
                new Command("process")
                {
                    Description = "Process media files.",
                    Handler = CommandHandler.Create<string, string, List<string>>(ProcessCommand)
                };
            processCommand.AddOption(filesOption);
            rootCommand.AddCommand(processCommand);

            // Monitor and process files
            Command monitorCommand =
                new Command("monitor")
                {
                    Description = "Monitor for changes in folders and process any changed files.",
                    Handler = CommandHandler.Create<string, string, List<string>>(MonitorCommand)
                };
            monitorCommand.AddOption(filesOption);
            rootCommand.AddCommand(monitorCommand);

            // Re-Mux files
            Command remuxCommand =
                new Command("remux")
                {
                    Description = "Re-Multiplex media files",
                    Handler = CommandHandler.Create<string, string, List<string>>(ReMuxCommand)
                };
            remuxCommand.AddOption(filesOption);
            rootCommand.AddCommand(remuxCommand);

            // Re-Encode files
            Command reencodeCommand =
                new Command("reencode")
                {
                    Description = "Re-Encode media files.",
                    Handler = CommandHandler.Create<string, string, List<string>>(ReEncodeCommand)
                };
            reencodeCommand.AddOption(filesOption);
            rootCommand.AddCommand(reencodeCommand);

            // De-interlace files
            Command deinterlaceCommand =
                new Command("deinterlace")
                {
                    Description = "De-Interlace media files.",
                    Handler = CommandHandler.Create<string, string, List<string>>(DeInterlaceCommand)
                };
            deinterlaceCommand.AddOption(filesOption);
            rootCommand.AddCommand(deinterlaceCommand);

            // Write sidecar files
            Command writesidecarCommand =
                new Command("writesidecar")
                {
                    Description = "Write sidecar files for media files.",
                    Handler = CommandHandler.Create<string, string, List<string>>(WriteSidecarCommand)
                };
            writesidecarCommand.AddOption(filesOption);
            rootCommand.AddCommand(writesidecarCommand);

            // Create tag-map
            Command createtagmapCommand =
                new Command("createtagmap")
                {
                    Description = "Create a tag-map from media files.",
                    Handler = CommandHandler.Create<string, string, List<string>>(CreateTagMapCommand)
                };
            createtagmapCommand.AddOption(filesOption);
            rootCommand.AddCommand(createtagmapCommand);

            // Show media info
            Command printinfoCommand =
                new Command("printinfo")
                {
                    Description = "Print info for media files.",
                    Handler = CommandHandler.Create<string, string, List<string>>(PrintInfoCommand)
                };
            printinfoCommand.AddOption(filesOption);
            rootCommand.AddCommand(printinfoCommand);

            return rootCommand;
        }

        private static int WriteDefaultsCommand(string settings, string log)
        {
            ConsoleEx.WriteLine($"Writing default settings to \"{settings}\"");

            // Save default config
            Config config = new Config();
            Config.ToFile(settings, config);

            return 0;
        }

        private static int CheckForNewToolsCommand(string settings, string log)
        {
            Program program = Create(settings, log);
            if (program == null)
                return -1;

            return Tools.CheckForNewTools() ? 0 : -1;
        }

        private static int ProcessCommand(string settings, string log, List<string> files)
        {
            Program program = Create(settings, log);
            if (program == null)
                return -1;

            if (!program.CreateFileList(files))
                return -1;

            Process process = new Process();
            return process.ProcessFiles(program.FileInfoList) && Process.DeleteEmptyFolders(program.FolderList) ? 0 : -1;
        }

        private static int MonitorCommand(string settings, string log, List<string> files)
        {
            Program program = Create(settings, log);
            if (program == null)
                return -1;

            Monitor monitor = new Monitor();
            return monitor.MonitorFolders(files) ? 0 : -1;
        }

        private static int ReMuxCommand(string settings, string log, List<string> files)
        {
            Program program = Create(settings, log);
            if (program == null)
                return -1;

            if (!program.CreateFileList(files))
                return -1;

            Process process = new Process();
            return process.ReMuxFiles(program.FileInfoList) ? 0 : -1;
        }

        private static int ReEncodeCommand(string settings, string log, List<string> files)
        {
            Program program = Create(settings, log);
            if (program == null)
                return -1;

            if (!program.CreateFileList(files))
                return -1;

            Process process = new Process();
            return process.ReEncodeFiles(program.FileInfoList) ? 0 : -1;
        }

        private static int DeInterlaceCommand(string settings, string log, List<string> files)
        {
            Program program = Create(settings, log);
            if (program == null)
                return -1;

            if (!program.CreateFileList(files))
                return -1;

            Process process = new Process();
            return process.DeInterlaceFiles(program.FileInfoList) ? 0 : -1;
        }

        private static int WriteSidecarCommand(string settings, string log, List<string> files)
        {
            Program program = Create(settings, log);
            if (program == null)
                return -1;

            if (!program.CreateFileList(files))
                return -1;

            Process process = new Process();
            return process.WriteSidecarFiles(program.FileInfoList) ? 0 : -1;
        }

        private static int CreateTagMapCommand(string settings, string log, List<string> files)
        {
            Program program = Create(settings, log);
            if (program == null)
                return -1;

            if (!program.CreateFileList(files))
                return -1;

            Process process = new Process();
            return process.CreateTagMapFiles(program.FileInfoList) ? 0 : -1;
        }

        private static int PrintInfoCommand(string settings, string log, List<string> files)
        {
            Program program = Create(settings, log);
            if (program == null)
                return -1;

            if (!program.CreateFileList(files))
                return -1;

            Process process = new Process();
            return process.PrintInfo(program.FileInfoList) ? 0 : -1;
        }

        // Add a reference to this class in the event handler arguments
        private void CancelHandlerEx(object s, ConsoleCancelEventArgs e) => CancelHandler(e, this);

        private static void CancelHandler(ConsoleCancelEventArgs e, Program program)
        {
            ConsoleEx.WriteLineError("Cancel key pressed");
            e.Cancel = true;

            // Signal the cancel event
            // We could signal Cancel directly now that it is static
            program.Break();
        }

        private Program()
        {
            // Register cancel handler
            Console.CancelKeyPress += CancelHandlerEx;
        }

        ~Program()
        {
            // Unregister cancel handler
            Console.CancelKeyPress -= CancelHandlerEx;
        }

        private static Program Create(string settingsFile, string logFile)
        {
            // Load config from JSON
            if (!File.Exists(settingsFile))
            {
                ConsoleEx.WriteLineError($"Settings file not found : \"{settingsFile}\"");
                return null;
            }
            ConsoleEx.WriteLine($"Loading settings from : \"{settingsFile}\"");
            Config config = Config.FromFile(settingsFile);

            // Set the static options from the loded settings
            Tools.Options = config.ToolsOptions;
            Process.Options = config.ProcessOptions;
            Monitor.Options = config.MonitorOptions;
            Convert.Options = config.ConvertOptions;

            // Set the FileEx options
            FileEx.Options.TestNoModify = config.ProcessOptions.TestNoModify;
            FileEx.Options.FileRetryCount = config.MonitorOptions.FileRetryCount;
            FileEx.Options.FileRetryWaitTime = config.MonitorOptions.FileRetryWaitTime;
            FileEx.Options.TraceToConsole = true;

            // Share the FileEx Cancel object
            Cancel = FileEx.Options.Cancel;
            
            // Create log file
            if (!string.IsNullOrEmpty(logFile))
            {
                LogFile.FileName = logFile;
                if (!LogFile.Clear())
                {
                    ConsoleEx.WriteLineError($"Failed to create logfile : \"{logFile}\"");
                    return null;
                }
                LogFile.Log(Environment.CommandLine);
                ConsoleEx.WriteLine($"Logging output to  : \"{logFile}\"");
            }

            // Make sure that the tools folder exists
            if (!Tools.VerifyTools(out ToolInfoJsonSchema toolInfo))
                return null;
            ConsoleEx.WriteLine($"Using Tools from : \"{Tools.GetToolsRoot()}\"");

            // Set tool version numbers
            FfMpegTool.Version = toolInfo.Tools.Find(t => t.Tool.Equals(nameof(FfMpegTool), StringComparison.OrdinalIgnoreCase)).Version;
            MediaInfoTool.Version = toolInfo.Tools.Find(t => t.Tool.Equals(nameof(MediaInfoTool), StringComparison.OrdinalIgnoreCase)).Version;
            MkvTool.Version = toolInfo.Tools.Find(t => t.Tool.Equals(nameof(MkvTool), StringComparison.OrdinalIgnoreCase)).Version;

            // Create program
            return new Program();
        }

        private void Break()
        {
            // Signal the cancel event
            Cancel.State = true;
        }

        private bool CreateFileList(List<string> files)
        {
            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLine("Creating file and folder list ...");

            // Trim quotes from input paths
            files = files.Select(file => file.Trim('"')).ToList();

            // Process all entries
            foreach (string fileorfolder in files)
            {
                // File or a directory
                FileAttributes fileAttributes;
                try
                {
                    fileAttributes = File.GetAttributes(fileorfolder);
                }
                catch (Exception e)
                {
                    ConsoleEx.WriteLineError(e);
                    ConsoleEx.WriteLineError($"Failed to get file attributes \"{fileorfolder}\"");
                    return false;
                }

                if (fileAttributes.HasFlag(FileAttributes.Directory))
                {
                    // Add this directory
                    DirectoryInfoList.Add(new DirectoryInfo(fileorfolder));
                    FolderList.Add(fileorfolder);

                    // Create the file list from the directory
                    if (!FileEx.EnumerateDirectory(fileorfolder, out List<FileInfo> fileInfoList, out List<DirectoryInfo> directoryInfoList))
                    {
                        ConsoleEx.WriteLineError($"Failed to enumerate directory \"{fileorfolder}\"");
                        return false;
                    }
                    FileInfoList.AddRange(fileInfoList);
                    DirectoryInfoList.AddRange(directoryInfoList);
                }
                else
                {
                    // Add this file
                    FileList.Add(fileorfolder);
                    FileInfoList.Add(new FileInfo(fileorfolder));
                }
            }

            // Report
            ConsoleEx.WriteLine($"Discovered {DirectoryInfoList.Count} directories and {FileInfoList.Count} files");
            ConsoleEx.WriteLine("");

            return true;
        }

        public static Signal Cancel { get; set; }
        public static readonly LogFile LogFile = new LogFile();

        private readonly List<string> FolderList = new List<string>();
        private readonly List<DirectoryInfo> DirectoryInfoList = new List<DirectoryInfo>();
        private readonly List<string> FileList = new List<string>();
        private readonly List<FileInfo> FileInfoList = new List<FileInfo>();
    }
}
