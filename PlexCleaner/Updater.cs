using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using InsaneGenius.Utilities;

namespace PlexCleaner
{
    public class ToolsJson
    {
        [JsonProperty("lastcheck")]
        public string LastCheck { get; set; }

        [JsonProperty("tools")]
        public List<ToolInfo> Tools { get; set; }

        public static string ToJson(ToolsJson tools) =>
            JsonConvert.SerializeObject(tools, Settings);

        public static ToolsJson FromJson(string json) =>
            JsonConvert.DeserializeObject<ToolsJson>(json, Settings);

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Formatting = Formatting.Indented
        };
    }

    public class ToolInfo
    {
        [JsonProperty("filename")]
        public string FileName { get; set; }

        [JsonProperty("modifiedtime")]
        public string ModifiedTime { get; set; }

        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("tool")]
        public string Tool { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        public void WriteLine(string prefix)
        {
            ConsoleEx.WriteLine($"{prefix} : {Version}, {FileName}, {Size}, {ModifiedTime}");
        }
    }

    public static class Updater
    {
        public static bool CheckForTools()
        {
            ConsoleEx.WriteLine("Checking for new tools ...");

            try
            {
                // Read the current tool versions from the JSON file
                string toolsfile = Tools.CombineToolPath("Tools.json");
                ToolsJson tools = null;
                if (File.Exists(toolsfile))
                    tools = ToolsJson.FromJson(File.ReadAllText(toolsfile));
                if (tools == null)
                    tools = new ToolsJson {Tools = new List<ToolInfo>()};
                tools.LastCheck = DateTime.UtcNow.ToString(DateTimeFormatInfo.InvariantInfo.UniversalSortableDateTimePattern);

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

                // TODO : Convert handcoded tools to enum and enumerate

                // Write json to file
                string json = ToolsJson.ToJson(tools);
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
            if (!Download.GetContentInfo(toolinfo.Url, out long size, out DateTime modified))
                return false;
            toolinfo.Size = size;
            toolinfo.ModifiedTime = modified.ToString(DateTimeFormatInfo.InvariantInfo.UniversalSortableDateTimePattern);
            return true;
        }

        private static bool UpdateTool(ToolsJson tools, ToolInfo toolinfo)
        {
            // Get the tool info
            bool download = false;
            ToolInfo tool = tools.Tools.FirstOrDefault(t => t.Tool.Equals(toolinfo.Tool));
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
                string filepath = Tools.CombineToolPath(toolinfo.FileName);
                if (!Download.DownloadFile(toolinfo.Url, filepath))
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
                        toolpath = Tools.CombineToolPath(Path.GetFileNameWithoutExtension(toolinfo.FileName));
                        break;
                    case nameof(FfMpegTool):
                        // FFMpeg archives have versioned folders in the zip
                        // The 7Zip -spe option does not work for zip files
                        // https://sourceforge.net/p/sevenzip/discussion/45798/thread/8cb61347/
                        // We need to extract to the root tools folder, that will create a subdir, then rename to the destination folder
                        toolpath = Tools.GetToolsRoot();
                        break;
                    case nameof(MkvTool):
                        toolpath = MkvTool.GetToolPath();
                        break;
                    case nameof(MediaInfoTool):
                        toolpath = MediaInfoTool.GetToolPath();
                        if (!FileEx.CreateDirectory(toolpath) ||
                            !FileEx.DeleteInsideDirectory(toolpath))
                            return false;
                        break;
                    case nameof(HandBrakeTool):
                        toolpath = HandBrakeTool.GetToolPath();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                // Make sure the output folder exists and is empty
                if (!FileEx.CreateDirectory(toolpath) ||
                    !FileEx.DeleteInsideDirectory(toolpath))
                    return false;

                // Extract the tool
                ConsoleEx.WriteLine($"Extracting \"{toolinfo.FileName}\" ...");
                if (!SevenZipTool.UnZip(filepath, toolpath))
                    return false;

                // Process the extracted folder
                switch (toolinfo.Tool)
                {
                    case nameof(SevenZipTool):
                        // Get the path and and clean the destination directory
                        toolpath = SevenZipTool.GetToolPath();
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
                        toolpath = FfMpegTool.GetToolPath();
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
                        throw new ArgumentOutOfRangeException();
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
