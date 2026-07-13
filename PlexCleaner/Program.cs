using System.Diagnostics;
using System.Runtime.InteropServices;
using ptr727.Utilities;
using Serilog;

namespace PlexCleaner;

public static class Program
{
    // Never disposed, so signal handlers can safely call Cancel() for the process lifetime
    private static readonly CancellationTokenSource s_cancelSource = new();

    // Exit code for an OS-signal interruption (128 + signal number), else 0; set only by PosixSignalHandler
    private static volatile int s_signalExitCode;

    // Serilog to Microsoft.Extensions.Logging bridge shared with library loggers
    private static Microsoft.Extensions.Logging.ILoggerFactory? s_libraryLoggerFactory;

    public static readonly TimeSpan SnippetTimeSpan = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan QuickScanTimeSpan = TimeSpan.FromMinutes(3);
    public const int QuickScanFrameCount = 1000;

    public static CommandLineOptions Options { get; set; } = null!;
    public static ConfigFileJsonSchema Config { get; set; } = null!;

    private static int MakeExitCode(ExitCode exitCode) => (int)exitCode;

    private static int MakeExitCode(bool success) =>
        success ? (int)ExitCode.Success : (int)ExitCode.Error;

    private static int Main(string[] args)
    {
        // Wait for debugger to attach
        if (args.Any(arg => arg == "--debug"))
        {
            WaitForDebugger();
            // Continue
        }

        // Parse commandline options
        CommandLineParser commandLineParser = new(args);
        if (commandLineParser.BypassStartup)
        {
            return commandLineParser.Result.Invoke();
        }

        // Bind all commandline options
        Options = commandLineParser.Bind();

        // Create the logger from the bound options
        Log.Logger = LoggerFactory.Create(LoggerFactory.FromCommandLine(Options));

        // Propagate the logger to the libraries so their output shares the same sinks; both take a
        // Microsoft.Extensions.Logging factory bridged from the Serilog logger
        s_libraryLoggerFactory = LoggerFactory.CreateLoggerFactory(Log.Logger);
        LogOptions.LoggerFactory = s_libraryLoggerFactory;
        ptr727.LanguageTags.LogOptions.SetFactory(s_libraryLoggerFactory);

        // Warn about deprecated options, single-threaded here before any command is invoked so the
        // warning is emitted once; routed through the override context so it shows at any log level
        if (Options.LogWarning)
        {
            Log.Logger.LogOverrideContext()
                .Warning(
                    "--logwarning is deprecated and will be removed; use --loglevel Warning (add --logelevate to restore per-file elevation)"
                );
        }
        if (Options.LogAppend)
        {
            Log.Logger.LogOverrideContext()
                .Warning(
                    "--logappend is deprecated and will be removed; appending is now the default, use --logclear to clear the log file"
                );
        }

        // Handle termination signals to cancel gracefully and still log the summary before exit
        PosixSignalRegistration sigIntRegistration = PosixSignalRegistration.Create(
            PosixSignal.SIGINT,
            PosixSignalHandler
        );
        PosixSignalRegistration sigTermRegistration = PosixSignalRegistration.Create(
            PosixSignal.SIGTERM,
            PosixSignalHandler
        );
        PosixSignalRegistration sigQuitRegistration = PosixSignalRegistration.Create(
            PosixSignal.SIGQUIT,
            PosixSignalHandler
        );

        // Keep the system from going to sleep
        KeepAwake.PreventSleep();
        using System.Timers.Timer keepAwakeTimer = new(30 * 1000);
        keepAwakeTimer.Elapsed += KeepAwake.OnTimedEvent;
        keepAwakeTimer.AutoReset = true;
        keepAwakeTimer.Start();

        // Invoke command
        int exitCode = commandLineParser.Result.Invoke();

        // A signal interruption reports its own exit code so a caller can tell it from a clean finish or an error
        if (s_signalExitCode != 0)
        {
            exitCode = s_signalExitCode;
        }

        // Cleanup
        Cancel();
        // Unhook signals before flushing so a second signal reverts to default OS termination
        sigIntRegistration.Dispose();
        sigTermRegistration.Dispose();
        sigQuitRegistration.Dispose();
        keepAwakeTimer.Stop();
        KeepAwake.AllowSleep();

        Log.Logger.LogOverrideContext().Information("Exit Code : {ExitCode}", exitCode);
        Log.CloseAndFlush();
        s_libraryLoggerFactory?.Dispose();

        return exitCode;
    }

