using InsaneGenius.Utilities;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PlexCleaner
{
    public static class Tools
    {
        public static bool VerifyTools(out ToolInfoJsonSchema toolInfo)
        {
            toolInfo = null;

            // Make sure the tools root folder exists
            if (!Directory.Exists(GetToolsRoot()))
            {
                ConsoleEx.WriteLineError($"Tools directory not found : \"{GetToolsRoot()}\"");
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
            string toolsfile = GetToolsJsonPath();
            if (!File.Exists(toolsfile))
            {
                ConsoleEx.WriteLineError($"Tools.json not found, run the 'checkfornewtools' command : \"{GetToolsJsonPath()}\"");
                return false;
            }

            // Read the current tool versions from the JSON file
            toolInfo = ToolInfoJsonSchema.FromJson(File.ReadAllText(toolsfile));

            return true;
        }

        public static string GetToolsRoot()
        {
            // Process relative or absolute tools path
            if (!Program.Config.ToolsOptions.RootRelative)
                // Return the absolute path
                return Program.Config.ToolsOptions.RootPath;
            
            // Get the assembly directory
            string toolsroot = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

            // Create the root from the relative directory
            return Path.GetFullPath(Path.Combine(toolsroot, Program.Config.ToolsOptions.RootPath));
        }

        public static string CombineToolPath(string filename)
        {
            return Path.GetFullPath(Path.Combine(GetToolsRoot(), filename));
        }

        public static string CombineToolPath(string path, string filename)
        {
            return Path.GetFullPath(Path.Combine(GetToolsRoot(), path, filename));
        }

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
                ToolInfoJsonSchema tools = null;
                if (File.Exists(toolsfile))
                    tools = ToolInfoJsonSchema.FromJson(File.ReadAllText(toolsfile));
                tools ??= new ToolInfoJsonSchema();
                tools.LastCheck = DateTime.UtcNow;

                // 7-Zip
                ConsoleEx.WriteLine("Getting latest version of 7-Zip ...");
                ToolInfo toolinfo = new ToolInfo();
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
                toolinfo = new ToolInfo();
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
                toolinfo = new ToolInfo();
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
                toolinfo = new ToolInfo();
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
                toolinfo = new ToolInfo();
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

                // TODO : Convert hardcoded tools to enum and enumerate

                // Write json to file
                string json = ToolInfoJsonSchema.ToJson(tools);
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
            toolinfo.ModifiedTime = modified;
            return true;
        }

        private static bool UpdateTool(ToolInfoJsonSchema tools, ToolInfo toolinfo)
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
                download = !toolinfo.Equals(tool);
            }

            // Download and extract new tools
            if (!download) 
                return true;
            
            // Download the file
            ConsoleEx.WriteLine($"Downloading \"{toolinfo.FileName}\" ...");
            string filepath = CombineToolPath(toolinfo.FileName);
            if (!Download.DownloadFile(new Uri(toolinfo.Url), filepath))
                return false;

            // Get the tool folder name
            string toolpath = toolinfo.Tool switch
            {
                nameof(SevenZipTool) =>
                // We need to keep the previous copy of 7zip so we can extract the new copy
                // We need to extract to a temp location in the root tools folder, then rename to the destination folder
                // Build the versioned folder from the downloaded filename
                // E.g. 7z1805-extra.7z to .\Tools\7z1805-extra
                CombineToolPath(Path.GetFileNameWithoutExtension(toolinfo.FileName)),
                nameof(FfMpegTool) =>
                // FFmpeg archives have versioned folders in the zip
                // The 7Zip -spe option does not work for zip files
                // https://sourceforge.net/p/sevenzip/discussion/45798/thread/8cb61347/
                // We need to extract to the root tools folder, that will create a subdir, then rename to the destination folder
                GetToolsRoot(),
                nameof(MkvTool) => MkvTool.GetToolFolder(),
                nameof(MediaInfoTool) => MediaInfoTool.GetToolFolder(),
                nameof(HandBrakeTool) => HandBrakeTool.GetToolFolder(),
                _ => throw new NotImplementedException()
            };

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
                    string sourcepath = CombineToolPath(Path.GetFileNameWithoutExtension(toolinfo.FileName));

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
                    // E.g. ffmpeg-3.4-win64-static.zip to .\Tools\FFmpeg\ffmpeg-3.4-win64-static
                    sourcepath = CombineToolPath(Path.GetFileNameWithoutExtension(toolinfo.FileName));

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

            return true;
        }
    }
}
