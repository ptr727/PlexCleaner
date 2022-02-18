using InsaneGenius.Utilities;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PlexCleaner;

public class SevenZipTool : MediaTool
{
    public override ToolFamily GetToolFamily()
    {
        return ToolFamily.SevenZip;
    }

    public override ToolType GetToolType()
    {
        return ToolType.SevenZip;
    }

    protected override string GetToolNameWindows()
    {
        return "7za.exe";
    }

    protected override string GetToolNameLinux()
    {
        return "7z";
    }

    public override bool GetInstalledVersion(out MediaToolInfo mediaToolInfo)
    {
        // Initialize            
        mediaToolInfo = new MediaToolInfo(this);

        // No version command, run with no arguments
        const string commandline = "";
        int exitcode = Command(commandline, out string output);
        if (exitcode != 0)
            return false;

        // First line as version
        // E.g. Windows : "7-Zip (a) 19.00 (x64) : Copyright (c) 1999-2018 Igor Pavlov : 2019-02-21"
        // E.g. Linux : "7-Zip [64] 16.02 : Copyright (c) 1999-2016 Igor Pavlov : 2016-05-21"
        string[] lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        // Extract the short version number
        const string pattern = @"7-Zip\ ([^\s]+)\ (?<version>.*?)\ ";
        Regex regex = new(pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
        Match match = regex.Match(lines[0]);
        Debug.Assert(match.Success);
        mediaToolInfo.Version = match.Groups["version"].Value;

        // Get tool filename
        mediaToolInfo.FileName = GetToolPath();

        // Get other attributes if we can read the file
        if (File.Exists(mediaToolInfo.FileName))
        {
            FileInfo fileInfo = new(mediaToolInfo.FileName);
            mediaToolInfo.ModifiedTime = fileInfo.LastWriteTimeUtc;
            mediaToolInfo.Size = fileInfo.Length;
        }

        return true;
    }

    protected override bool GetLatestVersionWindows(out MediaToolInfo mediaToolInfo)
    {
        // Initialize            
        mediaToolInfo = new MediaToolInfo(this);

        try
        {
            // Load the download page
            // TODO : Find a more reliable way of getting the last released version number
            // https://www.7-zip.org/download.html
            using HttpClient httpClient = new();
            string downloadPage = httpClient.GetStringAsync("https://www.7-zip.org/download.html").Result;

            // Extract the version number from the page source
            // E.g. "Download 7-Zip 18.05 (2018-04-30) for Windows"
            const string pattern = @"Download\ 7-Zip\ (?<major>.*?)\.(?<minor>.*?)\ \((?<date>.*?)\)\ for\ Windows";
            Regex regex = new(pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
            Match match = regex.Match(downloadPage);
            Debug.Assert(match.Success);
            mediaToolInfo.Version = $"{match.Groups["major"].Value}.{match.Groups["minor"].Value}";

            // Create download URL and the output filename using the version number
            // E.g. https://www.7-zip.org/a/7z1805-extra.7z
            mediaToolInfo.FileName = $"7z{match.Groups["major"].Value}{match.Groups["minor"].Value}-extra.7z";
            mediaToolInfo.Url = $"https://www.7-zip.org/a/{mediaToolInfo.FileName}";
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod().Name))
        {
            return false;
        }
        return true;
    }

    protected override bool GetLatestVersionLinux(out MediaToolInfo mediaToolInfo)
    {
        // Initialize            
        mediaToolInfo = new MediaToolInfo(this);

        // TODO
        return false;
    }

    protected override string GetSubFolder()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "x64" : "";
    }

    public override bool Update(string updateFile)
    {
        // We need to keep the previous copy of 7zip so we can extract the new copy
        // We need to extract to a temp location in the root tools folder, then rename to the destination folder
        // Build the versioned folder from the downloaded filename
        // E.g. 7z1805-extra.7z to .\Tools\7z1805-extra
        string extractPath = Tools.CombineToolPath(Path.GetFileNameWithoutExtension(updateFile));

        // Extract the update file
        Log.Logger.Information("Extracting {UpdateFile} ...", updateFile);
        if (!Tools.SevenZip.UnZip(updateFile, extractPath))
            return false;

        // Delete the tool destination directory
        string toolPath = GetToolFolder();
        if (!FileEx.DeleteDirectory(toolPath, true))
            return false;

        // Rename the folder
        // E.g. 7z1805-extra to .\Tools\7Zip
        return FileEx.RenameFolder(extractPath, toolPath);
    }

    public bool UnZip(string archive, string folder)
    {
        // 7z.exe x archive.zip -o"C:\Doc"
        string commandline = $"x -aoa -spe -y \"{archive}\" -o\"{folder}\"";
        int exitcode = Command(commandline);
        return exitcode == 0;
    }
}