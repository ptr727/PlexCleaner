using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using InsaneGenius.Utilities;
using Serilog;

namespace PlexCleaner;

public partial class SevenZipTool : MediaTool
{
    public override ToolFamily GetToolFamily() => ToolFamily.SevenZip;

    public override ToolType GetToolType() => ToolType.SevenZip;

    protected override string GetToolNameWindows() => "7za.exe";

    protected override string GetToolNameLinux() => "7z";

    public override bool GetInstalledVersion(out MediaToolInfo mediaToolInfo)
    {
        // Initialize
        mediaToolInfo = new MediaToolInfo(this);

        // No version command, run with no arguments
        const string commandline = "";
        int exitCode = Command(commandline, out string output);
        if (exitCode != 0)
        {
            return false;
        }

        // First line of stdout as version
        // E.g. Windows : "7-Zip (a) 24.09 (x86) : Copyright (c) 1999-2024 Igor Pavlov : 2024-11-29"
        // E.g. Linux : "7-Zip 24.08 (x64) : Copyright (c) 1999-2024 Igor Pavlov : 2024-08-11"
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
            // https://github.com/ip7z/7zip
            const string repo = "ip7z/7zip";
            mediaToolInfo.Version = GetLatestGitHubRelease(repo);

            // Create the filename using the version number
            // remove the . from the version, 23.01 -> 2301
            // 7z2301-extra.7z
            mediaToolInfo.FileName = $"7z{mediaToolInfo.Version.Replace(".", null)}-extra.7z";

            // Get the GitHub download Uri
            mediaToolInfo.Url = GitHubRelease.GetDownloadUri(repo, mediaToolInfo.Version, mediaToolInfo.FileName);
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            return false;
        }
        return true;
    }

    protected override bool GetLatestVersionLinux(out MediaToolInfo mediaToolInfo)
    {
        // Initialize
        mediaToolInfo = new MediaToolInfo(this);

        // TODO: Linux implementation
        return false;
    }

    protected override string GetSubFolder() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "x64" : "";

    public override bool Update(string updateFile)
    {
        // We need to keep the previous copy of 7zip so we can extract the new copy
        // We need to extract to a temp location in the root tools folder, then rename to the destination folder
        // Build the versioned folder from the downloaded filename
        // E.g. 7z1805-extra.7z to .\Tools\7z1805-extra
        string extractPath = Tools.CombineToolPath(Path.GetFileNameWithoutExtension(updateFile));

        // Extract the update file
        Log.Logger.Information("Extracting {UpdateFile} ...", updateFile);
        if (!Tools.SevenZip.UnZip(updateFile, extractPath))
        {
            return false;
        }

        // Delete the tool destination directory
        string toolPath = GetToolFolder();
        if (!FileEx.DeleteDirectory(toolPath, true))
        {
            return false;
        }

        // Rename the folder
        // E.g. 7z1805-extra to .\Tools\7Zip
        return FileEx.RenameFolder(extractPath, toolPath);
    }

    public bool UnZip(string archive, string folder)
    {
        // 7z.exe x archive.zip -o"C:\Doc"
        string commandline = $"x -aoa -spe -y \"{archive}\" -o\"{folder}\"";
        int exitCode = Command(commandline);
        return exitCode == 0;
    }

    public bool BootstrapDownload()
    {
        // Only supported on Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        // Make sure that the Tools folder exists
        if (!Directory.Exists(Tools.GetToolsRoot()))
        {
            Log.Logger.Warning("Creating missing Tools folder : \"{ToolsRoot}\"", Tools.GetToolsRoot());
            if (!FileEx.CreateDirectory(Tools.GetToolsRoot()))
            {
                return false;
            }
        }

        // Download 7zr.exe in the tools root folder
        // https://www.7-zip.org/a/7zr.exe
        Log.Logger.Information("Downloading \"7zr.exe\" ...");
        string sevenZr = Tools.CombineToolPath("7zr.exe");
        if (!Download.DownloadFile(new Uri("https://www.7-zip.org/a/7zr.exe"), sevenZr))
        {
            return false;
        }

        // Get the latest version of 7z
        if (!GetLatestVersionWindows(out MediaToolInfo mediaToolInfo))
        {
            return false;
        }

        // Download the latest version in the tools root folder
        Log.Logger.Information("Downloading \"{FileName}\" ...", mediaToolInfo.FileName);
        string updateFile = Tools.CombineToolPath(mediaToolInfo.FileName);
        if (!Download.DownloadFile(new Uri(mediaToolInfo.Url), updateFile))
        {
            return false;
        }

        // Follow the pattern from Update()

        // Use 7zr.exe to extract the archive to the tools folder
        Log.Logger.Information("Extracting {UpdateFile} ...", updateFile);
        string extractPath = Tools.CombineToolPath(Path.GetFileNameWithoutExtension(updateFile));
        string commandline = $"x -aoa -spe -y \"{updateFile}\" -o\"{extractPath}\"";
        int exitCode = ProcessEx.Execute(sevenZr, commandline);
        if (exitCode != 0)
        {
            Log.Logger.Error("Failed to extract archive : ExitCode: {ExitCode}", exitCode);
            return false;
        }

        // Delete the tool destination directory
        string toolPath = GetToolFolder();
        if (!FileEx.DeleteDirectory(toolPath, true))
        {
            return false;
        }

        // Rename the folder
        // E.g. 7z1805-extra to .\Tools\7Zip
        return FileEx.RenameFolder(extractPath, toolPath);
    }

    private const string InstalledVersionPattern = @"7-Zip(?:\s+\(.*?\))?(?:\s+\[\d+\])?\s+(?<version>\d+\.\d+)";
    [GeneratedRegex(InstalledVersionPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    public static partial Regex InstalledVersionRegex();
}
