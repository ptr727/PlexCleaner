using System;
using InsaneGenius.Utilities;
using System.Net;
using Newtonsoft.Json.Linq;

namespace PlexCleaner
{
    internal static class HandBrakeTool
    {
        public static int HandBrake(string parameters)
        {
            string path = Tools.CombineToolPath(ToolOptions.Default.HandBrake, HandBrakeBinary);
            ConsoleEx.WriteLineTool($"HandBrake : {parameters}");
            return ProcessEx.Execute(path, parameters);
        }

        public static string GetToolPath()
        {
            return Tools.CombineToolPath(ToolOptions.Default.HandBrake);
        }

        public static bool GetLatestVersion(ToolInfo toolinfo)
        {
            try
            {
                // Get the latest rfelease version number from github releases
                // https://api.github.com/repos/handbrake/handbrake/releases/latest
                using (WebClient webClient = new WebClient())
                {
                    // We need a user agent for GitHub else we get a 403 forbidden error
                    // https://developer.github.com/v3/#user-agent-required
                    webClient.Headers.Add("User-Agent", "PlexCleaner Utility");
                    string json = webClient.DownloadString(@"https://api.github.com/repos/handbrake/handbrake/releases/latest");
                    JObject releases = JObject.Parse(json);
                    // "tag_name": "1.2.2",
                    JToken versiontag = releases["tag_name"];
                    toolinfo.Version = versiontag.ToString();
                }

                // Create download URL and the output filename using the version number
                // https://handbrake.fr/downloads2.php
                // E.g. https://download2.handbrake.fr/1.2.2/HandBrakeCLI-1.2.2-win-x86_64.zip
                toolinfo.FileName = $"HandBrakeCLI-{toolinfo.Version}-win-x86_64.zip";
                toolinfo.Url = $"https://download2.handbrake.fr/{toolinfo.Version}/{toolinfo.FileName}";
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return true;
        }

        private const string HandBrakeBinary = @"HandBrakeCLI.exe";
    }
}
