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
        SevenZip,
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
        MkvExtract,
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
        if (!FileEx.CreateDirectory(toolPath) || !FileEx.DeleteInsideDirectory(toolPath))
        {
            return false;
        }

        // Extract the update file
        Log.Information("Extracting {UpdateFile} ...", updateFile);
        return Tools.SevenZip.UnZip(updateFile, toolPath);
    }

    // Tool subfolder, e.g. /x64, /bin
    // Used in GetToolPath()
    protected virtual string GetSubFolder() => "";

    // The tool info must be set during initialization
    // Version information is used in the sidecar tool logic
    public MediaToolInfo Info { get; set; }

    private string GetToolName() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? GetToolNameWindows()
            : GetToolNameLinux();

    public string GetToolPath() =>
        Program.Config.ToolsOptions.UseSystem
            ? GetToolName()
            : Tools.CombineToolPath(GetToolFamily().ToString(), GetSubFolder(), GetToolName());

    protected string GetToolFolder() => Tools.CombineToolPath(GetToolFamily().ToString());

    public bool GetLatestVersion(out MediaToolInfo mediaToolInfo) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? GetLatestVersionWindows(out mediaToolInfo)
            : GetLatestVersionLinux(out mediaToolInfo);

    protected int Command(string parameters)
    {
        parameters = parameters.Trim();
        Log.Information("Executing {ToolType} : {Parameters}", GetToolType(), parameters);
        return ProcessEx.Execute(GetToolPath(), parameters, !Program.Options.Parallel);
    }

    protected int Command(string parameters, out string output)
    {
        parameters = parameters.Trim();
        Log.Information("Executing {ToolType} : {Parameters}", GetToolType(), parameters);
        return ProcessEx.Execute(GetToolPath(), parameters, false, 0, out output);
    }

    protected int Command(string parameters, out string output, out string error)
    {
        parameters = parameters.Trim();
        Log.Information("Executing {ToolType} : {Parameters}", GetToolType(), parameters);
        return ProcessEx.Execute(GetToolPath(), parameters, false, 0, out output, out error);
    }

    protected int Command(string parameters, int limit, out string output, out string error)
    {
        parameters = parameters.Trim();
        Log.Information("Executing {ToolType} : {Parameters}", GetToolType(), parameters);
        return ProcessEx.Execute(GetToolPath(), parameters, false, limit, out output, out error);
    }

    protected string GetLatestGitHubRelease(string repo)
    {
        Log.Information(
            "{Tool} : Getting latest version from GitHub : {Repo}",
            GetToolFamily(),
            repo
        );
        return GitHubRelease.GetLatestRelease(repo);
    }
}
