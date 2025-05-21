using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;
using InsaneGenius.Utilities;
using Serilog;

// 7za <command> [<switches>...] <archive_name> [<file_names>...] [<@listfiles...>]

namespace PlexCleaner;

public partial class SevenZip
{
    public partial class Tool : MediaTool
    {
        public override ToolFamily GetToolFamily() => ToolFamily.SevenZip;

        public override ToolType GetToolType() => ToolType.SevenZip;

        protected override string GetToolNameWindows() => "7za.exe";

        protected override string GetToolNameLinux() => "7z";

        protected override string GetSubFolder() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "x64" : "";

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

            // "7-Zip (a) 24.09 (x86) : Copyright (c) 1999-2024 Igor Pavlov : 2024-11-29"
            // "7-Zip 24.08 (x64) : Copyright (c) 1999-2024 Igor Pavlov : 2024-08-11"

            // Parse version
            string[] lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Match match = InstalledVersionRegex().Match(lines[0]);
            Debug.Assert(match.Success && Version.TryParse(match.Groups["version"].Value, out _));
            mediaToolInfo.Version = match.Groups["version"].Value;
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

        public override bool Update(string updateFile)
        {
            // Keep the previous copy of 7zip so we can extract the new copy
            // Extract to a temp location in the root tools folder, then rename to the destination folder
            // Build the versioned folder from the downloaded filename
            // E.g. 7z1805-extra.7z to .\Tools\7z1805-extra
            string extractPath = Tools.CombineToolPath(
                Path.GetFileNameWithoutExtension(updateFile)
            );

            // Extract the update file
            Log.Information("Extracting {UpdateFile} ...", updateFile);
            if (!UnZip(updateFile, extractPath))
            {
                Log.Error("Failed to extract archive");
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

        public bool UnZip(string inputFile, string outputFolder) =>
            UnZip(GetBuilder(), inputFile, outputFolder);

        public bool UnZip(IGlobalOptions options, string inputFile, string outputFolder)
        {
            // Build commandline
            Command command = options
                .GlobalOptions(options => options.Add("x").Add("-aoa").Add("-spe").Add("-y"))
                .InputOptions(options => options.InputFile(inputFile))
                .OutputOptions(options => options.OutputFolder(outputFolder))
                .Build();

            // Execute command
            return Execute(command, out CommandResult result) && result.ExitCode == 0;
        }

        public bool BootstrapDownload()
        {
            // Make sure that the Tools folder exists
            Debug.Assert(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
            if (!Directory.Exists(Tools.GetToolsRoot()))
            {
                Log.Warning("Creating missing Tools folder : {ToolsRoot}", Tools.GetToolsRoot());
                if (!FileEx.CreateDirectory(Tools.GetToolsRoot()))
                {
                    return false;
                }
            }

            // Download 7zr.exe in the tools root folder
            // https://www.7-zip.org/a/7zr.exe
            Log.Information("Downloading \"7zr.exe\" ...");
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
            Log.Information("Downloading {FileName} ...", mediaToolInfo.FileName);
            string updateFile = Tools.CombineToolPath(mediaToolInfo.FileName);
            if (!Download.DownloadFile(new Uri(mediaToolInfo.Url), updateFile))
            {
                return false;
            }

            // Follow the pattern from Update()

            // Use 7zr.exe to extract the archive to the tools folder
            Log.Information("Extracting {UpdateFile} ...", updateFile);
            string extractPath = Tools.CombineToolPath(
                Path.GetFileNameWithoutExtension(updateFile)
            );
            if (!UnZip(Builder.Create(sevenZr), updateFile, extractPath))
            {
                Log.Error("Failed to extract archive");
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

        [GeneratedRegex(
            @"7-Zip(?:\s+\(.*?\))?(?:\s+\[\d+\])?\s+(?<version>\d+\.\d+)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline
        )]
        public static partial Regex InstalledVersionRegex();
    }
}
