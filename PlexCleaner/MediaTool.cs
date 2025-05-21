using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using CliWrap;
using CliWrap.Buffered;
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

    public bool Execute(Command command, out BufferedCommandResult bufferedCommandResult) =>
        Execute(command, false, false, out bufferedCommandResult);

    public bool Execute(
        Command command,
        bool stdOutSummary,
        bool stdErrSummary,
        out BufferedCommandResult bufferedCommandResult
    )
    {
        bufferedCommandResult = null;
        int processId = -1;
        try
        {
            StringBuilder stdOutBuilder = new();
            PipeTarget stdOutTarget = stdOutSummary
                ? ToStringSummary(stdOutBuilder)
                : ToStringBuilder(stdOutBuilder);
            StringBuilder stdErrBuilder = new();
            PipeTarget stdErrTarget = stdErrSummary
                ? ToStringSummary(stdErrBuilder)
                : ToStringBuilder(stdErrBuilder);

            CommandTask<CommandResult> task = command
                .WithStandardOutputPipe(stdOutTarget)
                .WithStandardErrorPipe(stdErrTarget)
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync(CancellationToken.None, Program.CancelToken());
            processId = task.ProcessId;
            Log.Information(
                "Executing {ToolType} : ProcessId: {ProcessId}, Arguments: {Arguments}",
                GetToolType(),
                processId,
                command.Arguments
            );

            CommandResult commandResult = task.Task.GetAwaiter().GetResult();
            bufferedCommandResult = new(
                commandResult.ExitCode,
                commandResult.StartTime,
                commandResult.ExitTime,
                stdOutBuilder.ToString(),
                stdErrBuilder.ToString()
            );
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

    public static PipeTarget ToStringBuilder(StringBuilder stringBuilder) =>
        PipeTarget.Create(
            async (stream, cancellationToken) =>
            {
                using StreamReader reader = new(
                    stream,
                    Encoding.Default,
                    false,
                    // BufferSizes.StreamReader
                    1024,
                    true
                );

                // Compare with CLiWrap.PipeTarget.ToStringBuilder() that reads character by character

                while (await reader.ReadLineAsync(cancellationToken) is { } line)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    _ = stringBuilder.AppendLine(line);
                }
            }
        );

    public static PipeTarget ToStringSummary(StringBuilder stringBuilder) =>
        PipeTarget.Create(
            async (stream, cancellationToken) =>
            {
                using StreamReader streamReader = new(
                    stream,
                    Encoding.Default,
                    false,
                    // BufferSizes.StreamReader
                    1024,
                    true
                );

                List<string> stringList = [];
                int startLinesRead = 0;
                int stopLinesRead = 0;
                while (await streamReader.ReadLineAsync(cancellationToken) is { } line)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (startLinesRead < StartLines)
                    {
                        stringList.Add(line);
                        startLinesRead++;
                        continue;
                    }

                    if (stopLinesRead < StopLines)
                    {
                        stringList.Add(line);
                        stopLinesRead++;
                        continue;
                    }

                    stringList.RemoveAt(StartLines);
                    stringList.Add(line);
                }
                stringList.ForEach(item => stringBuilder.AppendLine(item));
            }
        );

    public static string Summarize(string text)
    {
        // Use same logic as in ToStringSummary()
        StringBuilder stringBuilder = new();
        using StringReader stringReader = new(text);
        List<string> stringList = [];
        int startLinesRead = 0;
        int stopLinesRead = 0;
        while (stringReader.ReadLine() is { } line)
        {
            if (startLinesRead < StartLines)
            {
                stringList.Add(line);
                startLinesRead++;
                continue;
            }

            if (stopLinesRead < StopLines)
            {
                stringList.Add(line);
                stopLinesRead++;
                continue;
            }

            stringList.RemoveAt(StartLines);
            stringList.Add(line);
        }
        stringList.ForEach(item => stringBuilder.AppendLine(item));
        return stringBuilder.ToString();
    }

    // Default to 2 start lines and 8 end lines
    private const int StartLines = 2;
    private const int StopLines = 8;
}
