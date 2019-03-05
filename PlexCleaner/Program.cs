using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CommandLine;
using CommandLine.Text;
using InsaneGenius.Utilities;
using Microsoft.Extensions.Configuration;

namespace PlexCleaner
{
    public class Program
    {
        // Options: Process, ReMux, ReEncode, WriteSidecar, CreateTagMap, CheckForTools, Monitor, Folders, Files
        // Example input:
        // PlexCleaner.exe --Process --Folders "c:\foo" "d:\bar"
        // PlexCleaner.exe --Process --Files "c:\foo\bar.mp4" "d:\bar\foo.mkv"
        // PlexCleaner.exe --Monitor --Folders "c:\foo" "d:\bar"
        // PlexCleaner.exe --ReMux --Folders "c:\foo" "d:\bar"
        // PlexCleaner.exe --ReEncode --Folders "c:\foo" "d:\bar"
        // PlexCleaner.exe --Process --Monitor --Folders "..\..\..\Test\One" "..\..\..\Test\Two"
        // PlexCleaner.exe --Process --Monitor --Folders "\\STORAGE\Media\Series\Series" "\\STORAGE\Media\Movies\Movies"
        private static int Main()
        {
            // Load options from appsettings.json
            const string settingsfile = "appSettings.json";
            string settingspath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            ConsoleEx.WriteLine($"Loading settings from : {Path.Combine(settingspath, settingsfile)}");
            Options options = new Options();
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .SetBasePath(settingspath)
                .AddJsonFile(settingsfile);
            IConfiguration config = builder.Build();
            config.Bind(options);

            // Make sure that the tools folder exists
            if (!Tools.VerifyTools())
            {
                ConsoleEx.WriteLineError($"Tools folder or 7-Zip does not exist : {Tools.GetToolsRoot()}");
                return -1;
            }
            ConsoleEx.WriteLine($"Using Tools from : {Tools.GetToolsRoot()}");

            // TODO : Find a .NET Core replacement
            // https://github.com/dotnet/corefx/issues/16596
            // Redirect debug output to console
            // TODO : Disable modal UI for Assert, AssertUiEnabled
            // TextWriterTraceListener textWriterTraceListener = new TextWriterTraceListener(Console.Error);
            // Debug.Listeners.Add(textWriterTraceListener);

            // Parse the commandline arguments
            Parser parser = new Parser(with =>
            {
                with.CaseSensitive = false;
                with.HelpWriter = Console.Error;
            });
            // TODO : Quoted paths ending in a \ fail to parse properly, use our own parser
            // https://github.com/gsscoder/commandline/issues/473
            ParserResult<Commands> result = parser.ParseArguments<Commands>(CommandLineEx.GetCommandlineArgs());
            if (result.Tag == ParserResultType.NotParsed)
            {
                ConsoleEx.WriteLineError("Failed to parse commandline.");
                return -1;
            }
            Commands commands = ((Parsed<Commands>)result).Value;

            // At least some action must be specified
            if (!commands.Process &&
                !commands.ReMux &&
                !commands.ReEncode &&
                !commands.WriteSidecar &&
                !commands.CreateTagMap &&
                !commands.Monitor &&
                !commands.CheckForTools)
            {
                // TODO : Write commandline help output on demand
                // https://github.com/gsscoder/commandline/issues/445#issuecomment-317901624
                ConsoleEx.WriteLineError("Failed to parse commandline, missing action.");
                ConsoleEx.WriteLineError(HelpText.AutoBuild(result, null, null));
                return -1;
            }

            // Run
            Program program = new Program(commands, options);
            return program.Run();
        }

        private Program(Commands commands, Options options)
        {
            ProgramCommands = commands;
            ProgramOptions = options;

            // Share the cancel object and settings with the utilities FileEx project
            FileEx.Options.TestNoModify = AppOptions.Default.TestNoModify;
            FileEx.Options.FileRetryCount = AppOptions.Default.FileRetryCount;
            FileEx.Options.FileRetryWaitTime = AppOptions.Default.FileRetryWaitTime;
            Cancel = FileEx.Options.Cancel;

            // Set the static value for use in other static classes where cancel is observed
            Default = this;
        }

