using System;
using InsaneGenius.Utilities;
using Newtonsoft.Json.Linq;

namespace PlexCleaner
{
    internal static class HandBrakeTool
    {
        public static int HandBrakeCli(string parameters)
        {
            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLineTool($"HandBrake : {parameters}");
            string path = Tools.CombineToolPath(ToolsOptions.HandBrake, HandBrakeBinary);
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

            toolinfo.Tool = nameof(HandBrakeTool);

            try
            {
                // Get the latest release version number from github releases
                // https://api.github.com/repos/handbrake/handbrake/releases/latest
                if (!Download.DownloadString(new Uri(@"https://api.github.com/repos/handbrake/handbrake/releases/latest"), out string json))
                    return false;
                JObject releases = JObject.Parse(json);
                // "tag_name": "1.2.2",
                JToken versiontag = releases["tag_name"];
                toolinfo.Version = versiontag.ToString();

                // Create download URL and the output filename using the version number
                // https://github.com/HandBrake/HandBrake/releases/download/1.3.2/HandBrakeCLI-1.3.2-win-x86_64.zip
                // https://handbrake.fr/downloads2.php
                // https://download.handbrake.fr/releases/1.3.1/HandBrakeCLI-1.3.1-win-x86_64.zip
                toolinfo.FileName = $"HandBrakeCLI-{toolinfo.Version}-win-x86_64.zip";
                //toolinfo.Url = $"https://download.handbrake.fr/releases/{toolinfo.Version}/{toolinfo.FileName}";
                toolinfo.Url = $"https://github.com/HandBrake/HandBrake/releases/download/{toolinfo.Version}/{toolinfo.FileName}";
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return true;
        }

        public static bool ConvertToMkv(string inputname, int quality, string audiocodec, string outputname)
        {
            // Delete output file
            FileEx.DeleteFile(outputname);

            // Create the HandBrakeCLI commandline and execute
            // https://handbrake.fr/docs/en/latest/cli/command-line-reference.html
            // https://handbrake.fr/docs/en/latest/cli/cli-options.html
            string snippets = Program.Options.TestSnippets ? HandBrakeSnippet : "";
            string commandline = $"--input \"{inputname}\" --output \"{outputname}\" --format av_mkv --encoder x264 --encoder-preset medium --quality {quality} --all-subtitles --all-audio --aencoder {audiocodec} {snippets}";
            int exitcode = HandBrakeCli(commandline);
            return exitcode == 0;
        }

        public static bool DeInterlaceToMkv(string inputname, int quality, string audiocodec, string outputname)
        {
            // Delete output file
            FileEx.DeleteFile(outputname);

            // Create the HandBrakeCLI commandline and execute
            // https://handbrake.fr/docs/en/latest/cli/command-line-reference.html
            // https://handbrake.fr/docs/en/latest/cli/cli-options.html
            string snippets = Program.Options.TestSnippets ? HandBrakeSnippet : "";
            string commandline = $"--input \"{inputname}\" --output \"{outputname}\" --format av_mkv --encoder x264 --encoder-preset medium --quality {quality} --comb-detect --decomb --all-subtitles --all-audio --aencoder copy --audio-fallback {audiocodec} {snippets}";
            int exitcode = HandBrakeCli(commandline);
            return exitcode == 0;
        }

        private const string HandBrakeBinary = @"HandBrakeCLI.exe";
        private const string HandBrakeSnippet = "--start-at duration:00 --stop-at duration:60";
    }
}
