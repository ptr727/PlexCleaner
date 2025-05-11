using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using CliWrap;
using CliWrap.Buffered;
using CliWrap.Builders;
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

    // Tool subfolder, e.g. /x64, /bin
    protected virtual string GetSubFolder() => "";

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

    // The tool info must be set during initialization
    // Version information is used in the sidecar tool logic
    public MediaToolInfo Info { get; set; }

    public string GetToolName() =>
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
            : throw new NotImplementedException();

    protected string GetLatestGitHubRelease(string repo)
    {
        Log.Information(
            "{Tool} : Getting latest version from GitHub : {Repo}",
            GetToolFamily(),
            repo
        );
        return GitHubRelease.GetLatestRelease(repo);
    }

    public int Command(string parameters)
    {
        parameters = parameters.Trim();
        Log.Information("Executing {ToolType} : {Parameters}", GetToolType(), parameters);
        return ProcessEx.Execute(GetToolPath(), parameters, !Program.Options.Parallel);
    }

    public int Command(string parameters, out string output)
    {
        parameters = parameters.Trim();
        Log.Information("Executing {ToolType} : {Parameters}", GetToolType(), parameters);
        return ProcessEx.Execute(GetToolPath(), parameters, false, 0, out output);
    }

    public int Command(string parameters, out string output, out string error)
    {
        parameters = parameters.Trim();
        Log.Information("Executing {ToolType} : {Parameters}", GetToolType(), parameters);
        return ProcessEx.Execute(GetToolPath(), parameters, false, 0, out output, out error);
    }

    public int Command(string parameters, int limit, out string output, out string error)
    {
        parameters = parameters.Trim();
        Log.Information("Executing {ToolType} : {Parameters}", GetToolType(), parameters);
        return ProcessEx.Execute(GetToolPath(), parameters, false, limit, out output, out error);
    }

    public bool Execute(Command command, out CommandResult commandResult)
    {
        commandResult = null;
        int processId = -1;
        try
        {
            CommandTask<CommandResult> task = command
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync(CancellationToken.None, Program.CancelToken());
            processId = task.ProcessId;
            Log.Information(
                "Executing {ToolType} : ProcessId: {ProcessId}, Arguments: {Arguments}",
                GetToolType(),
                processId,
                command.Arguments
            );

            commandResult = task.Task.GetAwaiter().GetResult();
            return task.Task.IsCompletedSuccessfully;
        }
        catch (OperationCanceledException)
        {
            Log.Error(
                "Cancelled execution of {ToolType} : ProcessId: {ProcessId}, Arguments: {Arguments}",
                GetToolType(),
                processId,
                command.Arguments
            );
            return false;
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            return false;
        }
    }

    public bool Execute(Command command, out BufferedCommandResult bufferedCommandResult)
    {
        bufferedCommandResult = null;
        int processId = -1;
        try
        {
            CommandTask<BufferedCommandResult> task = command
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(
                    Encoding.Default,
                    Encoding.Default,
                    CancellationToken.None,
                    Program.CancelToken()
                );
            processId = task.ProcessId;
            Log.Information(
                "Executing {ToolType} : ProcessId: {ProcessId}, Arguments: {Arguments}",
                GetToolType(),
                processId,
                command.Arguments
            );

            bufferedCommandResult = task.Task.GetAwaiter().GetResult();
            return task.Task.IsCompletedSuccessfully;
        }
        catch (OperationCanceledException)
        {
            Log.Error(
                "Cancelled execution of {ToolType} : ProcessId: {ProcessId}, Arguments: {Arguments}",
                GetToolType(),
                processId,
                command.Arguments
            );
            return false;
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            return false;
        }
    }
}

public static class CliExtensions
{
    public static ArgumentsBuilder AddOption(
        this ArgumentsBuilder args,
        string name,
        string value
    ) =>
        string.IsNullOrEmpty(value) || string.IsNullOrWhiteSpace(value)
            ? args
            : args.Add(name).Add(value);
}
