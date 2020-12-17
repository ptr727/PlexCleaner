using InsaneGenius.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PlexCleaner
{
    public static class Tools
    {
        public static FfMpegTool FfMpeg = new FfMpegTool();
        public static FfProbeTool FfProbe = new FfProbeTool();
        public static MkvMergeTool MkvMerge = new MkvMergeTool();
        public static MkvPropEditTool MkvPropEdit = new MkvPropEditTool();
        public static MediaInfoTool MediaInfo = new MediaInfoTool();
        public static HandBrakeTool HandBrake = new HandBrakeTool();
        public static SevenZipTool SevenZip = new SevenZipTool();

        public static bool VerifyTools()
        {
            // Add all tools to a list
            List<MediaTool> toolList = new List<MediaTool>();
            toolList.Add(FfMpeg);
            toolList.Add(FfProbe);
            toolList.Add(MkvMerge);
            toolList.Add(MkvPropEdit);
            toolList.Add(MediaInfo);
            toolList.Add(HandBrake);
            toolList.Add(SevenZip);

            // Verify System tools or Tool folder tools
             if (Program.Config.ToolsOptions.UseSystem)
                return VerifySystemTools(toolList);
            return VerifyFolderTools(toolList);
        }

        public static bool VerifySystemTools(List<MediaTool> toolList)
        {
            // Verify each tool
            foreach (MediaTool tool in toolList)
            {
                // Query the version information from each tool
                if (!tool.GetInstalledVersion(out ToolInfo toolInfo))
                {
                    ConsoleEx.WriteLineError($"Error : {tool.GetToolType().ToString()} not found : \"{tool.GetToolPath()}\"");
                    return false;
                }
                ConsoleEx.WriteLine($"{tool.GetToolType().ToString()} : Path: \"{toolInfo.FileName}\", Version: \"{toolInfo.Version}\"");
            }

            return true;
        }

        public static bool VerifyFolderTools(List<MediaTool> toolList)
        {
            // Make sure the tools root folder exists
            if (!Directory.Exists(GetToolsRoot()))
            {
                ConsoleEx.WriteLineError($"Error : Tools directory not found : \"{GetToolsRoot()}\"");
                return false;
            }

            // Verify each tool
            foreach (MediaTool tool in toolList)
            {
                // Make sure the tool exists
                if (!File.Exists(tool.GetToolPath()))
                {
                    ConsoleEx.WriteLineError($"Error : {tool.GetToolType().ToString()} not found : \"{tool.GetToolPath()}\"");
                    return false;
                }
            }

            // Look for Tools.json
            string toolsfile = GetToolsJsonPath();
            if (!File.Exists(toolsfile))
            {
                ConsoleEx.WriteLineError($"Error : Tools.json not found, run the 'checkfornewtools' command : \"{GetToolsJsonPath()}\"");
                return false;
            }

            // Use the tool version numbers from the Tools.json file
            ToolInfoJsonSchema toolInfo = ToolInfoJsonSchema.FromJson(File.ReadAllText(toolsfile));

            return true;
        }

        public static string GetToolsRoot()
        {
            // System tools
            if (!Program.Config.ToolsOptions.UseSystem)
                return "";

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
                ToolInfo toolinfo = null;
                if (Tools.SevenZip.GetLatestVersion(out toolinfo) &&
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
                toolinfo = null;
                if (Tools.MkvMerge.GetLatestVersion(out toolinfo) &&
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
                toolinfo = null;
                if (Tools.FfMpeg.GetLatestVersion(out toolinfo) &&
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
                toolinfo = null;
                if (Tools.MediaInfo.GetLatestVersion(out toolinfo) &&
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
                toolinfo = null;
                if (Tools.HandBrake.GetLatestVersion(out toolinfo) &&
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
            bool download;
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
                nameof(MkvMergeTool) => MkvMerge.GetToolFamily().ToString(),
                nameof(MediaInfoTool) => MediaInfo.GetToolFamily().ToString(),
                nameof(HandBrakeTool) => HandBrake.GetToolFamily().ToString(),
                _ => throw new NotImplementedException()
            };

            // Make sure the tool folder exists and is empty
            // FfMpegTool will be in tools root, do not delete
            switch (toolinfo.Tool)
            {
                // case nameof(FfMpegTool):
                case nameof(SevenZipTool):
                case nameof(MkvMergeTool):
                case nameof(MediaInfoTool):
                case nameof(HandBrakeTool):
                    if (!FileEx.CreateDirectory(toolpath) ||
                        !FileEx.DeleteInsideDirectory(toolpath))
                        return false;
                    break;
            }

            // Extract the tool
            ConsoleEx.WriteLine($"Extracting \"{toolinfo.FileName}\" ...");
            if (!SevenZip.UnZip(filepath, toolpath))
                return false;

            // Process the extracted folder
            switch (toolinfo.Tool)
            {
                case nameof(SevenZipTool):
                    // Get the path and and clean the destination directory
                    toolpath = SevenZip.GetToolFamily().ToString();
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
                    toolpath = FfMpeg.GetToolFamily().ToString();
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
                case nameof(MkvMergeTool):
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
