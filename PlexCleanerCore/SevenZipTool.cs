using System;
using System.Net;
using System.Text.RegularExpressions;
using InsaneGenius.Utilities;

namespace PlexCleaner
{
    public static class SevenZipTool
    {
        public static bool UnZip(string archive, string folder)
        {
            // 7z.exe x archive.zip -o"C:\Doc"
            string commandline = $"x -aoa -spe -y \"{archive}\" -o\"{folder}\"";
            ConsoleEx.WriteLine("");
            int exitcode = SevenZip(commandline);
            ConsoleEx.WriteLine("");
            return exitcode == 0;
        }

        public static int SevenZip(string parameters)
        {
            string path = Tools.CombineToolPath(Settings.Default.SevenZip, SevenZipBinary);
            return ProcessEx.Execute(path, parameters);
        }

        public static string GetToolPath()
        {
            return Tools.CombineToolPath(Settings.Default.SevenZip);
        }

        public static bool GetLatestVersion(ToolInfo toolinfo)
        {
            try
            {
                // Load the download page
                // TODO : Find a more reliable way of getting the last released version number
                // https://www.7-zip.org/download.html
                WebClient wc = new WebClient();
                string downloadpage = wc.DownloadString("https://www.7-zip.org/download.html");

                // Extract the version number from the page source
                // E.g. "Download 7-Zip 18.05 (2018-04-30) for Windows"
                // https://regex101.com/
                const string pattern = @"Download\ 7-Zip\ (?<major>.*?)\.(?<minor>.*?)\ \((?<date>.*?)\)\ for\ Windows";
                Regex regex = new Regex(pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
                Match match = regex.Match(downloadpage);
                toolinfo.Version = $"{match.Groups["major"].Value}.{match.Groups["minor"].Value}";

                // Create download URL and the output filename using the version number
                // E.g. https://www.7-zip.org/a/7z1805-extra.7z
                toolinfo.FileName = $"7z{match.Groups["major"].Value}{match.Groups["minor"].Value}-extra.7z";
                toolinfo.Url = $"https://www.7-zip.org/a/{toolinfo.FileName}";
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return true;
        }

        private const string SevenZipBinary = @"x64\7za.exe";
    }
}
