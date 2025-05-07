using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using InsaneGenius.Utilities;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace PlexCleaner;

public static class Program
{
    private enum ExitCode
    {
        Success = 0,
        Error = 1,
    }

    private static int MakeExitCode(ExitCode exitCode) => (int)exitCode;

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

    private static int Main(string[] args)
    {
        // Wait for debugger to attach
        if (args.Any(arg => arg == "--debug"))
        {
            WaitForDebugger();
            // Continue
        }

        // TODO: How to get access to commandline arguments in ParseResult before calling Invoke()?
        RootCommand rootCommand = CommandLineOptions.CreateRootCommand();
        ParseResult parseResult = rootCommand.Parse(args);
        if (parseResult.Errors.Count > 0)
        {
            // TODO Parse does not handle --help and --version
            // https://github.com/dotnet/command-line-api/discussions/2553
            // Exit with default error handling
            return rootCommand.Invoke(args);
        }

        // Create default logger, will be replaced after commandline is parsed
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(
                theme: AnsiConsoleTheme.Code,
                formatProvider: CultureInfo.InvariantCulture
            )
            .CreateLogger();

        Console.CancelKeyPress += CancelEventHandler;

        // Only register keyboard handler if input is not redirected
        Task consoleKeyTask = null;
        if (!Console.IsInputRedirected)
        {
            consoleKeyTask = Task.Run(KeyPressHandler);
        }

        // Create a timer to keep the system from going to sleep
        KeepAwake.PreventSleep();
        using System.Timers.Timer keepAwakeTimer = new(30 * 1000);
        keepAwakeTimer.Elapsed += KeepAwake.OnTimedEvent;
        keepAwakeTimer.AutoReset = true;
        keepAwakeTimer.Start();

        // Invoke commands, commandline is parsed and passed to command handlers
        int exitCode = parseResult.Invoke();

        Cancel();
        consoleKeyTask?.Wait();
        Console.CancelKeyPress -= CancelEventHandler;

        keepAwakeTimer.Stop();
        KeepAwake.AllowSleep();

        // Override log level and always log exit information
        Log.Logger.LogOverrideContext().Information("Exit Code : {ExitCode}", exitCode);

        Log.CloseAndFlush();

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

    private static void ShowVersionInformation() =>
        Console.WriteLine(AssemblyVersion.GetAppVersion());

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

    public static int WriteDefaultSettingsCommand(CommandLineOptions options)
    {
        Log.Information("Writing default settings to {SettingsFile}", options.SettingsFile);

        // Save default config
        ConfigFileJsonSchema.WriteDefaultsToFile(options.SettingsFile);

        return MakeExitCode(ExitCode.Success);
    }

    public static int CreateJsonSchemaCommand(CommandLineOptions options)
    {
        Log.Information("Writing settings JSON schema to {SchemaFile}", options.SchemaFile);

        // Write schema
        ConfigFileJsonSchema.WriteSchemaToFile(options.SchemaFile);

        return MakeExitCode(ExitCode.Success);
    }

    public static int CheckForNewToolsCommand(CommandLineOptions options)
    {
        // Create but do not verify tools
        if (!Create(options, false))
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Update tools
        // Make sure that the tools exist
        return MakeExitCode(Tools.CheckForNewTools() && Tools.VerifyTools());
    }

    public static int ProcessCommand(CommandLineOptions options)
    {
        // Create
        if (!Create(options, true))
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Get file and directory list
        if (
            !ProcessDriver.GetFiles(
                options.MediaFiles,
                out List<string> directoryList,
                out List<string> fileList
            )
        )
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Process files
        if (!Process.ProcessFiles(fileList))
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Delete empty folders
        if (!Process.DeleteEmptyFolders(directoryList))
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Done
        return MakeExitCode(ExitCode.Success);
    }

    public static int MonitorCommand(CommandLineOptions options)
    {
        // Create
        if (!Create(options, true))
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Monitor and process changes in directories
        Monitor monitor = new();
        if (!monitor.MonitorFolders(options.MediaFiles))
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Done
        return MakeExitCode(ExitCode.Success);
    }

    private static int ProcessFileList(
        CommandLineOptions options,
        Func<List<string>, bool> taskFunc
    )
    {
        // Create
        if (!Create(options, true))
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Get file and directory list
        if (
            !ProcessDriver.GetFiles(
                options.MediaFiles,
                out List<string> _,
                out List<string> fileList
            )
        )
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Call task function with file list
        if (!taskFunc(fileList))
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Done
        return MakeExitCode(ExitCode.Success);
    }

    private static int ProcessFiles(
        CommandLineOptions options,
        bool mkvOnly,
        string taskName,
        Func<string, bool> taskFunc
    )
    {
        // Create
        if (!Create(options, true))
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Get file and directory list
        if (
            !ProcessDriver.GetFiles(
                options.MediaFiles,
                out List<string> _,
                out List<string> fileList
            )
        )
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Call task function for all files in list
        if (!ProcessDriver.ProcessFiles(fileList, taskName, mkvOnly, taskFunc))
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Done
        return MakeExitCode(ExitCode.Success);
    }

    public static int ReMuxCommand(CommandLineOptions options) =>
        ProcessFiles(options, false, nameof(MkvProcess.ReMux), MkvProcess.ReMux);

    public static int ReEncodeCommand(CommandLineOptions options) =>
        ProcessFiles(options, true, nameof(MkvProcess.ReEncode), MkvProcess.ReEncode);

    public static int DeInterlaceCommand(CommandLineOptions options) =>
        ProcessFiles(options, true, nameof(MkvProcess.DeInterlace), MkvProcess.DeInterlace);

    public static int VerifyCommand(CommandLineOptions options) =>
        ProcessFiles(options, true, nameof(MkvProcess.Verify), MkvProcess.Verify);

    public static int CreateSidecarCommand(CommandLineOptions options) =>
        ProcessFiles(options, true, nameof(SidecarFile.Create), SidecarFile.Create);

    public static int GetSidecarInfoCommand(CommandLineOptions options) =>
        ProcessFiles(options, true, nameof(SidecarFile.GetInformation), SidecarFile.GetInformation);

    public static int UpdateSidecarCommand(CommandLineOptions options) =>
        ProcessFiles(options, true, nameof(SidecarFile.Update), SidecarFile.Update);

    public static int GetTagMapCommand(CommandLineOptions options) =>
        ProcessFileList(options, ProcessDriver.GetTagMap);

    public static int GetMediaInfoCommand(CommandLineOptions options) =>
        ProcessFileList(options, ProcessDriver.GetMediaInfo);

    public static int GetToolInfoCommand(CommandLineOptions options) =>
        ProcessFileList(options, ProcessDriver.GetToolInfo);

    public static int RemoveSubtitlesCommand(CommandLineOptions options) =>
        ProcessFiles(options, true, nameof(MkvProcess.RemoveSubtitles), MkvProcess.RemoveSubtitles);

    public static int RemoveClosedCaptionsCommand(CommandLineOptions options) =>
        ProcessFiles(
            options,
            true,
            nameof(MkvProcess.RemoveClosedCaptions),
            MkvProcess.RemoveClosedCaptions
        );

    public static int GetVersionInfoCommand(CommandLineOptions options)
    {
        // Creating the program object will report all version information
        if (!Create(options, false))
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Verify tools to get tool version information
        if (!Tools.VerifyTools())
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Done
        return MakeExitCode(ExitCode.Success);
    }

    private static bool Create(CommandLineOptions options, bool verifyTools)
    {
        // Set the static commandline options
        Options = options;

        // Clear log file before creating the logger
        if (!string.IsNullOrEmpty(Options.LogFile) && !Options.LogAppend)
        {
            File.Delete(Options.LogFile);
        }

        // Create the logger
        CreateLogger();

        // Load config settings from JSON
        Log.Information("Loading settings from : {SettingsFile}", options.SettingsFile);
        if (!File.Exists(options.SettingsFile))
        {
            Log.Error("Settings file not found : {SettingsFile}", options.SettingsFile);
            return false;
        }
        ConfigFileJsonSchema config = ConfigFileJsonSchema.FromFile(options.SettingsFile);
        if (config == null)
        {
            Log.Error("Failed to load settings : {FileName}", options.SettingsFile);
            return false;
        }

        // Compare the schema version
        if (config.SchemaVersion != ConfigFileJsonSchema.Version)
        {
            Log.Warning(
                "Loaded old settings schema version : {LoadedVersion} != {CurrentVersion}, {FileName}",
                config.SchemaVersion,
                ConfigFileJsonSchema.Version,
                options.SettingsFile
            );

            // Upgrade the file schema
            Log.Information("Writing upgraded settings file : {FileName}", options.SettingsFile);
            ConfigFileJsonSchema.ToFile(options.SettingsFile, config);
        }

        // Verify the settings
        if (!config.VerifyValues())
        {
            Log.Error(
                "Settings file contains incorrect or missing values : {FileName}",
                options.SettingsFile
            );
            return false;
        }

        // Set the static settings
        Config = config;

        // Set the FileEx options
        FileEx.Options.RetryCount = Config.MonitorOptions.FileRetryCount;
        FileEx.Options.RetryWaitTime = Config.MonitorOptions.FileRetryWaitTime;

        // Set the FileEx Cancel object
        FileEx.Options.Cancel = s_cancelSource.Token;

        // Override log level and always log startup information
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
        Log.Information(
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

        // There is a race condition between tools exiting on Ctrl-C and reporting an error, and our app's Ctrl-C handler being called
        // In case of a suspected Ctrl-C, yield some time for this handler to be called before testing the state
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

    // Commandline options
    public static CommandLineOptions Options { get; set; }

    // Config file options
    public static ConfigFileJsonSchema Config { get; set; }

    // Snippet runtime
    public static readonly TimeSpan SnippetTimeSpan = TimeSpan.FromSeconds(30);

    // QuickScan runtime
    public static readonly TimeSpan QuickScanTimeSpan = TimeSpan.FromMinutes(3);

    // Cancellation token
    private static readonly CancellationTokenSource s_cancelSource = new();
}
