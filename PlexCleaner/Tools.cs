using System;
using System.IO;
using System.Reflection;

namespace PlexCleaner
{
    public static class Tools
    {
        public static bool VerifyTools(Config config)
        {
            // Make sure the tools root folder exists
            // Make sure that the 7-Zip tool exists
            // We need at least 7-Zip to be able to download and extract the other tools
            return Directory.Exists(GetToolsRoot(config)) && SevenZipTool.VerifyTool(config);
        }

        public static string GetToolsRoot(Config config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            // Process relative or absolute tools path
            if (!config.RootRelative)
                // Return the absolute path
                return config.RootPath;
            
            // Get the assembly directory
            string toolsroot = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

            // Create the root from the relative directory
            return Path.GetFullPath(Path.Combine(toolsroot, config.RootPath));
        }

        public static string CombineToolPath(Config config, string filename)
        {
            return Path.GetFullPath(Path.Combine(GetToolsRoot(config), filename));
        }

        public static string CombineToolPath(Config config, string path, string filename)
        {
            return Path.GetFullPath(Path.Combine(GetToolsRoot(config), path, filename));
        }

        public static bool IsMkvFile(string filename)
        {
            return IsMkvExtension(Path.GetExtension(filename));
        }

        public static bool IsMkvFile(FileInfo fileinfo)
        {
            if (fileinfo == null)
                throw new ArgumentNullException(nameof(fileinfo));

            return IsMkvExtension(fileinfo.Extension);
        }

        public static bool IsMkvExtension(string extension)
        {
            if (extension == null)
                throw new ArgumentNullException(nameof(extension));

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
