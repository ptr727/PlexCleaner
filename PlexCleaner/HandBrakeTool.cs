using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using InsaneGenius.Utilities;
using Serilog;

// https://handbrake.fr/docs/en/latest/cli/command-line-reference.html

// TODO: What is an equivalent to ffmpeg -nostats to suppress progress output?
// https://github.com/HandBrake/HandBrake/issues/2000

namespace PlexCleaner;

public partial class HandBrakeTool : MediaTool
{
    public override ToolFamily GetToolFamily() => ToolFamily.HandBrake;

    public override ToolType GetToolType() => ToolType.HandBrake;

    protected override string GetToolNameWindows() => "HandBrakeCLI.exe";

    protected override string GetToolNameLinux() => "HandBrakeCLI";

    public override bool GetInstalledVersion(out MediaToolInfo mediaToolInfo)
    {
        // Initialize
        mediaToolInfo = new MediaToolInfo(this);

        // Get version
        const string commandline = "--version";
        int exitCode = Command(commandline, out string output);
        if (exitCode != 0)
        {
            return false;
        }

        // First line of stdout as version
        // E.g. Windows : "HandBrake 1.3.3"
        // E.g. Linux : "HandBrake 1.3.3"
        string[] lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        // Extract the short version number
        Match match = InstalledVersionRegex().Match(lines[0]);
        Debug.Assert(match.Success);
        mediaToolInfo.Version = match.Groups["version"].Value;
        Debug.Assert(Version.TryParse(mediaToolInfo.Version, out _));

        // Get tool filename
        mediaToolInfo.FileName = GetToolPath();

        // Get other attributes if we can read the file
        if (File.Exists(mediaToolInfo.FileName))
        {
            FileInfo fileInfo = new(mediaToolInfo.FileName);
            mediaToolInfo.ModifiedTime = fileInfo.LastWriteTimeUtc;
            mediaToolInfo.Size = fileInfo.Length;
        }

        return true;
    }

    protected override bool GetLatestVersionWindows(out MediaToolInfo mediaToolInfo)
    {
        // Initialize
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
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            return false;
        }
        return true;
    }

    protected override bool GetLatestVersionLinux(out MediaToolInfo mediaToolInfo)
    {
        // Not implemented
        mediaToolInfo = null;
        return false;
    }

    public static string GetStartStopSplit(TimeSpan timeStart, TimeSpan timeEnd) =>
        $"--start-at seconds:{(int)timeStart.TotalSeconds} --stop-at seconds:{(int)timeEnd.TotalSeconds}";

    public static string GetStartSplit(TimeSpan timeSpan) =>
        $"--start-at seconds:{(int)timeSpan.TotalSeconds}";

    public static string GetStopSplit(TimeSpan timeSpan) =>
        $"--stop-at seconds:{(int)timeSpan.TotalSeconds}";

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

        // TODO: How to suppress console output when running in parallel mode?
        // if (Program.Options.Parallel)

        // Input file
        StringBuilder commandline = new();
        _ = commandline.Append(CultureInfo.InvariantCulture, $"--input \"{inputName}\" ");

        // Snippets
        if (Program.Options.TestSnippets)
        {
            _ = commandline.Append(
                CultureInfo.InvariantCulture,
                $"{GetStopSplit(Program.SnippetTimeSpan)} "
            );
        }

        // Output file
        _ = commandline.Append(CultureInfo.InvariantCulture, $"--output \"{outputName}\" ");

        // MKV output
        _ = commandline.Append("--format av_mkv ");

        // Video encoder options
        // E.g. --encoder x264 --quality 20 --encoder-preset medium
        _ = commandline.Append(
            CultureInfo.InvariantCulture,
            $"--encoder {Program.Config.ConvertOptions.HandBrakeOptions.Video} "
        );

        // DeInterlace using decomb filter
        if (deInterlace)
        {
            _ = commandline.Append("--comb-detect --decomb ");
        }

        // All audio with encoder
        // E.g. --all-audio --aencoder copy --audio-fallback ac3
        _ = commandline.Append(
            CultureInfo.InvariantCulture,
            $"--all-audio --aencoder {Program.Config.ConvertOptions.HandBrakeOptions.Audio} "
        );

        // All or no subtitles
        _ = commandline.Append(includeSubtitles ? "--all-subtitles " : "--subtitle none ");

        // Execute
        int exitCode = Command(commandline.ToString(), 5, out error, out _);
        return exitCode == 0;
    }

    public const string DefaultVideoOptions = "x264 --quality 22 --encoder-preset medium";
    public const string DefaultAudioOptions = "copy --audio-fallback ac3";

    private const string VersionPattern = @"HandBrake\ (?<version>.*)";

    [GeneratedRegex(VersionPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    public static partial Regex InstalledVersionRegex();
}
