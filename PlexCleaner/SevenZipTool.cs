using System;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using InsaneGenius.Utilities;
using System.Runtime.InteropServices;
using System.IO;

namespace PlexCleaner
{
    public class SevenZipTool : MediaTool
    {
        public override ToolFamily GetToolFamily()
        {
            return ToolFamily.SevenZip;
        }

        public override ToolType GetToolType()
        {
            return ToolType.SevenZip;
        }

        protected override string GetToolNameWindows()
        {
            return "7za.exe";
        }

        protected override string GetToolNameLinux()
        {
            return "7z";
        }

        public override bool GetInstalledVersion(out MediaToolInfo mediaToolInfo)
        {
            // Initialize            
            mediaToolInfo = new MediaToolInfo(this);

            // No version command, run with no arguments
            string commandline = "";
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
                // Load the download page
                // TODO : Find a more reliable way of getting the last released version number
                // https://www.7-zip.org/download.html
                using WebClient wc = new WebClient();
                string downloadpage = wc.DownloadString("https://www.7-zip.org/download.html");

                // Extract the version number from the page source
                // E.g. "Download 7-Zip 18.05 (2018-04-30) for Windows"
                const string pattern = @"Download\ 7-Zip\ (?<major>.*?)\.(?<minor>.*?)\ \((?<date>.*?)\)\ for\ Windows";
                Regex regex = new Regex(pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
                Match match = regex.Match(downloadpage);
                Debug.Assert(match.Success);
                mediaToolInfo.Version = $"{match.Groups["major"].Value}.{match.Groups["minor"].Value}";

                // Create download URL and the output filename using the version number
                // E.g. https://www.7-zip.org/a/7z1805-extra.7z
                mediaToolInfo.FileName = $"7z{match.Groups["major"].Value}{match.Groups["minor"].Value}-extra.7z";
                mediaToolInfo.Url = $"https://www.7-zip.org/a/{mediaToolInfo.FileName}";
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

        public override string GetSubFolder()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "x64";
            return "";
        }

        public override bool Update(string updateFile)
        {
            // We need to keep the previous copy of 7zip so we can extract the new copy
            // We need to extract to a temp location in the root tools folder, then rename to the destination folder
            // Build the versioned folder from the downloaded filename
            // E.g. 7z1805-extra.7z to .\Tools\7z1805-extra
            string extractPath = Tools.CombineToolPath(Path.GetFileNameWithoutExtension(updateFile));

            // Extract the update file
            ConsoleEx.WriteLine($"Extracting \"{updateFile}\" ...");
            if (!Tools.SevenZip.UnZip(updateFile, extractPath))
                return false;

            // Delete the tool destination directory
            string toolPath = GetToolFolder();
            if (!FileEx.DeleteDirectory(toolPath, true))
                return false;

            // Rename the folder
            // E.g. 7z1805-extra to .\Tools\7Zip
            if (!FileEx.RenameFolder(extractPath, toolPath))
                return false;

            // Done
            return true;
        }

        public bool UnZip(string archive, string folder)
        {
            // 7z.exe x archive.zip -o"C:\Doc"
            string commandline = $"x -aoa -spe -y \"{archive}\" -o\"{folder}\"";
            int exitcode = Command(commandline);
            return exitcode == 0;
        }
    }
}
