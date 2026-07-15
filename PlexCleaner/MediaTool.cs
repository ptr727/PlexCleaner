using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using CliWrap;
using CliWrap.Buffered;

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

    // Default to 2 start lines and 8 end lines
    private const int StartLines = 2;
    private const int StopLines = 8;

    // The tool info must be set during initialization
    // Version information is used in the sidecar tool logic
    public MediaToolInfo Info { get; set; } = null!;

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
    protected virtual string GetSubFolder() => string.Empty;

    // Tools can override the default behavior as needed
    public virtual bool Update(string updateFile)
    {
        // Make sure the tool folder exists and is empty
        string toolPath = GetToolFolder();
        if (Directory.Exists(toolPath))
        {
            Directory.Delete(toolPath, true);
        }
        _ = Directory.CreateDirectory(toolPath);

        // Extract the update file
        Log.Information("Extracting {UpdateFile} ...", updateFile);
        return Tools.SevenZip.UnZip(updateFile, toolPath);
    }

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

    protected bool GetLatestGitHubRelease(string repo, out string version)
    {
        Log.Debug("{Tool} : Getting latest version from GitHub : {Repo}", GetToolFamily(), repo);
        return GitHubRelease.GetLatestRelease(repo, out version);
    }

    public bool Execute(
        Command command,
        out BufferedCommandResult bufferedCommandResult,
        [CallerMemberName] string operation = ""
    ) => Execute(command, false, false, out bufferedCommandResult, operation);

    // Stream carrying tool error text; stderr by default, stdout for the mkvtoolnix tools
    protected virtual string GetErrorOutput(BufferedCommandResult result) => result.StandardError;

    protected bool LogFailedResult(
        BufferedCommandResult result,
        string fileName,
        [CallerMemberName] string operation = ""
    )
    {
        // ffmpeg can exit 0 yet report a fatal error on stderr (see FfMpegTool), so failures may carry
        // an error summary. Log the summary as its own value with the " : " separator in the template.
        // Folding the separator into a quoted string value instead puts the quote right after the exit
        // code, rendering: ExitCode: 0" : ... instead of the correct ExitCode: 0 : "...".
        // Prefer the stream the tool writes errors to (GetErrorOutput), fall back to the other captured
        // stream so the error is logged even if the tool used the unexpected stream.
        string error = GetErrorOutput(result).Trim();
        if (string.IsNullOrEmpty(error))
        {
            error = result.StandardError.Trim();
            if (string.IsNullOrEmpty(error))
            {
                error = result.StandardOutput.Trim();
            }
        }
        string summary = CleanForLog(Summarize(error));
        if (string.IsNullOrEmpty(summary))
        {
            Log.Error(
                "Failed execution of {ToolType} : {Operation:l} : ExitCode: {ExitCode} : {FileName}",
                GetToolType(),
                operation,
                result.ExitCode,
                fileName
            );
        }
        else
        {
            Log.Error(
                "Failed execution of {ToolType} : {Operation:l} : ExitCode: {ExitCode} : {Error} : {FileName}",
                GetToolType(),
                operation,
                result.ExitCode,
                summary,
                fileName
            );
        }
        return false;
    }

    // Join lines with " | " and drop other control characters so multi-line tool output stays a single structured log value; printable Unicode (e.g. media titles) is preserved
    protected static string CleanForLog(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }
        string joined = text.ReplaceLineEndings(" | ");
        StringBuilder builder = new(joined.Length);
        foreach (char character in joined)
        {
            _ = builder.Append(char.IsControl(character) ? ' ' : character);
        }
        return builder.ToString().Trim();
    }

    public bool Execute(
        Command command,
        bool stdOutSummary,
        bool stdErrSummary,
        out BufferedCommandResult bufferedCommandResult,
        [CallerMemberName] string operation = ""
    )
    {
        bufferedCommandResult = null!;
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
            Log.Debug(
                "Executing {ToolType} : {Operation:l} : ProcessId: {ProcessId}, Arguments: {Arguments}",
                GetToolType(),
                operation,
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
                "Cancelled execution of {ToolType} : {Operation:l} : ProcessId: {ProcessId}, Arguments: {Arguments}",
                GetToolType(),
                operation,
                processId,
                command.Arguments
            );
            return false;
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e))
        {
            return false;
        }
    }

    public bool ExecuteStreamStdErr(
        Command command,
        Action<string> lineAction,
        out int exitCode,
        [CallerMemberName] string operation = ""
    )
    {
        exitCode = -1;
        int processId = -1;
        try
        {
            // Stream stderr line by line to the caller instead of buffering it
            PipeTarget stdErrTarget = PipeTarget.Create(
                async (stream, cancellationToken) =>
                {
                    using StreamReader reader = new(stream, Encoding.Default, false, 1024, true);
                    while (await reader.ReadLineAsync(cancellationToken) is { } line)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }
                        lineAction(line);
                    }
                }
            );

            CommandTask<CommandResult> task = command
                .WithStandardOutputPipe(PipeTarget.Null)
                .WithStandardErrorPipe(stdErrTarget)
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync(CancellationToken.None, Program.CancelToken());
            processId = task.ProcessId;
            Log.Debug(
                "Executing {ToolType} : {Operation:l} : ProcessId: {ProcessId}, Arguments: {Arguments}",
                GetToolType(),
                operation,
                processId,
                command.Arguments
            );

            CommandResult commandResult = task.Task.GetAwaiter().GetResult();
            exitCode = commandResult.ExitCode;
            return task.Task.IsCompletedSuccessfully;
        }
        catch (OperationCanceledException)
        {
            Log.Error(
                "Cancelled execution of {ToolType} : {Operation:l} : ProcessId: {ProcessId}, Arguments: {Arguments}",
                GetToolType(),
                operation,
                processId,
                command.Arguments
            );
            return false;
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e))
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
}
