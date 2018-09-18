using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CommandLine;
using CommandLine.Text;
using ISO6393Library;
using InsaneGenius.Utilities;

// TODO : Capture standard output and error, and still let the app write formatted output, e.g. FFmpeg that writes in color
// TODO : Reenable the file watcher when directory dissapears
//  e.GetException().GetType() == typeof(SomethingPathNotAccessibleException)), retry waiting with with Directory.Exists(path)
//  if (e is Win32Exception)
//  OnError : System.ComponentModel.Win32Exception (0x80004005): The specified network name is no longer available
// TODO : Check for new tool version on start and download new tools
// TODO : Retrieve SRT subtitles using original file details, before the sourced file gets modified
// TODO : Embed SRT files in MKV file
// TODO : Consider converting DIVX to H264 or just re-tag as XVID
//  cfourcc -i DIVX, DX50, FMP4, cfourcc -u XVID
// TODO : Compare folder with file name and rename to match
// TODO : Check if more than two audio or subtitle tracks of the same language
//  Prefer DTS over AC3, if same language, change order, e.g. the breakfast club
// TODO : Keep machine from sleeping while processing
// TODO : Remove subtitles that cannot DirectPlay, e.g. SubStation Alpha ASS

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
            // Redirect debug outpout to console
            // TODO : Disable modal UI for Assert, AssertUiEnabled
            TextWriterTraceListener textWriterTraceListener = new TextWriterTraceListener(Console.Error);
            Debug.Listeners.Add(textWriterTraceListener);

            // Parse the commandline arguments
            // https://github.com/gsscoder/commandline
            Parser parser = new Parser(with =>
            {
                with.CaseSensitive = false;
                with.HelpWriter = Console.Error;
            });
            // TODO : Quoted paths ending in a \ fail to parse properly, use our own parser
            // https://github.com/gsscoder/commandline/issues/473
            ParserResult<Options> result = parser.ParseArguments<Options>(CommandLineEx.GetCommandlineArgs());
            if (result.Tag == ParserResultType.NotParsed)
            {
                ConsoleEx.WriteLineError("Failed to parse commandline.");
                return -1;
            }
            Options options = ((Parsed<Options>)result).Value;

            // Some action must be specified
            if (!options.Process &&
                !options.ReMux &&
                !options.ReEncode &&
                !options.WriteSidecar &&
                !options.CreateTagMap &&
                !options.Monitor &&
                !options.CheckForTools)
            {
                // TODO : Write commandline help output on demand
                // https://github.com/gsscoder/commandline/issues/445#issuecomment-317901624
                ConsoleEx.WriteLineError("Failed to parse commandline, missing action.");
                ConsoleEx.WriteLineError(HelpText.AutoBuild(result, null, null));
                return -1;
            }

            // Run
            Program program = new Program(options);
            return program.Run();
        }

        private Program(Options options)
        {
            Options = options;

            // Share the cancel object and settings with the utilities project
            FileEx.Settings = new FileEx.SettingsEx()
            {
                TestNoModify = Properties.Settings.Default.TestNoModify,
                FileRetryCount = Properties.Settings.Default.FileRetryCount,
                FileRetryWaitTime = Properties.Settings.Default.FileRetryWaitTime
            };
            Cancel = FileEx.Settings.Cancel;

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
                if (Options.Process &&
                    !Process())
                {
                    ret = -1;
                    break;
                }

                // ReMux
                if (Options.ReMux &&
                    !ReMux())
                {
                    ret = -1;
                    break;
                }

                // ReEncode
                if (Options.ReEncode &&
                    !ReEncode())
                {
                    ret = -1;
                    break;
                }

                // WriteSidecar
                if (Options.WriteSidecar &&
                    !WriteSidecar())
                {
                    ret = -1;
                    break;
                }

                // CreateTagMap
                if (Options.CreateTagMap &&
                    !CreateTagMap())
                {
                    ret = -1;
                    break;
                }

                // CheckForTools
                if (Options.CheckForTools &&
                    !CheckForTools())
                {
                    ret = -1;
                    break;
                }

                // Monitor
                if (Options.Monitor &&
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
            List<string> infolders = Options.Folders.ToList();
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
            Options.Folders = norfolders;

            return true;
        }

        private bool Process()
        {
            Process process = new Process();
            return process.ProcessFolders(Options.Folders.ToList());
        }

        private bool ReMux()
        {
            Process process = new Process();
            return process.ReMuxFolders(Options.Folders.ToList());
        }

        private bool ReEncode()
        {
            Process process = new Process();
            return process.ReEncodeFolders(Options.Folders.ToList());
        }

        private bool WriteSidecar()
        {
            Process process = new Process();
            return process.WriteSidecarFolders(Options.Folders.ToList());
        }

        private bool CreateTagMap()
        {
            Process process = new Process();
            return process.CreateTagMapFolders(Options.Folders.ToList());
        }

        private bool CheckForTools()
        {
            return Updater.CheckForTools();
        }

        private bool Monitor()
        {
            Monitor monitor = new Monitor();
            return monitor.MonitorFolders(Options.Folders.ToList());
        }

        private static void CancelHandler(ConsoleCancelEventArgs e, Program program)
        {
            ConsoleEx.WriteLineError("Cancel key pressed");
            e.Cancel = true;

            // Signal the cancel event
            program.Cancel.State = true;
        }

        private Options Options { get; }
        public Signal Cancel { get; }

        public List<Iso6393> Iso6393List;
        public static Program Default;
    }
}