        private int Run()
        {
            // Create the list of files and folders to process
            if (!CreateFileList())
                return -1;

            // Register cancel handler
            void Cancelhandler(object s, ConsoleCancelEventArgs e) => CancelHandler(e, this);
            Console.CancelKeyPress += Cancelhandler;

            // Process all the commands
            int ret;
            while (true)
            {
                // Process
                if (ProgramCommands.Process &&
                    !Process())
                {
                    ret = -1;
                    break;
                }

                // ReMux
                if (ProgramCommands.ReMux &&
                    !ReMux())
                {
                    ret = -1;
                    break;
                }

                // ReEncode
                if (ProgramCommands.ReEncode &&
                    !ReEncode())
                {
                    ret = -1;
                    break;
                }

                // WriteSidecar
                if (ProgramCommands.WriteSidecar &&
                    !WriteSidecar())
                {
                    ret = -1;
                    break;
                }

                // CreateTagMap
                if (ProgramCommands.CreateTagMap &&
                    !CreateTagMap())
                {
                    ret = -1;
                    break;
                }

                // CheckForTools
                if (ProgramCommands.CheckForTools &&
                    !CheckForTools())
                {
                    ret = -1;
                    break;
                }

                // Monitor
                if (ProgramCommands.Monitor &&
                    !Monitor())
                {
                    ret = -1;
                    break;
                }

                // Done
                ret = 0;
                break;
            }

            // Unregister cancel handler
            Console.CancelKeyPress -= Cancelhandler;

            return ret;
        }

        private bool CreateFileList()
        {
            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLine("Creating file and folder list ...");

            // Trim quotes from input paths
            ProgramCommands.Folders = ProgramCommands.Folders.Select(folder => folder.Trim('"'));
            ProgramCommands.Files = ProgramCommands.Files.Select(file => file.Trim('"'));

            // Create the file and directory list
            if (!PlexCleaner.Process.CreateFileAndFolderList(ProgramCommands.Folders.ToList(), out fileList, out directoryList))
                return false;

            try
            {
                // Add all the commandline files to the file list
                foreach (string file in ProgramCommands.Files)
                {
                    // Add the file to the list
                    FileInfo fileinfo = new FileInfo(file);
                    fileList.Add(fileinfo);
                }
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLineError(e);
                return false;
            }

            // Report
            ConsoleEx.WriteLine($"Discovered {directoryList.Count} directories and {fileList.Count} files");
            ConsoleEx.WriteLine("");

            return true;
        }

        private bool Process()
        {
            Process process = new Process();
            return (process.ProcessFiles(fileList) && 
                process.DeleteEmptyFolders(ProgramCommands.Folders.ToList()));
        }

        private bool ReMux()
        {
            Process process = new Process();
            return process.ReMuxFiles(fileList);
        }

        private bool ReEncode()
        {
            Process process = new Process();
            return process.ReEncodeFiles(fileList);
        }

        private bool WriteSidecar()
        {
            Process process = new Process();
            return process.WriteSidecarFiles(fileList);
        }

        private bool CreateTagMap()
        {
            Process process = new Process();
            return process.CreateTagMapFiles(fileList);
        }

        private bool CheckForTools()
        {
            return Updater.CheckForTools();
        }

        private bool Monitor()
        {
            Monitor monitor = new Monitor();
            return monitor.MonitorFolders(ProgramCommands.Folders.ToList());
        }

        private static void CancelHandler(ConsoleCancelEventArgs e, Program program)
        {
            ConsoleEx.WriteLineError("Cancel key pressed");
            e.Cancel = true;

            // Signal the cancel event
            program.Cancel.State = true;
        }

        private List<DirectoryInfo> directoryList;
        private List<FileInfo> fileList;

        private Commands ProgramCommands { get; }
        public Options ProgramOptions { get; }
        public Signal Cancel { get; }

        public static Program Default;
    }
}
