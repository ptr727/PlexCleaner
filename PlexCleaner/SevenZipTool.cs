using System;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using InsaneGenius.Utilities;
using System.Runtime.InteropServices;

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

        public override bool GetInstalledVersion(out ToolInfo toolInfo)
        {
            // Initialize            
            toolInfo = new ToolInfo
            {
                Tool = GetToolType().ToString()
            };

            // No version command, run with no arguments
            string commandline = "";
            int exitcode = Command(commandline, out string output);
            if (exitcode != 0)
                return false;

            // First line as version
            string[] lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            toolInfo.Version = lines[0];

            // Get tool filename
            toolInfo.FileName = GetToolPath();

            return true;
        }

        public override bool GetLatestVersion(out ToolInfo toolInfo)
        {
            // Initialize            
            toolInfo = new ToolInfo
            {
                Tool = GetToolType().ToString()
            };

            // Windows or Linux
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return GetLatestVersionWindows(toolInfo);
            return GetLatestVersionLinux(toolInfo);
        }

        protected bool GetLatestVersionWindows(ToolInfo toolInfo)
        {
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
                toolInfo.Version = $"{match.Groups["major"].Value}.{match.Groups["minor"].Value}";

                // Create download URL and the output filename using the version number
                // E.g. https://www.7-zip.org/a/7z1805-extra.7z
                toolInfo.FileName = $"7z{match.Groups["major"].Value}{match.Groups["minor"].Value}-extra.7z";
                toolInfo.Url = $"https://www.7-zip.org/a/{toolInfo.FileName}";
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return true;
        }

        protected bool GetLatestVersionLinux(ToolInfo toolInfo)
        {
            // TODO
            return false;
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
