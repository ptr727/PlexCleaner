using System;
using InsaneGenius.Utilities;
using Newtonsoft.Json.Linq;
using System.IO;

// https://handbrake.fr/docs/en/latest/cli/command-line-reference.html

namespace PlexCleaner
{
    public class HandBrakeTool : MediaTool
    {
        public override ToolFamily GetToolFamily()
        {
            return ToolFamily.HandBrake;
        }

        public override ToolType GetToolType()
        {
            return ToolType.HandBrake;
        }

        protected override string GetToolNameWindows()
        {
            return "HandBrakeCLI.exe";
        }

        protected override string GetToolNameLinux()
        {
            return "HandBrakeCLI";
        }

        public override bool GetInstalledVersion(out MediaToolInfo mediaToolInfo)
        {
            // Initialize            
            mediaToolInfo = new MediaToolInfo(this);

            // Get version
            string commandline = $"--version";
            int exitcode = Command(commandline, out string output);
            if (exitcode != 0)
                return false;

            // First line as version
            string[] lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            mediaToolInfo.Version = lines[0];

            // Get tool filename
            mediaToolInfo.FileName = GetToolPath();

            // Get other attributes if we can read the file
            if (File.Exists(mediaToolInfo.FileName))
            {
                FileInfo fileInfo = new FileInfo(mediaToolInfo.FileName);
                mediaToolInfo.ModifiedTime = fileInfo.LastWriteTimeUtc;
                mediaToolInfo.Size = fileInfo.Length;
            }

            return true;
        }

        public override bool GetLatestVersionWindows(out MediaToolInfo mediaToolInfo)
        {
            // Initialize            
            mediaToolInfo = new MediaToolInfo(this);

            try
            {
                // Get the latest release version number from github releases
                // https://api.github.com/repos/handbrake/handbrake/releases/latest
                if (!Download.DownloadString(new Uri(@"https://api.github.com/repos/handbrake/handbrake/releases/latest"), out string json))
                    return false;

                JObject releases = JObject.Parse(json);
                // "tag_name": "1.2.2",
                JToken versiontag = releases["tag_name"];
                mediaToolInfo.Version = versiontag.ToString();

                // Create download URL and the output filename using the version number
                // https://github.com/HandBrake/HandBrake/releases/download/1.3.2/HandBrakeCLI-1.3.2-win-x86_64.zip
                mediaToolInfo.FileName = $"HandBrakeCLI-{mediaToolInfo.Version}-win-x86_64.zip";
                mediaToolInfo.Url = $"https://github.com/HandBrake/HandBrake/releases/download/{mediaToolInfo.Version}/{mediaToolInfo.FileName}";
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return true;
        }

        public override bool GetLatestVersionLinux(out MediaToolInfo mediaToolInfo)
        {
            // Initialize            
            mediaToolInfo = new MediaToolInfo(this);

            // TODO
            return false;
        }

        public bool ConvertToMkv(string inputName, string videoCodec, int videoQuality, string audioCodec, string outputName)
        {
            // Delete output file
            FileEx.DeleteFile(outputName);

            // Encode audio and video, copy subtitles
            string snippets = Program.Options.TestSnippets ? HandBrakeSnippet : "";
            string commandline = $"--input \"{inputName}\" --output \"{outputName}\" --format av_mkv --encoder {videoCodec} --encoder-preset medium --quality {videoQuality} --all-subtitles --all-audio --aencoder {audioCodec} {snippets}";
            int exitcode = Command(commandline);
            return exitcode == 0;
        }

        public bool ConvertToMkv(string inputName, string videoCodec, int videoQuality, string outputName)
        {
            // Delete output file
            FileEx.DeleteFile(outputName);

            // Encode video, copy audio and subtitles
            string snippets = Program.Options.TestSnippets ? HandBrakeSnippet : "";
            string commandline = $"--input \"{inputName}\" --output \"{outputName}\" --format av_mkv --encoder {videoCodec} --encoder-preset medium --quality {videoQuality} --all-subtitles --all-audio --aencoder copy {snippets}";
            int exitcode = Command(commandline);
            return exitcode == 0;
        }

        public bool ConvertToMkv(string inputName, string outputName)
        {
            // Use defaults
            return ConvertToMkv(inputName,
                                Program.Config.ConvertOptions.EnableH265Encoder ? H265Codec : H264Codec,
                                Program.Config.ConvertOptions.VideoEncodeQuality,
                                Program.Config.ConvertOptions.AudioEncodeCodec,
                                outputName);
        }

        public bool DeInterlaceToMkv(string inputName, string videoCodec, int videoQuality, string outputName)
        {
            // Delete output file
            FileEx.DeleteFile(outputName);

            // Encode and decomb video, copy audio and subtitles
            string snippets = Program.Options.TestSnippets ? HandBrakeSnippet : "";
            string commandline = $"--input \"{inputName}\" --output \"{outputName}\" --format av_mkv --encoder {videoCodec} --encoder-preset medium --quality {videoQuality} --comb-detect --decomb --all-subtitles --all-audio --aencoder copy {snippets}";
            int exitcode = Command(commandline);
            return exitcode == 0;
        }

        public bool DeInterlaceToMkv(string inputName, string outputName)
        {
            // Use defaults
            return DeInterlaceToMkv(inputName,
                                    Program.Config.ConvertOptions.EnableH265Encoder ? H265Codec : H264Codec,
                                    Program.Config.ConvertOptions.VideoEncodeQuality,
                                    outputName);
        }

        public const string H264Codec = "x264";
        public const string H265Codec = "x265";
        private const string HandBrakeSnippet = "--start-at duration:00 --stop-at duration:60";
    }
}
