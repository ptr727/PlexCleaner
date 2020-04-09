using InsaneGenius.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PlexCleaner
{
    public static class Tools
    {
        public static ToolsOptions Options { get; set; } = new ToolsOptions();

        public static bool VerifyTools()
        {
            // Make sure the tools root folder exists
            if (!Directory.Exists(GetToolsRoot()))
            {
                ConsoleEx.WriteLineError($"Tools directory not found : \"{Tools.GetToolsRoot()}\"");
                return false;
            }

            // Make sure the 7-Zip tool exists
            if (!File.Exists(SevenZipTool.GetToolPath()))
            {
                ConsoleEx.WriteLineError($"7-Zip not found : \"{SevenZipTool.GetToolPath()}\"");
                return false;
            }

            // Make sure the FFmpeg tool exists
            if (!File.Exists(FfMpegTool.GetToolPath()))
            {
                ConsoleEx.WriteLineError($"FFmpeg not found : \"{FfMpegTool.GetToolPath()}\"");
                return false;
            }

            // Make sure the HandBrake tool exists
            if (!File.Exists(HandBrakeTool.GetToolPath()))
            {
                ConsoleEx.WriteLineError($"HandBrake not found : \"{HandBrakeTool.GetToolPath()}\"");
                return false;
            }

            // Make sure the MediaInfo tool exists
            if (!File.Exists(MediaInfoTool.GetToolPath()))
            {
                ConsoleEx.WriteLineError($"MediaInfo not found : \"{MediaInfoTool.GetToolPath()}\"");
                return false;
            }

            // Make sure the MKVToolNix folder exists
            // We just test for the directory not each tool
            if (!Directory.Exists(MkvTool.GetToolFolder()))
            {
                ConsoleEx.WriteLineError($"MKVToolNix not found : \"{MkvTool.GetToolFolder()}\"");
                return false;
            }

            // Look for Tools.json
            if (!File.Exists(GetToolsJsonPath()))
            {
                ConsoleEx.WriteLineError($"Tools.json not found, run the 'checkfornewtools' command : \"{GetToolsJsonPath()}\"");
                return false;
            }

            return true;
        }

        public static string GetToolsRoot()
        {
            // Process relative or absolute tools path
            if (!Options.RootRelative)
                // Return the absolute path
                return Options.RootPath;
            
            // Get the assembly directory
            string toolsroot = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

            // Create the root from the relative directory
            return Path.GetFullPath(Path.Combine(toolsroot, Options.RootPath));
        }

        public static string CombineToolPath(string filename)
        {
            return Path.GetFullPath(Path.Combine(GetToolsRoot(), filename));
        }

        public static string CombineToolPath(string path, string filename)
        {
            return Path.GetFullPath(Path.Combine(GetToolsRoot(), path, filename));
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

        public static string GetToolsJsonPath()
        {
            return CombineToolPath("Tools.json");
        }

        public static bool CheckForNewTools()
        {
            ConsoleEx.WriteLine("Checking for new tools ...");

            try
            {
                // Read the current tool versions from the JSON file
                string toolsfile = GetToolsJsonPath();
                ToolInfoSettings tools = null;
                if (File.Exists(toolsfile))
                    tools = ToolInfoSettings.FromJson(File.ReadAllText(toolsfile));
                if (tools == null)
                    tools = new ToolInfoSettings { Tools = new List<ToolInfo>() };
                tools.LastCheck = DateTime.UtcNow.ToString(DateTimeFormatInfo.InvariantInfo.UniversalSortableDateTimePattern, CultureInfo.InvariantCulture);

                // 7-Zip
                ConsoleEx.WriteLine("Getting latest version of 7-Zip ...");
                ToolInfo toolinfo = new ToolInfo { Tool = nameof(SevenZipTool) };
                if (SevenZipTool.GetLatestVersion(toolinfo) &&
                    GetUrlDetails(toolinfo))
                {
                    // Update the tool
                    if (!UpdateTool(tools, toolinfo))
                        return false;
                }
                else
                {
                    ConsoleEx.WriteLineError($"Error getting latest version of 7-Zip : {toolinfo.Url}");
                    return false;
                }

                // MKVToolNix
                ConsoleEx.WriteLine("Getting latest version of MKVToolNix ...");
                toolinfo.Tool = nameof(MkvTool);
                if (MkvTool.GetLatestVersion(toolinfo) &&
                    GetUrlDetails(toolinfo))
                {
                    // Update the tool
                    if (!UpdateTool(tools, toolinfo))
                        return false;
                }
                else
                {
                    ConsoleEx.WriteLineError($"Error getting latest version of MKVToolNix : {toolinfo.Url}");
                    return false;
                }

                // FFmpeg
                ConsoleEx.WriteLine("Getting latest version of FFmpeg ...");
                toolinfo.Tool = nameof(FfMpegTool);
                if (FfMpegTool.GetLatestVersion(toolinfo) &&
                    GetUrlDetails(toolinfo))
                {
                    // Update the tool
                    if (!UpdateTool(tools, toolinfo))
                        return false;
                }
                else
                {
                    ConsoleEx.WriteLineError($"Error getting latest version of FFmpeg : {toolinfo.Url}");
                    return false;
                }

                // MediaInfo
                ConsoleEx.WriteLine("Getting latest version of MediaInfo ...");
                toolinfo.Tool = nameof(MediaInfoTool);
                if (MediaInfoTool.GetLatestVersion(toolinfo) &&
                    GetUrlDetails(toolinfo))
                {
                    // Update the tool
                    if (!UpdateTool(tools, toolinfo))
                        return false;
                }
                else
                {
                    ConsoleEx.WriteLineError($"Error getting latest version of MediaInfo : {toolinfo.Url}");
                    return false;
                }

                // HandBrake
                ConsoleEx.WriteLine("Getting latest version of HandBrake ...");
                toolinfo.Tool = nameof(HandBrakeTool);
                if (HandBrakeTool.GetLatestVersion(toolinfo) &&
                    GetUrlDetails(toolinfo))
                {
                    // Update the tool
                    if (!UpdateTool(tools, toolinfo))
                        return false;
                }
                else
                {
                    ConsoleEx.WriteLineError($"Error getting latest version of HandBrake : {toolinfo.Url}");
                    return false;
                }

                // TODO : Convert handcoded tools to enum and enumerate

                // Write json to file
                string json = ToolInfoSettings.ToJson(tools);
                File.WriteAllText(toolsfile, json);
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return true;
        }

        private static bool GetUrlDetails(ToolInfo toolinfo)
        {
            if (!Download.GetContentInfo(new Uri(toolinfo.Url), out long size, out DateTime modified))
                return false;

            toolinfo.Size = size;
            toolinfo.ModifiedTime = modified.ToString(DateTimeFormatInfo.InvariantInfo.UniversalSortableDateTimePattern, CultureInfo.InvariantCulture);
            return true;
        }

        private static bool UpdateTool(ToolInfoSettings tools, ToolInfo toolinfo)
        {
            // Get the tool info
            bool download = false;
            ToolInfo tool = tools.Tools.FirstOrDefault(t => t.Tool.Equals(toolinfo.Tool, StringComparison.OrdinalIgnoreCase));
            if (tool == null)
            {
                // No tool found, create a new entry for this tool
                tool = new ToolInfo
                {
                    Tool = toolinfo.Tool
                };
                tools.Tools.Add(tool);
                download = true;
            }
            else
            {
                // Tool found, compare the last filename
                tool.WriteLine("Current Version");
                toolinfo.WriteLine("Latest Version");
                if (!toolinfo.FileName.Equals(tool.FileName, StringComparison.OrdinalIgnoreCase))
                    download = true;
            }

            // Download and extract new tools
            if (download)
            {
                // Download the file
                ConsoleEx.WriteLine($"Downloading \"{toolinfo.FileName}\" ...");
                string filepath = CombineToolPath(toolinfo.FileName);
                if (!Download.DownloadFile(new Uri(toolinfo.Url), filepath))
                    return false;

                // Get the tool folder name
                string toolpath;
                switch (toolinfo.Tool)
                {
                    case nameof(SevenZipTool):
                        // We need to keep the previous copy of 7zip so we can extract the new copy
                        // We need to extract to a temp location in the root tools folder, then rename to the destination folder
                        // Build the versioned folder from the downloaded filename
                        // E.g. 7z1805-extra.7z to .\Tools\7z1805-extra
                        toolpath = CombineToolPath(Path.GetFileNameWithoutExtension(toolinfo.FileName));
                        break;
                    case nameof(FfMpegTool):
                        // FFMpeg archives have versioned folders in the zip
                        // The 7Zip -spe option does not work for zip files
                        // https://sourceforge.net/p/sevenzip/discussion/45798/thread/8cb61347/
                        // We need to extract to the root tools folder, that will create a subdir, then rename to the destination folder
                        toolpath = GetToolsRoot();
                        break;
                    case nameof(MkvTool):
                        toolpath = MkvTool.GetToolFolder();
                        break;
                    case nameof(MediaInfoTool):
                        toolpath = MediaInfoTool.GetToolFolder();
                        break;
                    case nameof(HandBrakeTool):
                        toolpath = HandBrakeTool.GetToolFolder();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(toolinfo));
                }

                // Make sure the tool folder exists and is empty
                // FfMpegTool will be in tools root, do not delete
                switch (toolinfo.Tool)
                {
                    // case nameof(FfMpegTool):
                    case nameof(SevenZipTool):
                    case nameof(MkvTool):
                    case nameof(MediaInfoTool):
                    case nameof(HandBrakeTool):
                        if (!FileEx.CreateDirectory(toolpath) ||
                            !FileEx.DeleteInsideDirectory(toolpath))
                            return false;
                        break;
                }

                // Extract the tool
                ConsoleEx.WriteLine($"Extracting \"{toolinfo.FileName}\" ...");
                if (!SevenZipTool.UnZip(filepath, toolpath))
                    return false;

                // Process the extracted folder
                switch (toolinfo.Tool)
                {
                    case nameof(SevenZipTool):
                        // Get the path and and clean the destination directory
                        toolpath = SevenZipTool.GetToolFolder();
                        if (!FileEx.DeleteDirectory(toolpath, true))
                            return false;

                        // Build the versioned folder from the downloaded filename
                        // E.g. 7z1805-extra.7z to .\Tools\7z1805-extra
                        string sourcepath = Tools.CombineToolPath(Path.GetFileNameWithoutExtension(toolinfo.FileName));

                        // Rename the folder
                        // E.g. 7z1805-extra to .\Tools\7Zip
                        if (!FileEx.RenameFolder(sourcepath, toolpath))
                            return false;
                        break;
                    case nameof(FfMpegTool):
                        // Get the path and and clean the destination directory
                        toolpath = FfMpegTool.GetToolFolder();
                        if (!FileEx.DeleteDirectory(toolpath, true))
                            return false;

                        // Build the versioned out folder from the downloaded filename
                        // E.g. ffmpeg-3.4-win64-static.zip to .\Tools\FFMpeg\ffmpeg-3.4-win64-static
                        sourcepath = Tools.CombineToolPath(Path.GetFileNameWithoutExtension(toolinfo.FileName));

                        // Rename the source folder to the tool folder
                        // E.g. ffmpeg-3.4-win64-static to .\Tools\FFMpeg
                        if (!FileEx.RenameFolder(sourcepath, toolpath))
                            return false;
                        break;
                    case nameof(MkvTool):
                    case nameof(MediaInfoTool):
                    case nameof(HandBrakeTool):
                        // Nothing to do
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(toolinfo));
                }

                // Update the tool information
                tool.FileName = toolinfo.FileName;
                tool.ModifiedTime = toolinfo.ModifiedTime;
                tool.Size = toolinfo.Size;
                tool.Url = toolinfo.Url;
                tool.Version = toolinfo.Version;
            }

            return true;
        }
    }
}
