using InsaneGenius.Utilities;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PlexCleaner;

public static class Tools
{
    // All the tools
    public static readonly FfMpegTool FfMpeg = new();
    public static readonly FfProbeTool FfProbe = new();
    public static readonly MkvMergeTool MkvMerge = new();
    public static readonly MkvPropEditTool MkvPropEdit = new();
    public static readonly MediaInfoTool MediaInfo = new();
    public static readonly HandBrakeTool HandBrake = new();
    public static readonly SevenZipTool SevenZip = new();

    private static List<MediaTool> GetToolList()
    {
        // Add all tools to a list
        List<MediaTool> toolList = new()
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

    private static List<MediaTool> GetToolFamilyList()
    {
        // Add all tools families to a list
        List<MediaTool> toolList = new()
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
            Log.Logger.Warning("Folder tools are not supported on Linux");
            Log.Logger.Warning("Set 'ToolsOptions:UseSystem' to 'true' on Linux");
            Program.Config.ToolsOptions.UseSystem = true;
        }

        // Verify System tools or Tool folder tools
        return Program.Config.ToolsOptions.UseSystem ? VerifySystemTools() : VerifyFolderTools();
    }

    private static bool VerifySystemTools()
    {
        // Verify each tool
        List<MediaTool> toolList = GetToolList();
        foreach (MediaTool mediaTool in toolList)
        {
            // Query the installed version information
            if (!mediaTool.GetInstalledVersion(out MediaToolInfo mediaToolInfo))
            {
                Log.Logger.Error("{Tool} not found : {FileName}", mediaTool.GetToolType(), mediaTool.GetToolPath());
                return false;
            }
            Log.Logger.Information("{Tool} : Version: {Version}, Path: {FileName}", mediaTool.GetToolType(), mediaToolInfo.Version, mediaToolInfo.FileName);

            // Assign the tool info
            mediaTool.Info = mediaToolInfo;
        }

        return true;
    }

    private static bool VerifyFolderTools()
    {
        // Make sure the tools root folder exists
        if (!Directory.Exists(GetToolsRoot()))
        {
            Log.Logger.Error("Tools directory not found : {Directory}", GetToolsRoot());
            return false;
        }

        // Look for Tools.json
        string toolsFile = GetToolsJsonPath();
        if (!File.Exists(toolsFile))
        {
            Log.Logger.Error("{FileName} not found, run the 'checkfornewtools' command", toolsFile);
            return false;
        }

        // Deserialize and compare the schema version
        ToolInfoJsonSchema toolInfoJson = ToolInfoJsonSchema.FromJson(File.ReadAllText(toolsFile));
        if (toolInfoJson.SchemaVersion != ToolInfoJsonSchema.CurrentSchemaVersion)
        {
            Log.Logger.Error("Tool JSON schema mismatch : {JsonSchemaVersion} != {CurrentSchemaVersion}, {FileName}",
                toolInfoJson.SchemaVersion,
                ToolInfoJsonSchema.CurrentSchemaVersion,
                toolsFile);
            // Upgrade schema
            if (!ToolInfoJsonSchema.Upgrade(toolInfoJson))
                return false;
        }

        // Verify each tool
        List<MediaTool> toolList = GetToolList();
        foreach (MediaTool mediaTool in toolList)
        {
            // Lookup using the tool family
            MediaToolInfo mediaToolInfo = toolInfoJson.GetToolInfo(mediaTool);
            if (mediaToolInfo == null)
            {
                Log.Logger.Error("{Tool} not found in Tools.json", mediaTool.GetToolFamily());
                return false;
            }

            // Make sure the tool exists
            // Query the installed version information
            if (!File.Exists(mediaTool.GetToolPath()) ||
                !mediaTool.GetInstalledVersion(out mediaToolInfo))
            {
                Log.Logger.Error("{Tool} not found in path {Directory}", mediaTool.GetToolType(), mediaTool.GetToolPath());
                return false;
            }
            Log.Logger.Information("{Tool} : Version: {Version}, Path: {FileName}",
                mediaTool.GetToolType(),
                mediaToolInfo.Version,
                mediaToolInfo.FileName);

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
        string toolsRoot = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);

        // Create the root from the relative directory
        return Path.GetFullPath(Path.Combine(toolsRoot!, Program.Config.ToolsOptions.RootPath));
    }

    public static string CombineToolPath(string filename)
    {
        return Path.GetFullPath(Path.Combine(GetToolsRoot(), filename));
    }

    public static string CombineToolPath(string path, string filename)
    {
        return Path.GetFullPath(Path.Combine(GetToolsRoot(), path, filename));
    }

    public static string CombineToolPath(string path, string subPath, string filename)
    {
        return Path.GetFullPath(Path.Combine(GetToolsRoot(), path, subPath, filename));
    }

    private static string GetToolsJsonPath()
    {
        return CombineToolPath("Tools.json");
    }

    public static bool CheckForNewTools()
    {
        // TODO: Checking for new tools are not currently supported on Linux
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Log.Logger.Warning("Checking for new tools are not supported on Linux");
            if (Program.Config.ToolsOptions.AutoUpdate)
            {
                Log.Logger.Warning("Set 'ToolsOptions:AutoUpdate' to 'false' on Linux");
                Program.Config.ToolsOptions.AutoUpdate = false;
            }

            // Nothing to do
            return true;
        }

        // 7-Zip must be installed
        if (!File.Exists(SevenZip.GetToolPath()))
        {
            Log.Logger.Error("{Tool} not found : {Directory}", SevenZip.GetToolType(), SevenZip.GetToolPath());
            return false;
        }

        Log.Logger.Information("Checking for new tools ...");

        try
        {
            // Read the current tool versions from the JSON file
            string toolsFile = GetToolsJsonPath();
            ToolInfoJsonSchema toolInfoJson = null;
            if (File.Exists(toolsFile))
            {
                // Deserialize and compare the schema version
                toolInfoJson = ToolInfoJsonSchema.FromFile(toolsFile);
                if (toolInfoJson.SchemaVersion != ToolInfoJsonSchema.CurrentSchemaVersion)
                {
                    Log.Logger.Error("Tool JSON schema mismatch : {JsonSchemaVersion} != {CurrentSchemaVersion}",
                        toolInfoJson.SchemaVersion,
                        ToolInfoJsonSchema.CurrentSchemaVersion);

                    // Upgrade Schema
                    if (!ToolInfoJsonSchema.Upgrade(toolInfoJson))
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
                Log.Logger.Information("Getting latest version of {Tool} ...", mediaTool.GetToolFamily());
                if (!mediaTool.GetLatestVersion(out MediaToolInfo latestToolInfo) ||
                    // Get the URL details
                    !GetUrlDetails(latestToolInfo))
                {
                    Log.Logger.Error("{Tool} : Failed to get latest version", mediaTool.GetToolFamily());
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
                Log.Logger.Information("Downloading {FileName} ...", latestToolInfo.FileName);
                string downloadFile = CombineToolPath(latestToolInfo.FileName);
                if (!Download.DownloadFile(new Uri(latestToolInfo.Url), downloadFile))
                    return false;

                // Update the tool using the downloaded file
                if (!mediaTool.Update(downloadFile))
                {
                    FileEx.DeleteFile(downloadFile);
                    return false;
                }

                // Update the tool info, do a deep copy to update the object in the list
                jsonToolInfo.Copy(latestToolInfo);

                // Delete the downloaded update file
                FileEx.DeleteFile(downloadFile);

                // Next tool
            }

            // Write updated JSON to file
            ToolInfoJsonSchema.ToFile(toolsFile, toolInfoJson);
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
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