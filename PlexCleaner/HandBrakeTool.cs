using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;
using InsaneGenius.Utilities;
using Serilog;

// https://handbrake.fr/docs/en/latest/cli/command-line-reference.html

// HandBrakeCLI [options] -i <source> -o <destination>

// TODO: What is an equivalent to ffmpeg -nostats to suppress progress output?
// https://github.com/HandBrake/HandBrake/issues/2000

namespace PlexCleaner;

public partial class HandBrake
{
    // TODO Why partial?
    public partial class Tool : MediaTool
    {
        public override ToolFamily GetToolFamily() => ToolFamily.HandBrake;

        public override ToolType GetToolType() => ToolType.HandBrake;

        protected override string GetToolNameWindows() => "HandBrakeCLI.exe";

        protected override string GetToolNameLinux() => "HandBrakeCLI";

        public IGlobalOptions GetBuilder() => Builder.Create(GetToolPath());

        public override bool GetInstalledVersion(out MediaToolInfo mediaToolInfo)
        {
            // Get version info
            mediaToolInfo = new MediaToolInfo(this) { FileName = GetToolPath() };
            Command command = Builder.Version(GetToolPath());
            return Execute(command, out BufferedCommandResult result)
                && result.ExitCode == 0
                && GetVersion(result.StandardOutput, mediaToolInfo);
        }

        public static bool GetVersion(string text, MediaToolInfo mediaToolInfo)
        {
            // Get file info
            if (File.Exists(mediaToolInfo.FileName))
            {
                FileInfo fileInfo = new(mediaToolInfo.FileName);
                mediaToolInfo.ModifiedTime = fileInfo.LastWriteTimeUtc;
                mediaToolInfo.Size = fileInfo.Length;
            }

            // "HandBrake 1.3.3"

            // Parse version
            string[] lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Match match = InstalledVersionRegex().Match(lines[0]);
            Debug.Assert(match.Success && Version.TryParse(match.Groups["version"].Value, out _));
            mediaToolInfo.Version = match.Groups["version"].Value;
            return true;
        }

        protected override bool GetLatestVersionWindows(out MediaToolInfo mediaToolInfo)
        {
            mediaToolInfo = new MediaToolInfo(this);
            try
            {
                // Get the latest release version number from github releases
                // https://github.com/HandBrake/HandBrake
                const string repo = "HandBrake/HandBrake";
                mediaToolInfo.Version = GetLatestGitHubRelease(repo);

                // Create the filename using the version number
                // HandBrakeCLI-1.3.2-win-x86_64.zip
                mediaToolInfo.FileName = $"HandBrakeCLI-{mediaToolInfo.Version}-win-x86_64.zip";

                // Get the GitHub download Uri
                mediaToolInfo.Url = GitHubRelease.GetDownloadUri(
                    repo,
                    mediaToolInfo.Version,
                    mediaToolInfo.FileName
                );
            }
            catch (Exception e)
                when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
            {
                return false;
            }
            return true;
        }

        public bool ConvertToMkv(
            string inputName,
            string outputName,
            bool includeSubtitles,
            bool deInterlace,
            out string error
        )
        {
            // Delete output file
            _ = FileEx.DeleteFile(outputName);

            // Build command line
            error = string.Empty;
            Command command = GetBuilder()
                .GlobalOptions(options => options.Default())
                .InputOptions(options => options.InputFile(inputName).TestSnippets())
                .OutputOptions(options =>
                    options
                        .OutputFile(outputName)
                        .FormatMatroska()
                        .VideoEncoder(Program.Config.ConvertOptions.HandBrakeOptions.Video)
                        .Add(deInterlace, (options) => options.CombDetect().Decomb())
                        .AllAudio()
                        .AudioEncoder(Program.Config.ConvertOptions.HandBrakeOptions.Audio)
                        .Add(
                            includeSubtitles,
                            (options) => options.AllSubtitles(),
                            (options) => options.NoSubtitles()
                        )
                )
                .Build();

            // Execute command
            if (!Execute(command, true, out BufferedCommandResult result))
            {
                return false;
            }
            error = result.StandardError.Trim();
            return result.ExitCode == 0;
        }

        [GeneratedRegex(
            @"HandBrake\ (?<version>.*)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline
        )]
        public static partial Regex InstalledVersionRegex();
    }

    public const string DefaultVideoOptions = "x264 --quality 22 --encoder-preset medium";
    public const string DefaultAudioOptions = "copy --audio-fallback ac3";
}