    public static void VerifyLatestVersion()
    {
        if (!Config.ToolsOptions.AutoUpdate)
        {
            // Skip
            return;
        }

        // Get the latest release version from github releases, skip on download failure (already logged)
        // E.g. 1.2.3
        const string repo = "ptr727/PlexCleaner";
        if (!GitHubRelease.GetLatestRelease(repo, out string latest))
        {
            return;
        }

        try
        {
            Version latestVersion = new(latest);

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
        catch (Exception e) when (Log.Logger.LogAndHandle(e))
        {
            // Nothing to do
        }
    }

    private static void PosixSignalHandler(PosixSignalContext context)
    {
        Log.Warning("Operation interrupted : {Signal}", context.Signal);

        // Report 128 + signal number (PosixSignal enum values are abstract, so map explicitly); any other signal defaults to 128
        s_signalExitCode =
            context.Signal == PosixSignal.SIGINT ? 130
            : context.Signal == PosixSignal.SIGQUIT ? 131
            : context.Signal == PosixSignal.SIGTERM ? 143
            : 128;

        // Keep running and do a graceful exit so the summary and exit code are logged
        context.Cancel = true;
        Cancel();
    }

    private static void WaitForDebugger()
    {
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

    public static int DefaultSettingsCommand()
    {
        // Save default config
        Debug.Assert(!string.IsNullOrEmpty(Options.SettingsFile));
        Log.Information("Writing default settings to {SettingsFile}", Options.SettingsFile);
        ConfigFileJsonSchema.WriteDefaultsToFile(Options.SettingsFile);

        return MakeExitCode(ExitCode.Success);
    }

    public static int CreateSchemaCommand()
    {
        // Write schema
        Debug.Assert(!string.IsNullOrEmpty(Options.SchemaFile));
        Log.Information("Writing settings JSON schema to {SchemaFile}", Options.SchemaFile);
        ConfigFileJsonSchema.WriteSchemaToFile(Options.SchemaFile);

        return MakeExitCode(ExitCode.Success);
    }

    public static int CheckForNewToolsCommand()
    {
        // Create but do not verify tools
        if (!Create(false))
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Update tools
        // Make sure that the tools exist
        return MakeExitCode(Tools.CheckForNewTools() && Tools.VerifyTools());
    }

    public static int ProcessCommand()
    {
        // Create
        if (!Create(true))
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Get file and directory list
        if (
            !ProcessDriver.GetFiles(
                Options.MediaFiles,
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

#if PLUGINS
    public static int CustomCommand()
    {
        // Create
        if (!Create(true))
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Load the plugin after config and tools are initialized (the option is required, so the
        // path is always present here)
        if (Options.PluginAssembly == null)
        {
            Log.Error("Plugin assembly path is required");
            return MakeExitCode(ExitCode.Error);
        }
        IProcessPlugin? plugin = PluginLoader.Load(Options.PluginAssembly);
        if (plugin == null)
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Get file and directory list
        if (
            !ProcessDriver.GetFiles(
                Options.MediaFiles,
                out List<string> _,
                out List<string> fileList
            )
        )
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Process each file using the plugin, isolating plugin exceptions to the current file so a
        // faulty plugin fails that file instead of aborting the entire run
        bool ProcessFile(string fileName)
        {
            try
            {
                return plugin.ProcessFile(fileName);
            }
            catch (Exception e) when (Log.Logger.LogAndHandle(e))
            {
                return false;
            }
        }

        if (!ProcessDriver.ProcessFiles(fileList, plugin.Name, false, ProcessFile))
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Done
        return MakeExitCode(ExitCode.Success);
    }
#else
    public static int CustomCommand()
    {
        Log.Error("Custom plugin support requires a non-AOT build");
        return MakeExitCode(ExitCode.Error);
    }
#endif

    public static int MonitorCommand()
    {
        // Create
        if (!Create(true))
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Monitor and process changes in directories
        Monitor monitor = new();
        if (!monitor.MonitorFolders(Options.MediaFiles))
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Done
        return MakeExitCode(ExitCode.Success);
    }

    private static int ProcessFileList(Func<List<string>, bool> taskFunc)
    {
        // Create
        if (!Create(true))
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Get file and directory list
        if (
            !ProcessDriver.GetFiles(
                Options.MediaFiles,
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

    private static int ProcessFiles(bool mkvOnly, string taskName, Func<string, bool> taskFunc)
    {
        // Create
        if (!Create(true))
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Get file and directory list
        if (
            !ProcessDriver.GetFiles(
                Options.MediaFiles,
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

    public static int ReMuxCommand() =>
        ProcessFiles(false, nameof(MkvProcess.ReMux), MkvProcess.ReMux);

    public static int ReEncodeCommand() =>
        ProcessFiles(true, nameof(MkvProcess.ReEncode), MkvProcess.ReEncode);

    public static int DeInterlaceCommand() =>
        ProcessFiles(true, nameof(MkvProcess.DeInterlace), MkvProcess.DeInterlace);

    public static int VerifyCommand() =>
        ProcessFiles(true, nameof(MkvProcess.Verify), MkvProcess.Verify);

    public static int CreateSidecarCommand() =>
        ProcessFiles(true, nameof(SidecarFile.Create), SidecarFile.Create);

    public static int GetSidecarInfoCommand() =>
        ProcessFiles(true, nameof(SidecarFile.GetInformation), SidecarFile.GetInformation);

    public static int UpdateSidecarCommand() =>
        ProcessFiles(true, nameof(SidecarFile.Update), SidecarFile.Update);

    public static int RemoveSubtitlesCommand() =>
        ProcessFiles(true, nameof(MkvProcess.RemoveSubtitles), MkvProcess.RemoveSubtitles);

    public static int GetTagMapCommand() => ProcessFileList(ProcessDriver.GetTagMap);

    public static int GetMediaInfoCommand() => ProcessFileList(ProcessDriver.GetMediaInfo);

    public static int TestMediaInfoCommand() => ProcessFileList(ProcessDriver.TestMediaInfo);

    public static int GetToolInfoCommand() => ProcessFileList(ProcessDriver.GetToolInfo);

    public static int RemoveClosedCaptionsCommand() =>
        ProcessFiles(
            true,
            nameof(MkvProcess.RemoveClosedCaptions),
            MkvProcess.RemoveClosedCaptions
        );

    public static int GetVersionInfoCommand()
    {
        // Creating the program object will report all version information
        if (!Create(false))
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

    private static bool LoadSettings()
    {
        // Load config settings from JSON
        Log.Information("Loading settings from : {SettingsFile}", Options.SettingsFile);
        if (!File.Exists(Options.SettingsFile))
        {
            Log.Error("Settings file not found : {SettingsFile}", Options.SettingsFile);
            return false;
        }

        try
        {
            // Load the settings file
            ConfigFileJsonSchema config = ConfigFileJsonSchema.OpenAndUpgrade(Options.SettingsFile);

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
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e))
        {
            Log.Error("Error opening settings file : {FileName}", Options.SettingsFile);
            return false;
        }
        return true;
    }

    private static bool Create(bool verifyTools)
    {
        // Load config settings from JSON
        if (!LoadSettings())
        {
            return false;
        }

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

    public static CancellationToken CancelToken() => s_cancelSource.Token;

    private enum ExitCode
    {
        Success = 0,
        Error = 1,
    }
}
