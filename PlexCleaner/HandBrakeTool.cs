using System;
using InsaneGenius.Utilities;
using System.Net;
using Newtonsoft.Json.Linq;

namespace PlexCleaner
{
    internal static class HandBrakeTool
    {
        public static int HandBrakeCli(string parameters)
        {
            string path = Tools.CombineToolPath(ToolsOptions.HandBrake, HandBrakeBinary);
            ConsoleEx.WriteLineTool($"HandBrake : {parameters}");
            return ProcessEx.Execute(path, parameters);
        }

        public static string GetToolFolder()
        {
            return Tools.CombineToolPath(ToolsOptions.HandBrake);
        }

        public static string GetToolPath()
        {
            return Tools.CombineToolPath(ToolsOptions.HandBrake, HandBrakeBinary);
        }

        public static bool GetLatestVersion(ToolInfo toolinfo)
        {
            if (toolinfo == null)
                throw new ArgumentNullException(nameof(toolinfo));

            try
            {
                // Get the latest release version number from github releases
                // https://api.github.com/repos/handbrake/handbrake/releases/latest
                // We need a user agent for GitHub else we get a 403 forbidden error
                // https://developer.github.com/v3/#user-agent-required
                using WebClient webClient = new WebClient();
                webClient.Headers.Add("User-Agent", "PlexCleaner Utility");
                string json = webClient.DownloadString(@"https://api.github.com/repos/handbrake/handbrake/releases/latest");
                JObject releases = JObject.Parse(json);
                // "tag_name": "1.2.2",
                JToken versiontag = releases["tag_name"];
                toolinfo.Version = versiontag.ToString();

                // Create download URL and the output filename using the version number
                // https://handbrake.fr/downloads2.php
                // E.g. https://download.handbrake.fr/releases/1.3.1/HandBrakeCLI-1.3.1-win-x86_64.zip
                toolinfo.FileName = $"HandBrakeCLI-{toolinfo.Version}-win-x86_64.zip";
                toolinfo.Url = $"https://download.handbrake.fr/releases/{toolinfo.Version}/{toolinfo.FileName}";
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return true;
        }

        public static bool ConvertToMkv(string inputname, int quality, string outputname)
        {
            // Delete output file
            FileEx.DeleteFile(outputname);

            // Create the HandBrakeCLI commandline and execute
            // https://handbrake.fr/docs/en/latest/cli/command-line-reference.html
            // https://handbrake.fr/docs/en/latest/cli/cli-options.html
            string snippets = Convert.Options.TestSnippets ? HandBrakeSnippet : "";
            string commandline = $"--input \"{inputname}\" --output \"{outputname}\" --format av_mkv --encoder x264 --encoder-preset medium --quality {quality} --comb-detect --decomb --subtitle 1,2,3,4 --audio 1,2,3,4 --aencoder copy --audio-fallback ac3 {snippets}";
            ConsoleEx.WriteLine("");
            int exitcode = HandBrakeCli(commandline);
            ConsoleEx.WriteLine("");
            return exitcode == 0;
        }

        private const string HandBrakeBinary = @"HandBrakeCLI.exe";
        private const string HandBrakeSnippet = "--start-at duration:00 --stop-at duration:60";
    }
}
