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
        // Options: Process, ReMux, ReEncode, WriteSidecar, CreateTagMap, CheckForTools, Monitor, Folders
        // Example input:
        // PlexCleaner.exe --Process --Folders "c:\foo" "d:\bar"
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
            ProgCommands = commands;
            ProgOptions = options;

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
            // Register cancel handler
            void Cancelhandler(object s, ConsoleCancelEventArgs e) => CancelHandler(e, this);
            Console.CancelKeyPress += Cancelhandler;

            // Process the commands
            int ret;
            while (true)
            {
                // Test for existence and normalize the folder paths
                if (!NormalizeFolders())
                {
                    ret = -1;
                    break;
                }

                // Process
                if (ProgCommands.Process &&
                    !Process())
                {
                    ret = -1;
                    break;
                }

                // ReMux
                if (ProgCommands.ReMux &&
                    !ReMux())
                {
                    ret = -1;
                    break;
                }

                // ReEncode
                if (ProgCommands.ReEncode &&
                    !ReEncode())
                {
                    ret = -1;
                    break;
                }

                // WriteSidecar
                if (ProgCommands.WriteSidecar &&
                    !WriteSidecar())
                {
                    ret = -1;
                    break;
                }

                // CreateTagMap
                if (ProgCommands.CreateTagMap &&
                    !CreateTagMap())
                {
                    ret = -1;
                    break;
                }

                // CheckForTools
                if (ProgCommands.CheckForTools &&
                    !CheckForTools())
                {
                    ret = -1;
                    break;
                }

                // Monitor
                if (ProgCommands.Monitor &&
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

        private bool NormalizeFolders()
        {
            List<string> infolders = ProgCommands.Folders.ToList();
            List<string> norfolders = new List<string>();
            foreach (string infolder in infolders)
            {
                string norfolder;
                try
                {
                    // Normalize the folder
                    norfolder = Path.GetFullPath(infolder.Trim('"'));

                    // Make sure it exists
                    if (!Directory.Exists(norfolder))
                    {
                        ConsoleEx.WriteLineError($"Folder does not exist : \"{infolder}\"");
                        return false;
                    }
                }
                catch (Exception)
                {
                    ConsoleEx.WriteLineError($"Problem with folder : \"{infolder}\"");
                    return false;
                }

                // Save normalized folder
                norfolders.Add(norfolder);
            }

            // Swap the input folders with the normalized folders
            ProgCommands.Folders = norfolders;

            return true;
        }

        private bool Process()
        {
            Process process = new Process();
            return process.ProcessFolders(ProgCommands.Folders.ToList());
        }

        private bool ReMux()
        {
            Process process = new Process();
            return process.ReMuxFolders(ProgCommands.Folders.ToList());
        }

        private bool ReEncode()
        {
            Process process = new Process();
            return process.ReEncodeFolders(ProgCommands.Folders.ToList());
        }

        private bool WriteSidecar()
        {
            Process process = new Process();
            return process.WriteSidecarFolders(ProgCommands.Folders.ToList());
        }

        private bool CreateTagMap()
        {
            Process process = new Process();
            return process.CreateTagMapFolders(ProgCommands.Folders.ToList());
        }

        private bool CheckForTools()
        {
            return Updater.CheckForTools();
        }

        private bool Monitor()
        {
            Monitor monitor = new Monitor();
            return monitor.MonitorFolders(ProgCommands.Folders.ToList());
        }

        private static void CancelHandler(ConsoleCancelEventArgs e, Program program)
        {
            ConsoleEx.WriteLineError("Cancel key pressed");
            e.Cancel = true;

            // Signal the cancel event
            program.Cancel.State = true;
        }

        private Commands ProgCommands { get; }
        public Options ProgOptions { get; }
        public Signal Cancel { get; }

        public List<Iso6393> Iso6393List;
        public static Program Default;
    }
}
