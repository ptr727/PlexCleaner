using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using InsaneGenius.Utilities;
using Serilog;

namespace PlexCleaner;

public static class Tools
{
    // Tool details are populated during VerifyTools() call
    public static readonly FfMpegTool FfMpeg = new();
    public static readonly FfProbeTool FfProbe = new();
    public static readonly MkvMergeTool MkvMerge = new();
    public static readonly MkvPropEditTool MkvPropEdit = new();
    public static readonly MkvExtractTool MkvExtract = new();
    public static readonly MediaInfoTool MediaInfo = new();
    public static readonly HandBrakeTool HandBrake = new();
    public static readonly SevenZipTool SevenZip = new();

    public static List<MediaTool> GetToolList() =>
        [FfMpeg, FfProbe, MkvMerge, MkvPropEdit, MkvExtract, MediaInfo, HandBrake, SevenZip];

    public static List<MediaTool> GetToolFamilyList() =>
        [FfMpeg, MkvMerge, MediaInfo, HandBrake, SevenZip];

    public static bool VerifyTools()
    {
        // TODO: Folder tools are not supported on Linux
        if (
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && !Program.Config.ToolsOptions.UseSystem
        )
        {
            Log.Warning("Folder tools are not supported on Linux");
            Log.Warning("Set 'ToolsOptions:UseSystem' to 'true' on Linux");
            Program.Config.ToolsOptions.UseSystem = true;
        }

        // Verify tools populates the tool information
        if (Program.Config.ToolsOptions.UseSystem ? VerifySystemTools() : VerifyFolderTools())
        {
            GetToolList()
                .ForEach(tool =>
                    Log.Information(
                        "{Tool} : Version: {Version}, Path: {FileName}",
                        tool.GetToolType(),
                        tool.Info.Version,
                        tool.Info.FileName
                    )
                );
            return true;
        }
        return false;
    }

    private static bool VerifySystemTools()
    {
        // Verify each tool
        foreach (MediaTool mediaTool in GetToolList())
        {
            // Query the installed version information
            if (!mediaTool.GetInstalledVersion(out MediaToolInfo mediaToolInfo))
            {
                Log.Error(
                    "{Tool} not found : {FileName}",
                    mediaTool.GetToolType(),
                    mediaTool.GetToolPath()
                );
                return false;
            }
            mediaTool.Info = mediaToolInfo;
        }

        return true;
    }

    private static bool VerifyFolderTools()
    {
        // Make sure the tools root folder exists
        if (!Directory.Exists(GetToolsRoot()))
        {
            Log.Error("Tools directory not found : {Directory}", GetToolsRoot());
            return false;
        }

        // Look for Tools.json
        string toolsFile = GetToolsJsonPath();
        if (!File.Exists(toolsFile))
        {
            Log.Error("{FileName} not found, run the 'checkfornewtools' command", toolsFile);
            return false;
        }

        // Deserialize
        ToolInfoJsonSchema toolInfoJson = ToolInfoJsonSchema.FromFile(toolsFile);
        if (toolInfoJson == null)
        {
            Log.Error("{FileName} is not a valid JSON file", toolsFile);
            return false;
        }

        // Compare schema version
        if (toolInfoJson.SchemaVersion != ToolInfoJsonSchema.CurrentSchemaVersion)
        {
            Log.Error(
                "Tool JSON schema mismatch : {JsonSchemaVersion} != {CurrentSchemaVersion}, {FileName}",
                toolInfoJson.SchemaVersion,
                ToolInfoJsonSchema.CurrentSchemaVersion,
                toolsFile
            );

            // Upgrade schema
            if (!ToolInfoJsonSchema.Upgrade(toolInfoJson))
            {
                return false;
            }
        }

        // Verify each tool
        foreach (MediaTool mediaTool in GetToolList())
        {
            // Lookup using the tool family
            MediaToolInfo mediaToolInfo = toolInfoJson.GetToolInfo(mediaTool);
            if (mediaToolInfo == null)
            {
                Log.Error("{Tool} not found in Tools.json", mediaTool.GetToolFamily());
                return false;
            }

            // Make sure the tool exists
            // Query the installed version information
            if (
                !File.Exists(mediaTool.GetToolPath())
                || !mediaTool.GetInstalledVersion(out mediaToolInfo)
            )
            {
                Log.Error(
                    "{Tool} not found in path {Directory}",
                    mediaTool.GetToolType(),
                    mediaTool.GetToolPath()
                );
                return false;
            }
            mediaTool.Info = mediaToolInfo;
        }

        return true;
    }

