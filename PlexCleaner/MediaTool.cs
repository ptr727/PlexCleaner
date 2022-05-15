using System;
using System.Runtime.InteropServices;
using InsaneGenius.Utilities;
using Serilog;

namespace PlexCleaner;

public abstract class MediaTool
{
    public enum ToolFamily
    {
        None,
        FfMpeg,
        HandBrake,
        MediaInfo,
        MkvToolNix,
        SevenZip
    }
    public enum ToolType
    {
        None,
        FfMpeg,
        FfProbe,
        HandBrake,
        MediaInfo,
        MkvMerge,
        MkvPropEdit,
        SevenZip,
        MkvExtract
    }
    public abstract ToolFamily GetToolFamily();
    public abstract ToolType GetToolType();
    // Tool binary name
    protected abstract string GetToolNameWindows();
    protected abstract string GetToolNameLinux();
    // Installed version information retrieved from the tool commandline
    public abstract bool GetInstalledVersion(out MediaToolInfo mediaToolInfo);
    // Latest downloadable version
    protected abstract bool GetLatestVersionWindows(out MediaToolInfo mediaToolInfo);
    protected abstract bool GetLatestVersionLinux(out MediaToolInfo mediaToolInfo);

    // Tools can override the default behavior as needed
    public virtual bool Update(string updateFile)
    {
        // Make sure the tool folder exists and is empty
        string toolPath = GetToolFolder();
        if (!FileEx.CreateDirectory(toolPath) ||
            !FileEx.DeleteInsideDirectory(toolPath))
        {
            return false;
        }

        // Extract the update file
        Log.Logger.Information("Extracting {UpdateFile} ...", updateFile);
        return Tools.SevenZip.UnZip(updateFile, toolPath);
    }

    // Tool subfolder, e.g. /x64, /bin
    // Used in GetToolPath()
    protected virtual string GetSubFolder()
    {
        return "";
    }

    // The tool info must be set during initialization
    // Version information is used in the sidecar tool logic
    public MediaToolInfo Info { get; set; }

    private string GetToolName()
    {
        // Windows or Linux
        // TODO: Mac may work the same as Linux, but untested
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? GetToolNameWindows() : GetToolNameLinux();
    }

    public string GetToolPath()
    {
        // Tool binary name
        string toolName = GetToolName();

        // System use just tool name
        // Append to tools folder using tool family type and sub folder as folder name
        return Program.Config.ToolsOptions.UseSystem ? toolName : Tools.CombineToolPath(GetToolFamily().ToString(), GetSubFolder(), toolName);
    }

    protected string GetToolFolder()
    {
        // Append to tools folder using tool family type as folder name
        // Sub folders are not included in the tool folder
        return Tools.CombineToolPath(GetToolFamily().ToString());
    }

    public bool GetLatestVersion(out MediaToolInfo mediaToolInfo)
    {
        // Windows or Linux
        // TODO: Mac may work the same as Linux, but untested
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? GetLatestVersionWindows(out mediaToolInfo) : GetLatestVersionLinux(out mediaToolInfo);
    }

    protected int Command(string parameters)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        parameters = parameters.Trim();
        Log.Logger.Information("Executing {ToolType} : {Parameters}", GetToolType(), parameters);

        // Suppress console output when running in parallel mode
        string path = GetToolPath();
        int exitCode = ProcessEx.Execute(path, parameters, !Program.Options.Parallel);
        return exitCode;
    }

    protected int Command(string parameters, out string output)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        parameters = parameters.Trim();
        Log.Logger.Information("Executing {ToolType} : {Parameters}", GetToolType(), parameters);

        string path = GetToolPath();
        int exitCode = ProcessEx.Execute(path, parameters, false, 0, out output);
        return exitCode;
    }

    protected int Command(string parameters, out string output, out string error)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        parameters = parameters.Trim();
        Log.Logger.Information("Executing {ToolType} : {Parameters}", GetToolType(), parameters);

        string path = GetToolPath();
        int exitCode = ProcessEx.Execute(path, parameters, false, 0, out output, out error);
        return exitCode;
    }

    protected int Command(string parameters, int limit, out string output, out string error)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        parameters = parameters.Trim();
        Log.Logger.Information("Executing {ToolType} : {Parameters}", GetToolType(), parameters);

        string path = GetToolPath();
        int exitCode = ProcessEx.Execute(path, parameters, false, limit, out output, out error);
        return exitCode;
    }
}
