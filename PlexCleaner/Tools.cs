using InsaneGenius.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PlexCleaner
{
    public static class Tools
    {
        // All the tools
        public static FfMpegTool FfMpeg = new FfMpegTool();
        public static FfProbeTool FfProbe = new FfProbeTool();
        public static MkvMergeTool MkvMerge = new MkvMergeTool();
        public static MkvPropEditTool MkvPropEdit = new MkvPropEditTool();
        public static MediaInfoTool MediaInfo = new MediaInfoTool();
        public static HandBrakeTool HandBrake = new HandBrakeTool();
        public static SevenZipTool SevenZip = new SevenZipTool();

        public static List<MediaTool> GetToolList()
        {
            // Add all tools to a list
            List<MediaTool> toolList = new List<MediaTool>
            {
                FfMpeg,
                FfProbe,
                MkvMerge,
                MkvPropEdit,
                MediaInfo,
                HandBrake,
                SevenZip
            };

            return toolList;
        }

        public static List<MediaTool> GetToolFamilyList()
        {
            // Add all tools families to a list
            List<MediaTool> toolList = new List<MediaTool>
            {
                FfMpeg,
                MkvMerge,
                MediaInfo,
                HandBrake,
                SevenZip
            };

            return toolList;
        }

        public static bool VerifyTools()
        {
            // TODO: Folder tools are not currently supported on Linux
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
                !Program.Config.ToolsOptions.UseSystem)
            {
                ConsoleEx.WriteLineError($"Warning : Forcing 'ToolsOptions:UseSystem` to 'true' on Linux");
                Program.Config.ToolsOptions.UseSystem = true;
            }

            // Verify System tools or Tool folder tools
            if (Program.Config.ToolsOptions.UseSystem)
                return VerifySystemTools();
            return VerifyFolderTools();
        }

        public static bool VerifySystemTools()
        {
            // Verify each tool
            List<MediaTool> toolList = GetToolList();
            foreach (MediaTool mediaTool in toolList)
            {
                // Query the installed version information from each tool
                if (!mediaTool.GetInstalledVersion(out MediaToolInfo mediaToolInfo))
                {
                    ConsoleEx.WriteLineError($"Error : {mediaTool.GetToolType()} not found : \"{mediaTool.GetToolPath()}\"");
                    return false;
                }
                ConsoleEx.WriteLine($"{mediaTool.GetToolType()} : Version: \"{mediaToolInfo.Version}\", Path: \"{mediaToolInfo.FileName}\"");

                // Assign the tool info
                mediaTool.Info = mediaToolInfo;
            }

            return true;
        }

        public static bool VerifyFolderTools()
        {
            // Make sure the tools root folder exists
            if (!Directory.Exists(GetToolsRoot()))
            {
                ConsoleEx.WriteLineError($"Error : Tools directory not found : \"{GetToolsRoot()}\"");
                return false;
            }

            // Look for Tools.json
            string toolsfile = GetToolsJsonPath();
            if (!File.Exists(toolsfile))
            {
                ConsoleEx.WriteLineError($"Error : \"{toolsfile}\" not found, run the 'checkfornewtools' command");
                return false;
            }

            // Deserialize and compare the schema version
            ToolInfoJsonSchema toolInfoJson = ToolInfoJsonSchema.FromJson(File.ReadAllText(toolsfile));
            if (toolInfoJson.SchemaVersion != ToolInfoJsonSchema.CurrentSchemaVersion)
            {
                ConsoleEx.WriteLine($"Error : Tool schema mismatch : {toolInfoJson.SchemaVersion} != {ToolInfoJsonSchema.CurrentSchemaVersion}");
                return false;
            }

            // Use the tool version numbers from the JSON file
            List<MediaTool> toolList = GetToolList();
            foreach (MediaTool mediaTool in toolList)
            {
                // Lookup using the tool family
                MediaToolInfo mediaToolInfo = toolInfoJson.GetToolInfo(mediaTool);
                if (mediaToolInfo == null)
                {
                    ConsoleEx.WriteLineError($"Error : {mediaTool.GetToolFamily()} not found in Tools.json, run the 'checkfornewtools' command");
                    return false;
                }

                // Make sure the tool exists
                if (!File.Exists(mediaTool.GetToolPath()))
                {
                    ConsoleEx.WriteLineError($"Error : {mediaTool.GetToolType()} not found : \"{mediaTool.GetToolPath()}\"");
                    return false;
                }

                ConsoleEx.WriteLine($"{mediaTool.GetToolType()} : Version: \"{mediaToolInfo.Version}\", Path: \"{mediaTool.GetToolPath()}\"");

                // Assign the tool info
                mediaTool.Info = mediaToolInfo;
            }

            return true;
        }

        public static string GetToolsRoot()
        {
            // System tools
            if (Program.Config.ToolsOptions.UseSystem)
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

        public static string CombineToolPath(string path, string subpath, string filename)
        {
            return Path.GetFullPath(Path.Combine(GetToolsRoot(), path, subpath, filename));
        }

        public static string GetToolsJsonPath()
        {
            return CombineToolPath("Tools.json");
        }

        public static bool CheckForNewTools()
        {
            // 7-Zip must be installed
            if (!File.Exists(SevenZip.GetToolPath()))
            {
                ConsoleEx.WriteLineError($"Error : {SevenZip.GetToolType()} not found : \"{SevenZip.GetToolPath()}\"");
                return false;
            }

            ConsoleEx.WriteLine("Checking for new tools ...");

            try
            {
                // Read the current tool versions from the JSON file
                string toolsFile = GetToolsJsonPath();
                ToolInfoJsonSchema toolInfoJson = null;
                if (File.Exists(toolsFile))
                {
                    // Deserialize and compare the schema version
                    toolInfoJson = ToolInfoJsonSchema.FromJson(File.ReadAllText(toolsFile));
                    if (toolInfoJson.SchemaVersion != ToolInfoJsonSchema.CurrentSchemaVersion)
                    {
                        ConsoleEx.WriteLine($"Warning : Tool schema mismatch : {toolInfoJson.SchemaVersion} != {ToolInfoJsonSchema.CurrentSchemaVersion}");
                        toolInfoJson = null;
                    }
                }
                toolInfoJson ??= new ToolInfoJsonSchema();

                // Set the last check time
                toolInfoJson.LastCheck = DateTime.UtcNow;

                // Get a list of all tool family types
                List<MediaTool> toolList = GetToolFamilyList();
                foreach (MediaTool mediaTool in toolList)
                {
                    // Get the latest version of each tool
                    ConsoleEx.WriteLine($"Getting latest version of {mediaTool.GetToolFamily()} ...");
                    if (!mediaTool.GetLatestVersion(out MediaToolInfo latestToolInfo) ||
                        // Get the URL details
                        !GetUrlDetails(latestToolInfo))
                    {
                        ConsoleEx.WriteLineError($"Error : {mediaTool.GetToolFamily()} : Failed to get latest version");
                        return false;
                    }

                    // Lookup in JSON file using the tool family
                    MediaToolInfo jsonToolInfo = toolInfoJson.GetToolInfo(mediaTool);
                    bool updateRequired;
                    if (jsonToolInfo == null)
                    {
                        // Add to list if not already registered
                        jsonToolInfo = latestToolInfo;
                        toolInfoJson.Tools.Add(jsonToolInfo);

                        // No current version
                        latestToolInfo.WriteLine("Latest Version");
                        updateRequired = true;
                    }
                    else 
                    {
                        // Print tool details
                        jsonToolInfo.WriteLine("Current Version");
                        latestToolInfo.WriteLine("Latest Version");

                        // Compare the latest version with the current version
                        updateRequired = jsonToolInfo.CompareTo(latestToolInfo) != 0;
                    }

                    // If no update is required continue
                    if (!updateRequired)
                        continue;

                    // Download the update file in the tools folder
                    ConsoleEx.WriteLine($"Downloading \"{latestToolInfo.FileName}\" ...");
                    string downloadFile = CombineToolPath(latestToolInfo.FileName);
                    if (!Download.DownloadFile(new Uri(latestToolInfo.Url), downloadFile))
                        return false;

                    // Update the tool using the downloaded file
                    if (!mediaTool.Update(downloadFile))
                    {
                        FileEx.DeleteFile(downloadFile);
                        return false;
                    }
                    ConsoleEx.WriteLine("");

                    // Update the tool info, do a deep copy to update the object in the list
                    jsonToolInfo.Copy(latestToolInfo);

                    // Delete the downloaded update file
                    FileEx.DeleteFile(downloadFile);

                    // Next tool
                }

                // Write updated JSON to file
                string json = ToolInfoJsonSchema.ToJson(toolInfoJson);
                File.WriteAllText(toolsFile, json);
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLineError(e);
                return false;
            }
            return true;
        }

        private static bool GetUrlDetails(MediaToolInfo mediaToolInfo)
        {
            // Get URL content details
            if (!Download.GetContentInfo(new Uri(mediaToolInfo.Url), out long size, out DateTime modified))
                return false;

            mediaToolInfo.Size = size;
            mediaToolInfo.ModifiedTime = modified;

            return true;
        }
    }
}
