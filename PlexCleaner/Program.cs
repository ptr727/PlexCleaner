using InsaneGenius.Utilities;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Timers;

namespace PlexCleaner
{
    internal class Program
    {
        private static int Main()
        {
            // Create a timer to keep the system from going to sleep
            using Timer preventSleepTimer = new Timer(30000);
            preventSleepTimer.Elapsed += OnTimedEvent;
            preventSleepTimer.AutoReset = true;
            preventSleepTimer.Start();

            // TODO : Quoted paths ending in a \ fail to parse properly, use our own parser
            // https://github.com/gsscoder/commandline/issues/473
            RootCommand rootCommand = CommandLineOptions.CreateRootCommand();
            int ret = rootCommand.Invoke(CommandLineEx.GetCommandLineArgs());

            // Stop the timer
            preventSleepTimer.Stop();
            preventSleepTimer.Dispose();

            return ret;
        }

        private static void OnTimedEvent(object sender, ElapsedEventArgs e)
        {
            KeepAwake.PreventSleep();
        }

        internal static int WriteDefaultSettingsCommand(CommandLineOptions options)
        {
            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLine($"Writing default settings to \"{options.SettingsFile}\"");

            // Save default config
            ConfigFileJsonSchema.WriteDefaultsToFile(options.SettingsFile);

            return 0;
        }

        internal static int CheckForNewToolsCommand(CommandLineOptions options)
        {
            // Do not verify tools
            Program program = Create(options, false);
            if (program == null)
                return -1;

            // Update tools
            // Make sure that the tools exist
            return Tools.CheckForNewTools() && Tools.VerifyTools() ? 0 : -1;
        }

        internal static int ProcessCommand(CommandLineOptions options)
        {
            Program program = Create(options, true);
            if (program == null)
                return -1;

            if (!program.CreateFileList(options.MediaFiles))
                return -1;

            Process process = new Process();
            return process.ProcessFiles(program.FileInfoList) && 
                   Process.DeleteEmptyFolders(program.FolderList) ? 0 : -1;
        }

        internal static int MonitorCommand(CommandLineOptions options)
        {
            Program program = Create(options, true);
            if (program == null)
                return -1;

            Monitor monitor = new Monitor();
            return monitor.MonitorFolders(options.MediaFiles) ? 0 : -1;
        }

        internal static int ReMuxCommand(CommandLineOptions options)
        {
            Program program = Create(options, true);
            if (program == null)
                return -1;

            if (!program.CreateFileList(options.MediaFiles))
                return -1;

            Process process = new Process();
            return process.ReMuxFiles(program.FileInfoList) ? 0 : -1;
        }

        internal static int ReEncodeCommand(CommandLineOptions options)
        {
            Program program = Create(options, true);
            if (program == null)
                return -1;

            if (!program.CreateFileList(options.MediaFiles))
                return -1;

            return Process.ReEncodeFiles(program.FileInfoList) ? 0 : -1;
        }

        internal static int DeInterlaceCommand(CommandLineOptions options)
        {
            Program program = Create(options, true);
            if (program == null)
                return -1;

            if (!program.CreateFileList(options.MediaFiles))
                return -1;

            return Process.DeInterlaceFiles(program.FileInfoList) ? 0 : -1;
        }

        internal static int VerifyCommand(CommandLineOptions options)
        {
            Program program = Create(options, true);
            if (program == null)
                return -1;

            if (!program.CreateFileList(options.MediaFiles))
                return -1;

            return Process.VerifyFiles(program.FileInfoList) ? 0 : -1;
        }

        internal static int CreateSidecarCommand(CommandLineOptions options)
        {
            Program program = Create(options, true);
            if (program == null)
                return -1;

            if (!program.CreateFileList(options.MediaFiles))
                return -1;

            return Process.CreateSidecarFiles(program.FileInfoList) ? 0 : -1;
        }

        internal static int GetSidecarCommand(CommandLineOptions options)
        {
            Program program = Create(options, true);
            if (program == null)
                return -1;

            if (!program.CreateFileList(options.MediaFiles))
                return -1;

            return Process.GetSidecarFiles(program.FileInfoList) ? 0 : -1;
        }

        internal static int GetTagMapCommand(CommandLineOptions options)
        {
            Program program = Create(options, true);
            if (program == null)
                return -1;

            if (!program.CreateFileList(options.MediaFiles))
                return -1;

            return Process.GetTagMapFiles(program.FileInfoList) ? 0 : -1;
        }

