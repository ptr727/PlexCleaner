using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using InsaneGenius.Utilities;
using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace PlexCleaner
{
    [Command(Name = "PlexCleaner", Description = "Plex media cleanup utility")]
    [HelpOption("-?")]
    public class ProgramMcMaster
    {
        // TODO : Quoted paths ending in a \ fail to parse properly, use our own parser
        // https://github.com/gsscoder/commandline/issues/473
        static Task<int> MainMcMaster(string[] args) => CommandLineApplication.ExecuteAsync<Program>(CommandLineEx.GetCommandlineArgs());

        [Option(Description = "Process the files or folders.")]
        public bool Process { get; }

        [Option(Description = "Re-Multiplex the files.")]
        public bool ReMux { get; }

        [Option(Description = "Re-Encode the files.")]
        public bool ReEncode { get; }

        [Option(Description = "Write sidecar files.")]
        public bool WriteSidecar { get; }

        [Option(Description = "Create a tag map for the files.")]
        public bool CreateTagMap { get; }

        [Option(Description = "Check for new tools and download if available.")]
        public bool CheckForTools { get; }

        [Option(Description = "Monitor for changes in folders, and process any changed files.")]
        public bool Monitor { get; }

        [Option(Description = "List of folders to process.")]
        public IEnumerable<string> Folders { get; }

        [Option(Description = "List of files to process.")]
        public IEnumerable<string> Files { get; }

        private async Task<int> OnExecuteAsync(CommandLineApplication app)
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

            // At least some action must be specified
            if (!Process &&
                !ReMux &&
                !ReEncode &&
                !WriteSidecar &&
                !CreateTagMap &&
                !Monitor &&
                !CheckForTools)
            {
                // TODO : Write commandline help output on demand
                // https://github.com/gsscoder/commandline/issues/445#issuecomment-317901624
                ConsoleEx.WriteLineError("Failed to parse commandline, missing action.");
                return -1;
            }

            // Run
            return 0;
        }
    }
}
