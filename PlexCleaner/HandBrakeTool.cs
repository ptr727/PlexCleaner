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
                toolinfo.FileName = $"HandBrakeCLI-{toolinfo.Version}-win-x86_64.zip";
                toolinfo.Url = $"https://github.com/HandBrake/HandBrake/releases/download/{toolinfo.Version}/{toolinfo.FileName}";
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return true;
        }

        public static bool ConvertToMkv(string inputName, string videoCodec, int videoQuality, string audioCodec, string outputName)
        {
            // Delete output file
            FileEx.DeleteFile(outputName);

            // Create the HandBrakeCLI commandline and execute
            // https://handbrake.fr/docs/en/latest/cli/command-line-reference.html
            // Encode audio and video, copy subtitles
            string snippets = Program.Options.TestSnippets ? HandBrakeSnippet : "";
            string commandline = $"--input \"{inputName}\" --output \"{outputName}\" --format av_mkv --encoder {videoCodec} --encoder-preset medium --quality {videoQuality} --all-subtitles --all-audio --aencoder {audioCodec} {snippets}";
            int exitcode = HandBrakeCli(commandline);
            return exitcode == 0;
        }

        public static bool ConvertToMkv(string inputName, string videoCodec, int videoQuality, string outputName)
        {
            // Delete output file
            FileEx.DeleteFile(outputName);

            // Create the HandBrakeCLI commandline and execute
            // https://handbrake.fr/docs/en/latest/cli/command-line-reference.html
            // Encode video, copy audio and subtitles
            string snippets = Program.Options.TestSnippets ? HandBrakeSnippet : "";
            string commandline = $"--input \"{inputName}\" --output \"{outputName}\" --format av_mkv --encoder {videoCodec} --encoder-preset medium --quality {videoQuality} --all-subtitles --all-audio --aencoder copy {snippets}";
            int exitcode = HandBrakeCli(commandline);
            return exitcode == 0;
        }

        public static bool ConvertToMkv(string inputName, string outputName)
        {
            // Use defaults
            return ConvertToMkv(inputName,
                                Program.Config.ConvertOptions.EnableH265Encoder ? HandBrakeTool.H265Codec : HandBrakeTool.H264Codec,
                                Program.Config.ConvertOptions.VideoEncodeQuality,
                                Program.Config.ConvertOptions.AudioEncodeCodec,
                                outputName);
        }

        public static bool DeInterlaceToMkv(string inputName, string videoCodec, int videoQuality, string outputName)
        {
            // Delete output file
            FileEx.DeleteFile(outputName);

            // Create the HandBrakeCLI commandline and execute
            // https://handbrake.fr/docs/en/latest/cli/command-line-reference.html
            // Encode and decomb video, copy audio and subtitles
            string snippets = Program.Options.TestSnippets ? HandBrakeSnippet : "";
            string commandline = $"--input \"{inputName}\" --output \"{outputName}\" --format av_mkv --encoder {videoCodec} --encoder-preset medium --quality {videoQuality} --comb-detect --decomb --all-subtitles --all-audio --aencoder copy {snippets}";
            int exitcode = HandBrakeCli(commandline);
            return exitcode == 0;
        }

        public static bool DeInterlaceToMkv(string inputName, string outputName)
        {
            // Use defaults
            return DeInterlaceToMkv(inputName,
                                    Program.Config.ConvertOptions.EnableH265Encoder ? HandBrakeTool.H265Codec : HandBrakeTool.H264Codec,
                                    Program.Config.ConvertOptions.VideoEncodeQuality,
                                    outputName);
        }

        public const string H264Codec = "x264";
        public const string H265Codec = "x265";

        private const string HandBrakeBinary = @"HandBrakeCLI.exe";
        private const string HandBrakeSnippet = "--start-at duration:00 --stop-at duration:60";
    }
}
