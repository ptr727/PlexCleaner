using System;
using System.IO;

namespace PlexCleaner
{
    public static class Tools
    {
        public static string GetToolsRoot()
        {
            // Process relative or absolute tools path
            if (!Settings.Default.ToolsRootProcessRelative)
                // Return the absolute path
                return Settings.Default.ToolsRootPath;
            
            // Get the process directory
            string toolsroot = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
            if (toolsroot == null) throw new ArgumentNullException(nameof(toolsroot));

            // Create the root from the process relative directory
            return Path.GetFullPath(Path.Combine(toolsroot, Settings.Default.ToolsRootPath));
        }

        public static string CombineToolPath(string filename)
        {
            return Path.GetFullPath(Path.Combine(GetToolsRoot(), filename));
        }

        public static string CombineToolPath(string path, string filename)
        {
            return Path.GetFullPath(Path.Combine(GetToolsRoot(), path, filename));
        }

        // EchoArgs app
/*
        public static int EchoArgs(string parameters)
        {
            string path = CombineToolPath(Settings.Default.EchoArgs, @"echoargs.exe");
            return Execute(path, parameters);
        }
*/

        // Handbrake app
/*
        public static int Handbrake(string parameters)
        {
            string path = CombineToolPath(Settings.Default.Handbrake, @"handbrakecli.exe");
            return Execute(path, parameters);
        }
*/



        public static bool IsMkvFile(string filename)
        {
            return IsMkvExtension(Path.GetExtension(filename));
        }

        public static bool IsMkvFile(FileInfo fileinfo)
        {
            return IsMkvExtension(fileinfo.Extension);
        }

        public static bool IsMkvExtension(string extension)
        {
            return extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase);
        }

/*
        public static bool WildcardMatch(string match, string value)
        {
            // https://stackoverflow.com/questions/30299671/matching-strings-with-wildcard
            // ? - any character (one and only one)
            // * - any characters (zero or more)

            // Convert "*" and "?" to regex
            string regex = "^" + Regex.Escape(match).Replace("\\?", ".").Replace("\\*", ".*") + "$";

            // Match
            return Regex.IsMatch(value, regex);
        }
*/



    }
}