        internal static int GetMediaInfoCommand(CommandLineOptions options)
        {
            Program program = Create(options, true);
            if (program == null)
                return -1;

            if (!program.CreateFileList(options.MediaFiles))
                return -1;

            return Process.GetMediaInfoFiles(program.FileInfoList) ? 0 : -1;
        }

        internal static int GetBitrateInfoCommand(CommandLineOptions options)
        {
            Program program = Create(options, true);
            if (program == null)
                return -1;

            if (!program.CreateFileList(options.MediaFiles))
                return -1;

            return Process.GetBitrateFiles(program.FileInfoList) ? 0 : -1;
        }

        // Add a reference to this class in the event handler arguments
        private void CancelHandlerEx(object s, ConsoleCancelEventArgs e) => CancelHandler(e, this);

        private static void CancelHandler(ConsoleCancelEventArgs e, Program program)
        {
            ConsoleEx.WriteLine("");
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

        private static Program Create(CommandLineOptions options, bool verifyTools)
        {
            // Load config from JSON
            if (!File.Exists(options.SettingsFile))
            {
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLineError($"Error : Settings file not found : \"{options.SettingsFile}\"");
                return null;
            }
            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLine($"Loading settings from : \"{options.SettingsFile}\"");
            ConfigFileJsonSchema config = ConfigFileJsonSchema.FromFile(options.SettingsFile);

            // Set the static options from the loaded settings
            Options = options;
            Config = config;

            // Set the FileEx options
            FileEx.Options.TestNoModify = Options.TestNoModify;
            FileEx.Options.FileRetryCount = config.MonitorOptions.FileRetryCount;
            FileEx.Options.FileRetryWaitTime = config.MonitorOptions.FileRetryWaitTime;
            FileEx.Options.TraceToConsole = true;

            // Share the FileEx Cancel object
            CancelSignal = FileEx.Options.Cancel;
            
            // Create log file
            if (!string.IsNullOrEmpty(options.LogFile))
            {
                // Set file name for internal re-use
                LogFile.FileName = options.LogFile;

                // Clear if not in append mode
                if (!options.LogAppend &&
                    !LogFile.Clear())
                {
                    ConsoleEx.WriteLine("");
                    ConsoleEx.WriteLineError($"Error : Failed to create the logfile : \"{options.LogFile}\"");
                    return null;
                }
                LogFile.Log("");
                LogFile.Log(Environment.CommandLine);
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLine($"Logging output to : \"{options.LogFile}\"");
            }

            // Verify tools
            if (verifyTools &&
                !Tools.VerifyTools())
                return null;

            // Create program instance
            return new Program();
        }

        private void Break()
        {
            // Signal the cancel event
            Cancel();
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
                    DirectoryInfo dirInfo = new DirectoryInfo(fileorfolder);
                    DirectoryInfoList.Add(dirInfo);
                    FolderList.Add(fileorfolder);

                    // Create the file list from the directory
                    ConsoleEx.WriteLine("");
                    ConsoleEx.WriteLine($"Getting files and folders from \"{dirInfo.FullName}\" ...");
                    if (!FileEx.EnumerateDirectory(fileorfolder, out List<FileInfo> fileInfoList, out List<DirectoryInfo> directoryInfoList))
                    {
                        ConsoleEx.WriteLine("");
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
            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLine($"Discovered {DirectoryInfoList.Count} directories and {FileInfoList.Count} files");

            return true;
        }

        public static bool IsCancelledError()
        {
            // There is a race condition between tools exiting on Ctrl-C and reporting an error, and our app's Ctrl-C handler being called
            // In case of a suspected Ctrl-C, yield some time for this handler to be called before testing the state
            return IsCancelled(100);
        }

        public static bool IsCancelled()
        {
            return CancelSignal.WaitForSet(0);
        }

        public static bool IsCancelled(int milliseconds)
        {
            return CancelSignal.WaitForSet(milliseconds);
        }

        public static void Cancel()
        {
            CancelSignal.State = true;
        }

        public static readonly LogFile LogFile = new LogFile();
        public static CommandLineOptions Options { get; set; }
        public static ConfigFileJsonSchema Config { get; set; }

        private static Signal CancelSignal;

        private readonly List<string> FolderList = new List<string>();
        private readonly List<DirectoryInfo> DirectoryInfoList = new List<DirectoryInfo>();
        private readonly List<string> FileList = new List<string>();
        private readonly List<FileInfo> FileInfoList = new List<FileInfo>();
    }
}