    public static string GetToolsRoot()
    {
        // System tools
        if (Program.Config.ToolsOptions.UseSystem)
        {
            return "";
        }

        // Process relative or absolute tools path
        if (!Program.Config.ToolsOptions.RootRelative)
        {
            // Return the absolute path
            return Program.Config.ToolsOptions.RootPath;
        }

        // Get the assembly directory
        string toolsRoot = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);

        // Create the root from the relative directory
        return Path.GetFullPath(Path.Combine(toolsRoot!, Program.Config.ToolsOptions.RootPath));
    }

    public static string CombineToolPath(string fileName) =>
        Path.GetFullPath(Path.Combine(GetToolsRoot(), fileName));

    public static string CombineToolPath(string path, string subPath, string fileName) =>
        Path.GetFullPath(Path.Combine(GetToolsRoot(), path, subPath, fileName));

    private static string GetToolsJsonPath() => CombineToolPath("Tools.json");

    public static bool CheckForNewTools()
    {
        // TODO: Checking for new tools are not supported on Linux
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Log.Warning("Checking for new tools are not supported on Linux");
            if (Program.Config.ToolsOptions.AutoUpdate)
            {
                Log.Warning("Set 'ToolsOptions:AutoUpdate' to 'false' on Linux");
                Program.Config.ToolsOptions.AutoUpdate = false;
            }

            // Nothing to do
            return true;
        }

        // 7-Zip must be installed
        if (!File.Exists(SevenZip.GetToolPath()))
        {
            // Bootstrap the 7-Zip download, only supported on Windows
            Log.Warning(
                "Downloading missing {Tool} ... : \"{ToolPath}\"",
                SevenZip.GetToolType(),
                SevenZip.GetToolPath()
            );
            if (!SevenZip.BootstrapDownload())
            {
                return false;
            }
            Debug.Assert(File.Exists(SevenZip.GetToolPath()));
        }

        Log.Information("Checking for new tools ...");

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
                    Log.Error(
                        "Tool JSON schema mismatch : {JsonSchemaVersion} != {CurrentSchemaVersion}",
                        toolInfoJson.SchemaVersion,
                        ToolInfoJsonSchema.CurrentSchemaVersion
                    );

                    // Upgrade Schema
                    if (!ToolInfoJsonSchema.Upgrade(toolInfoJson))
                    {
                        toolInfoJson = null;
                    }
                }
            }
            toolInfoJson ??= new ToolInfoJsonSchema();

            // Set the last check time
            toolInfoJson.LastCheck = DateTime.UtcNow;

            // Get a list of all tool family types
            List<MediaTool> toolList = GetToolFamilyList();
            foreach (MediaTool mediaTool in toolList)
            {
                // Get the latest version of the tool
                Log.Information("{Tool} : Getting latest version ...", mediaTool.GetToolFamily());
                if (!mediaTool.GetLatestVersion(out MediaToolInfo latestToolInfo))
                {
                    Log.Error("{Tool} : Failed to get latest version", mediaTool.GetToolFamily());
                    return false;
                }

                // Get the URL details
                Log.Information(
                    "{Tool} : Getting download URI details : {Uri}",
                    mediaTool.GetToolFamily(),
                    latestToolInfo.Url
                );
                if (!GetUrlDetails(latestToolInfo))
                {
                    Log.Error(
                        "{Tool} : Failed to get download URI details : {Uri}",
                        mediaTool.GetToolFamily(),
                        latestToolInfo.Url
                    );
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
                {
                    continue;
                }

                // Download the update file in the tools folder
                Log.Information("Downloading {FileName} ...", latestToolInfo.FileName);
                string downloadFile = CombineToolPath(latestToolInfo.FileName);
                if (!Download.DownloadFile(new Uri(latestToolInfo.Url), downloadFile))
                {
                    return false;
                }

                // Update the tool using the downloaded file
                if (!mediaTool.Update(downloadFile))
                {
                    _ = FileEx.DeleteFile(downloadFile);
                    return false;
                }

                // Update the tool info, do a deep copy to update the object in the list
                jsonToolInfo.Copy(latestToolInfo);

                // Delete the downloaded update file
                _ = FileEx.DeleteFile(downloadFile);

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
        if (
            !Download.GetContentInfo(
                new Uri(mediaToolInfo.Url),
                out long size,
                out DateTime modified
            )
        )
        {
            return false;
        }

        // Set retrieved values
        mediaToolInfo.Size = size;
        mediaToolInfo.ModifiedTime = modified;

        return true;
    }
}
