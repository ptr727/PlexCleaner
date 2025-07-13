using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DotMake.CommandLine;
using InsaneGenius.Utilities;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using Timer = System.Timers.Timer;

namespace PlexCleaner;

// TODO: Specialize all catch(Exception) to catch specific expected exceptions only
// TODO: Replace async Task.Run() wrappers with native async/await methods

public static class Program
{
    public static readonly TimeSpan SnippetTimeSpan = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan QuickScanTimeSpan = TimeSpan.FromMinutes(3);
    private static readonly CancellationTokenSource s_cancelSource = new();
    private static HttpClient s_httpClient;

    // Commandline options
    public static CommandLineOptions Options { get; set; }

    // Config file options
    public static ConfigFileJsonSchema Config { get; set; }

    public static HttpClient GetHttpClient()
    {
        if (s_httpClient != null)
        {
            return s_httpClient;
        }
        s_httpClient = new() { Timeout = TimeSpan.FromSeconds(120) };
        s_httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue(
                Assembly.GetExecutingAssembly().GetName().Name,
                Assembly.GetExecutingAssembly().GetName().Version.ToString()
            )
        );
        return s_httpClient;
    }

    private static Task<int> MakeExitResult(ExitCode exitCode) =>
        Task.FromResult(MakeExitCode(exitCode));

    private static int MakeExitCode(ExitCode exitCode) => (int)exitCode;

    private static Task<int> MakeExitResult(bool success) => Task.FromResult(MakeExitCode(success));

    private static int MakeExitCode(bool success) =>
        success ? (int)ExitCode.Success : (int)ExitCode.Error;

    public static void LogInterruptMessage()
    {
        // Keyboard handler is only active if input is not redirected
        if (!Console.IsInputRedirected)
        {
            Console.WriteLine("Press Ctrl+C or Ctrl+Z or Ctrl+Q to exit.");
        }
    }

    private static async Task<int> Main(string[] args)
    {
        // Wait for debugger to attach
        // Evaluate args directly to avoid calling any dependencies before the debugger is attached
        if (args.Any(arg => arg == "--debug"))
        {
            WaitForDebugger();
            // Continue
        }

        // Parse commandline options
        ParseResult parseResult = Cli.Parse<CliRootCommand>(args);
        if (parseResult.Errors.Count > 0)
        {
            // Exit with default error handling
            return await parseResult.InvokeAsync();
        }

        // Bind parsed commandline options
        CliRootCommand rootCommand = parseResult.Bind<CliRootCommand>();
        Options = rootCommand.Options;

        // Create logger
        CreateLogger();

        // Console handlers
        Console.CancelKeyPress += CancelEventHandler;
        Task consoleKeyTask = null;
        if (!Console.IsInputRedirected)
        {
            consoleKeyTask = Task.Run(KeyPressHandler);
        }

        // Keep system awake
        KeepAwake.PreventSleep();
        using Timer keepAwakeTimer = new(30 * 1000);
        keepAwakeTimer.Elapsed += KeepAwake.OnTimedEvent;
        keepAwakeTimer.AutoReset = true;
        keepAwakeTimer.Start();

        // Call command handler
        int exitCode = await parseResult.InvokeAsync();

        // Cleanup
        Cancel();
        consoleKeyTask?.Wait();
        Console.CancelKeyPress -= CancelEventHandler;
        keepAwakeTimer.Stop();
        KeepAwake.AllowSleep();

        Log.Logger.LogOverrideContext().Information("Exit Code : {ExitCode}", exitCode);
        await Log.CloseAndFlushAsync();

        return exitCode;
    }

    public static void VerifyLatestVersion()
    {
        if (!Config.ToolsOptions.AutoUpdate)
        {
            // Skip
            return;
        }

        try
        {
            // Get the latest release version from github releases
            // E.g. 1.2.3
            const string repo = "ptr727/PlexCleaner";
            Version latestVersion = new(GitHubRelease.GetLatestRelease(repo));

            // Get this version
            Version thisVersion = new(AssemblyVersion.GetReleaseVersion());

            // Compare the versions
            if (thisVersion.CompareTo(latestVersion) < 0)
            {
                Log.Warning(
                    "Current version is older than latest version : {CurrentVersion} < {LatestVersion}",
                    thisVersion.ToString(),
                    latestVersion.ToString()
                );
            }
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            // Nothing to do
        }
    }

    private static void KeyPressHandler()
    {
        for (; ; )
        {
            // Wait on key available or cancelled
            while (!Console.KeyAvailable)
            {
                if (WaitForCancel(100))
                {
                    // Done
                    return;
                }
            }

            // Read key and hide from console display
            ConsoleKeyInfo keyInfo = Console.ReadKey(true);

            // Break on Ctrl+Q or Ctrl+Z, Ctrl+C and Ctrl+Break is handled in cancel handler
            if (
                keyInfo.Key is ConsoleKey.Q or ConsoleKey.Z
                && keyInfo.Modifiers == ConsoleModifiers.Control
            )
            {
                // Signal the cancel event
                Cancel(ConsoleModifiers.Control, keyInfo.Key);

                // Done
                return;
            }
        }
    }

    private static void CancelEventHandler(object sender, ConsoleCancelEventArgs eventArgs)
    {
        Log.Warning("Cancel event triggered : {EventType}", eventArgs.SpecialKey);

        // Keep running and do graceful exit
        eventArgs.Cancel = true;

        // Signal the cancel event, use Ctrl+Break as signal
        Cancel(ConsoleModifiers.Control, ConsoleKey.Pause);
    }

    private static void WaitForDebugger()
    {
        // Do not use any dependencies as this code gets called very early in launch

        // Wait for a debugger to be attached
        Console.WriteLine("Waiting for debugger to attach...");
        while (!Debugger.IsAttached)
        {
            // Wait a bit and try again
            Thread.Sleep(100);
        }
        Console.WriteLine("Debugger attached.");

        // Break into the debugger
        Debugger.Break();
    }

    private static void CreateLogger()
    {
        // Commandline must have been parsed and assigned to Options
        Debug.Assert(Options != null);

        // Clear log file before creating the logger
        if (!string.IsNullOrEmpty(Options.LogFile) && !Options.LogAppend)
        {
            File.Delete(Options.LogFile);
        }

        // Enable Serilog debug output to the console
        SelfLog.Enable(Console.Error);

        // Logger configuration
        LoggerConfiguration loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Is(Options.LogWarning ? LogEventLevel.Warning : LogEventLevel.Information)
            // Set minimum to Verbose for LogOverride context
            .MinimumLevel.Override(typeof(Extensions.LogOverride).FullName, LogEventLevel.Verbose)
            .Enrich.WithThreadId()
            .WriteTo.Console(
                theme: AnsiConsoleTheme.Code,
                // Remove lj from default to quote strings
                outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] <{ThreadId}> {Message}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture
            );

        // Log to file
        if (!string.IsNullOrEmpty(Options.LogFile))
        {
            _ = loggerConfiguration.WriteTo.Async(action =>
                action.File(
                    Options.LogFile,
                    rollOnFileSizeLimit: true,
                    // Remove lj from default to quote strings
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] <{ThreadId}> {Message}{NewLine}{Exception}",
                    formatProvider: CultureInfo.InvariantCulture
                )
            );
        }

        // Create static Serilog logger
        Log.Logger = loggerConfiguration.CreateLogger();

        // Set library logger to Serilog logger
        LogOptions.Logger = Log.Logger;
    }

    public static async Task<int> DefaultSettingsCommandAsync()
    {
        // Save default config
        Log.Information("Writing default settings to {SettingsFile}", Options.SettingsFile);
        await ConfigFileJsonSchema.WriteDefaultsToFileAsync(Options.SettingsFile);

        return MakeExitCode(ExitCode.Success);
    }

    public static async Task<int> CreateSchemaCommandAsync()
    {
        // Write schema to file
        Log.Information("Writing settings JSON schema to {SchemaFile}", Options.SchemaFile);
        await ConfigFileJsonSchema.WriteSchemaToFileAsync(Options.SchemaFile);

        return MakeExitCode(ExitCode.Success);
    }

    public static async Task<int> CheckForNewToolsCommandAsync()
    {
        ExitCode exitCode = await Task.Run(async () =>
            !await CreateAsync(false) || !Tools.CheckForNewTools() || !Tools.VerifyTools()
                ? ExitCode.Error
                : ExitCode.Success
        );
        return MakeExitCode(exitCode);
    }

    public static async Task<int> ProcessCommandAsync()
    {
        ExitCode exitCode = await Task.Run(async () =>
            !await CreateAsync(true)
            || !ProcessDriver.GetFiles(
                Options.MediaFiles,
                out List<string> directoryList,
                out List<string> fileList
            )
            || !await Process.ProcessFilesAsync(fileList)
            || !Process.DeleteEmptyFolders(directoryList)
                ? ExitCode.Error
                : ExitCode.Success
        );
        return MakeExitCode(exitCode);
    }

    public static async Task<int> MonitorCommandAsync()
    {
        ExitCode exitCode = await Task.Run(async () =>
        {
            // Create
            // Monitor folders
            Monitor monitor = new();
            return
                !await CreateAsync(true) || !await monitor.MonitorFoldersAsync(Options.MediaFiles)
                ? ExitCode.Error
                : ExitCode.Success;
        });
        return MakeExitCode(exitCode);
    }

    private static async Task<int> ProcessFileListAsync(Func<List<string>, bool> taskFunc)
    {
        ExitCode exitCode = await Task.Run(async () =>
            !await CreateAsync(true)
            || !ProcessDriver.GetFiles(
                Options.MediaFiles,
                out List<string> _,
                out List<string> fileList
            )
            || !taskFunc(fileList)
                ? ExitCode.Error
                : ExitCode.Success
        );
        return MakeExitCode(exitCode);
    }

    private static async Task<int> ProcessFilesAsync(
        bool mkvOnly,
        string taskName,
        Func<string, bool> taskFunc
    )
    {
        ExitCode exitCode = await Task.Run(async () =>
            !await CreateAsync(true)
            || !ProcessDriver.GetFiles(
                Options.MediaFiles,
                out List<string> _,
                out List<string> fileList
            )
            || !ProcessDriver.ProcessFiles(fileList, taskName, mkvOnly, taskFunc)
                ? ExitCode.Error
                : ExitCode.Success
        );
        return MakeExitCode(exitCode);
    }

    public static async Task<int> ReMuxCommandAsync() =>
        await ProcessFilesAsync(false, nameof(MkvProcess.ReMux), MkvProcess.ReMux);

    public static async Task<int> ReEncodeCommandAsync() =>
        await ProcessFilesAsync(true, nameof(MkvProcess.ReEncode), MkvProcess.ReEncode);

    public static async Task<int> DeInterlaceCommandAsync() =>
        await ProcessFilesAsync(true, nameof(MkvProcess.DeInterlace), MkvProcess.DeInterlace);

    public static async Task<int> VerifyCommandAsync() =>
        await ProcessFilesAsync(true, nameof(MkvProcess.Verify), MkvProcess.Verify);

    public static async Task<int> CreateSidecarCommandAsync() =>
        await ProcessFilesAsync(true, nameof(SidecarFile.Create), SidecarFile.Create);

    public static async Task<int> GetSidecarCommandAsync() =>
        await ProcessFilesAsync(
            true,
            nameof(SidecarFile.GetInformation),
            SidecarFile.GetInformation
        );

    public static async Task<int> UpdateSidecarCommandAsync() =>
        await ProcessFilesAsync(true, nameof(SidecarFile.Update), SidecarFile.Update);

    public static async Task<int> RemoveSubtitlesCommandAsync() =>
        await ProcessFilesAsync(
            true,
            nameof(MkvProcess.RemoveSubtitles),
            MkvProcess.RemoveSubtitles
        );

    public static async Task<int> GetTagMapCommandAsync() =>
        await ProcessFileListAsync(ProcessDriver.GetTagMap);

    public static async Task<int> GetMediaInfoCommandAsync() =>
        await ProcessFileListAsync(ProcessDriver.GetMediaInfo);

    public static async Task<int> TestMediaInfoCommandAsync() =>
        await ProcessFileListAsync(ProcessDriver.TestMediaInfo);

    public static async Task<int> GetToolInfoCommandAsync() =>
        await ProcessFileListAsync(ProcessDriver.GetToolInfo);

    public static async Task<int> RemoveClosedCaptionsCommandAsync() =>
        await ProcessFilesAsync(
            true,
            nameof(MkvProcess.RemoveClosedCaptions),
            MkvProcess.RemoveClosedCaptions
        );

    public static async Task<int> GetVersionInfoCommandAsync()
    {
        ExitCode exitCode = await Task.Run(async () =>
            !await CreateAsync(true) || !Tools.VerifyTools() ? ExitCode.Error : ExitCode.Success
        );
        return MakeExitCode(exitCode);
    }

    private static async Task<bool> CreateAsync(bool verifyTools)
    {
        // Load config settings from JSON
        Log.Information("Loading settings from : {SettingsFile}", Options.SettingsFile);
        if (!File.Exists(Options.SettingsFile))
        {
            Log.Error("Settings file not found : {SettingsFile}", Options.SettingsFile);
            return false;
        }
        ConfigFileJsonSchema config = await ConfigFileJsonSchema.FromFileAsync(
            Options.SettingsFile
        );
        if (config == null)
        {
            Log.Error("Failed to load settings : {FileName}", Options.SettingsFile);
            return false;
        }

        // Compare the schema version
        if (config.SchemaVersion != ConfigFileJsonSchema.Version)
        {
            Log.Warning(
                "Loaded old settings schema version : {LoadedVersion} != {CurrentVersion}, {FileName}",
                config.SchemaVersion,
                ConfigFileJsonSchema.Version,
                Options.SettingsFile
            );

            // Upgrade the file schema
            Log.Information("Writing upgraded settings file : {FileName}", Options.SettingsFile);
            await ConfigFileJsonSchema.ToFileAsync(Options.SettingsFile, config);
        }

        // Verify the settings
        if (!config.VerifyValues())
        {
            Log.Error(
                "Settings file contains incorrect or missing values : {FileName}",
                Options.SettingsFile
            );
            return false;
        }

        // Set the static config
        Config = config;

        // Log runtime information
        Log.Logger.LogOverrideContext()
            .Information("Commandline : {Commandline}", Environment.CommandLine);
        Log.Logger.LogOverrideContext()
            .Information("Application Version : {AppVersion}", AssemblyVersion.GetAppVersion());
        Log.Logger.LogOverrideContext()
            .Information(
                "Runtime Version : {RuntimeVersions}",
                AssemblyVersion.GetRuntimeVersion()
            );
        Log.Logger.LogOverrideContext()
            .Information("OS Version : {OsDescription}", RuntimeInformation.OSDescription);
        Log.Logger.LogOverrideContext()
            .Information("Build Date : {BuildDate}", AssemblyVersion.GetBuildDate().ToLocalTime());

        // Warn if a newer version has been released
        VerifyLatestVersion();

        // Configure thread count for parallel processing
        Options.ThreadCount = Options.Parallel
            ? Options.ThreadCount == 0
                ? Math.Clamp(Environment.ProcessorCount / 2, 1, 4)
                : Math.Clamp(Options.ThreadCount, 1, Environment.ProcessorCount)
            : 1;
        Log.Logger.LogOverrideContext()
            .Information(
                "Parallel Processing: {Parallel} : Thread Count: {ThreadCount}, Processor Count: {ProcessorCount}",
                Options.Parallel,
                Options.ThreadCount,
                Environment.ProcessorCount
            );

        // Verify tools
        if (verifyTools)
        {
            // Upgrade tools if auto update is enabled
            if (Config.ToolsOptions.AutoUpdate && !Tools.CheckForNewTools())
            {
                // Ignore error, do not stop execution in case of e.g. a site being down
                Log.Error(
                    "Checking for new tools failed but continuing with existing tool versions"
                );
            }

            // Verify tools
            if (!Tools.VerifyTools())
            {
                // Error
                return false;
            }
        }

        // Done
        return true;
    }

    public static bool IsCancelledError()
    {
        // Test immediately
        if (s_cancelSource.IsCancellationRequested)
        {
            return true;
        }

        // In case of possible Ctrl-C and tool exit, yield some time for this handler to be called before testing the state
        return WaitForCancel(100);
    }

    public static bool WaitForCancel(int millisecond) =>
        s_cancelSource.Token.WaitHandle.WaitOne(millisecond);

    public static bool IsCancelled() => s_cancelSource.IsCancellationRequested;

    public static void Cancel() =>
        // Signal cancel
        s_cancelSource.Cancel();

    public static void Cancel(ConsoleModifiers modifiers, ConsoleKey key)
    {
        Log.Warning("Operation interrupted : {Modifiers}+{Key}", modifiers, key);
        Cancel();
    }

    public static CancellationToken CancelToken() => s_cancelSource.Token;

    private enum ExitCode
    {
        Success = 0,
        Error = 1,
    }
}
