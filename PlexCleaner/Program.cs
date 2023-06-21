using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using InsaneGenius.Utilities;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace PlexCleaner;

internal class Program
{
    enum ExitCode { Success = 0, Error = 1 }

    static int MakeExitCode(ExitCode exitCode)
    {
        return (int)exitCode;
    }
    static int MakeExitCode(bool success)
    {
        return success ? (int)ExitCode.Success : (int)ExitCode.Error;
    }

    private static int Main()
    {
        // Wait for debugger to attach
        WaitForDebugger();

        // Display version information only
        if (ShowVersionInformation())
        {
            // Exit immediately to not print anything else
            return 0;
        }

        // Register cancel and keyboard handlers
        Console.CancelKeyPress += CancelEventHandler;
        var consoleKeyTask = Task.Run(KeyPressHandler);
        Console.WriteLine("Press Ctrl+C or Ctrl+Z or Ctrl+Q to exit.");

        // Create default logger
        CreateLogger(null);

        // Create a timer to keep the system from going to sleep
        KeepAwake.PreventSleep();
        using System.Timers.Timer keepAwakeTimer = new(30 * 1000);
        keepAwakeTimer.Elapsed += KeepAwake.OnTimedEvent;
        keepAwakeTimer.AutoReset = true;
        keepAwakeTimer.Start();

        // Create the commandline and execute commands
        var exitCode = CommandLineOptions.Invoke();

        // Cancel background operations
        Cancel();
        consoleKeyTask.Wait();

        // Stop the timer
        keepAwakeTimer.Stop();
        keepAwakeTimer.Dispose();
        KeepAwake.AllowSleep();

        Log.Logger.Information("Exit Code : {ExitCode}", exitCode);

        // Close and flush on process exit
        Log.CloseAndFlush();

        return exitCode;
    }

    private static bool ShowVersionInformation()
    {
        // Use the raw commandline and look for --version
        if (Environment.CommandLine.Contains("--version"))
        {
            string appVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            Console.WriteLine(appVersion);
            return true;
        }
        return false;
    }

    private static void KeyPressHandler()
    {
        // Skip if input is redirected
        if (Console.IsInputRedirected)
        {
            return;
        }

        for (;;)
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
            var keyInfo = Console.ReadKey(true);

            // Break on Ctrl+Q or Ctrl+Z, Ctrl+C handled in cancel handler
            if (keyInfo.Key is ConsoleKey.Q or ConsoleKey.Z
                && keyInfo.Modifiers == ConsoleModifiers.Control)
            {
                Log.Logger.Warning("Operation interrupted : {Modifiers}+{Key}", keyInfo.Modifiers, keyInfo.Key);

                // Signal the cancel event
                Cancel();

                // Done
                return;
            }
        }
    }

    private static void CancelEventHandler(object sender, ConsoleCancelEventArgs eventArgs)
    {
        Log.Logger.Warning("Operation interrupted : {SpecialKey}", eventArgs.SpecialKey);

        // Keep running and do graceful exit
        eventArgs.Cancel = true;

        // Signal the cancel event
        Cancel();
    }


    private static void WaitForDebugger()
    {
        // Do not use any dependencies as this code gets called very early in launch

        // Use the raw commandline and look for --debug
        if (Environment.CommandLine.Contains("--debug"))
        {
            // Wait for a debugger to be attached
            Console.WriteLine("Waiting for debugger to attach...");
            while (!System.Diagnostics.Debugger.IsAttached)
            {
                // Wait a bit and try again
                Thread.Sleep(100);
            }
            Console.WriteLine("Debugger attached.");

            // Break into the debugger
            System.Diagnostics.Debugger.Break();
        }
    }

    private static void CreateLogger(string logfile)
    {
        // Enable Serilog debug output to the console
        SelfLog.Enable(Console.Error);

        // Log to console
        // outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        // Remove lj to quote strings
        LoggerConfiguration loggerConfiguration = new();

        // Log Thread Id
        // Need to explicitly add thread id formatting to file and console output
        loggerConfiguration.Enrich.WithThreadId();

        // Default minimum log level
        #if DEBUG
            LogEventLevel logLevelDefault = LogEventLevel.Debug;
        #else
            LogEventLevel logLevelDefault = LogEventLevel.Information;
        #endif

        // Log to console
        loggerConfiguration.WriteTo.Console(theme: AnsiConsoleTheme.Code, 
            restrictedToMinimumLevel: logLevelDefault,
            outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] <{ThreadId}> {Message}{NewLine}{Exception}");

        // Log to file
        // outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
        // Remove lj to quote strings
        if (!string.IsNullOrEmpty(logfile))
        {
            // Set log level
            if (Options.LogWarning)
            {
                logLevelDefault = LogEventLevel.Warning;
            }

            // Write async to file
            // Default max size is 1GB, roll when max size is reached
            loggerConfiguration.WriteTo.Async(action => action.File(logfile, 
                restrictedToMinimumLevel: logLevelDefault,
                rollOnFileSizeLimit: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] <{ThreadId}> {Message}{NewLine}{Exception}"));
        }

        // Create static Serilog logger
        Log.Logger = loggerConfiguration.CreateLogger();

        // Set library logger to Serilog logger
        LogOptions.Logger = Log.Logger;
    }

    internal static int WriteDefaultSettingsCommand(CommandLineOptions options)
    {
        Log.Logger.Information("Writing default settings to {SettingsFile}", options.SettingsFile);

        // Save default config
        ConfigFileJsonSchema.WriteDefaultsToFile(options.SettingsFile);

        return MakeExitCode(ExitCode.Success);
    }

    internal static int CreateJsonSchemaCommand(CommandLineOptions options)
    {
        Log.Logger.Information("Writing settings JSON schema to {SchemaFile}", options.SchemaFile);

        // Write schema
        ConfigFileJsonSchema.WriteSchemaToFile(options.SchemaFile);

        return MakeExitCode(ExitCode.Success);
    }

    internal static int CheckForNewToolsCommand(CommandLineOptions options)
    {
        // Do not verify tools
        Program program = Create(options, false);
        if (program == null)
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Update tools
        // Make sure that the tools exist
        return MakeExitCode(Tools.CheckForNewTools() && Tools.VerifyTools());
    }

    internal static int ProcessCommand(CommandLineOptions options)
    {
        // Create program and get file list
        Program program = CreateFileList(options);
        if (program == null)
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Process all files and delete empty folders
        return MakeExitCode(Process.ProcessFiles(program.FileList) && Process.DeleteEmptyFolders(program.DirectoryList));
    }

    internal static int MonitorCommand(CommandLineOptions options)
    {
        // Create program
        Program program = Create(options, true);
        if (program == null)
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Monitor and process changes
        Monitor monitor = new();
        return MakeExitCode(monitor.MonitorFolders(options.MediaFiles));
    }

    internal static int ReMuxCommand(CommandLineOptions options)
    {
        // Create program and get file list
        Program program = CreateFileList(options);
        if (program == null)
        {
            return MakeExitCode(ExitCode.Error);
        }

        // ReMux
        return MakeExitCode(Process.ReMuxFiles(program.FileList));
    }

    internal static int ReEncodeCommand(CommandLineOptions options)
    {
        // Create program and get file list
        Program program = CreateFileList(options);
        if (program == null)
        {
            return MakeExitCode(ExitCode.Error);
        }

        // ReEncode
        return MakeExitCode(Process.ReEncodeFiles(program.FileList));
    }

    internal static int DeInterlaceCommand(CommandLineOptions options)
    {
        // Create program and get file list
        Program program = CreateFileList(options);
        if (program == null)
        {
            return MakeExitCode(ExitCode.Error);
        }

        // DeInterlace
        return MakeExitCode(Process.DeInterlaceFiles(program.FileList));
    }

    internal static int CreateSidecarCommand(CommandLineOptions options)
    {
        // Create program and get file list
        Program program = CreateFileList(options);
        if (program == null)
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Create sidecar files
        return MakeExitCode(Process.CreateSidecarFiles(program.FileList));
    }

    internal static int GetSidecarCommand(CommandLineOptions options)
    {
        // Create program and get file list
        Program program = CreateFileList(options);
        if (program == null)
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Get sidecar files info
        return MakeExitCode(Process.GetSidecarFiles(program.FileList));
    }

    internal static int UpdateSidecarCommand(CommandLineOptions options)
    {
        // Create program and get file list
        Program program = CreateFileList(options);
        if (program == null)
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Update sidecar files
        return MakeExitCode(Process.UpdateSidecarFiles(program.FileList));
    }

    internal static int GetTagMapCommand(CommandLineOptions options)
    {
        // Create program and get file list
        Program program = CreateFileList(options);
        if (program == null)
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Get tag map
        return MakeExitCode(Process.GetTagMapFiles(program.FileList));
    }

    internal static int GetMediaInfoCommand(CommandLineOptions options)
    {
        // Create program and get file list
        Program program = CreateFileList(options);
        if (program == null)
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Get media info
        return MakeExitCode(Process.GetMediaInfoFiles(program.FileList));
    }

    internal static int GetToolInfoCommand(CommandLineOptions options)
    {
        // Create program and get file list
        Program program = CreateFileList(options);
        if (program == null)
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Get tool info
        return MakeExitCode(Process.GetToolInfoFiles(program.FileList));
    }

    internal static int RemoveSubtitlesCommand(CommandLineOptions options)
    {
        // Create program and get file list
        Program program = CreateFileList(options);
        if (program == null)
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Remove subtitles
        return MakeExitCode(Process.RemoveSubtitlesFiles(program.FileList));
    }

    internal static int GetVersionInfoCommand(CommandLineOptions options)
    {
        // Creating the program object will report all version information
        // Do not verify the tools during create
        Program program = Create(options, false);
        if (program == null)
        {
            return MakeExitCode(ExitCode.Error);
        }

        // Verify tools to get tool version information
        return MakeExitCode(Tools.VerifyTools());
    }

    private static Program CreateFileList(CommandLineOptions options)
    {
        // Create program and enumerate files
        var program = Create(options, true);
        if (program == null || !program.CreateFileList(options.MediaFiles))
        {
            return null;
        }
        return program;
    }

    private static Program Create(CommandLineOptions options, bool verifyTools)
    {
        // Does the file exist
        if (!File.Exists(options.SettingsFile))
        {
            Log.Logger.Error("Settings file not found : {SettingsFile}", options.SettingsFile);
            return null;
        }

        // Load config from JSON
        Log.Logger.Information("Loading settings from : {SettingsFile}", options.SettingsFile);
        ConfigFileJsonSchema config = ConfigFileJsonSchema.FromFile(options.SettingsFile);
        if (config == null)
        {
            Log.Logger.Error("Failed to load settings : {FileName}", options.SettingsFile);
            return null;
        }

        // Compare the schema version
        if (config.SchemaVersion != ConfigFileJsonSchema.Version)
        {
            Log.Logger.Warning("Settings JSON schema version mismatch : {SchemaVersion} != {Version}, {FileName}",
                config.SchemaVersion,
                ConfigFileJsonSchema.Version,
                options.SettingsFile);

            // Upgrade the file schema
            Log.Logger.Information("Writing upgraded settings file : {FileName}", options.SettingsFile);
            ConfigFileJsonSchema.ToFile(options.SettingsFile, config);
        }

        // Verify the settings
        if (!config.VerifyValues())
        {
            Log.Logger.Error("Settings file contains incorrect or missing values : {FileName}", options.SettingsFile);
            return null;
        }

        // Set the static options from the loaded settings and options
        Options = options;
        Config = config;

        // Set the FileEx options
        FileEx.Options.TestNoModify = Options.TestNoModify;
        FileEx.Options.RetryCount = Config.MonitorOptions.FileRetryCount;
        FileEx.Options.RetryWaitTime = Config.MonitorOptions.FileRetryWaitTime;

        // Set the FileEx Cancel object
        FileEx.Options.Cancel = CancelSource.Token;

        // Use log file
        if (!string.IsNullOrEmpty(Options.LogFile))
        {
            // Delete if not in append mode
            if (!Options.LogAppend &&
                !FileEx.DeleteFile(Options.LogFile))
            {
                Log.Logger.Error("Failed to clear the logfile : {LogFile}", Options.LogFile);
                return null;
            }

            // Recreate the logger with a file
            CreateLogger(Options.LogFile);
            Log.Logger.Information("Logging output to : {LogFile}", Options.LogFile);
        }

        // Log app and runtime version
        #if DEBUG
            const bool debugBuild = true;
        #else
            const bool debugBuild = false;
        #endif
        string appVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        string runtimeVersion = Environment.Version.ToString();
        Log.Logger.Information("Application Version : {AppVersion}, Runtime Version : {RuntimeVersion}, Debug Build: {DebugBuild}", appVersion, runtimeVersion, debugBuild);

        // Parallel processing config
        if (Options.Parallel)
        {
            // If threadcount is 0 (default) use half the number of processors
            if (Options.ThreadCount == 0)
            { 
                Options.ThreadCount = Math.Max(Environment.ProcessorCount / 2, 1);
            }
        }
        else
        {
            // If disabled set the threadcount to 1
            Options.ThreadCount = 1;
        }
        Log.Logger.Information("Parallel Processing: {Parallel} : Thread Count: {ThreadCount}, Processor Count: {ProcessorCount}", Options.Parallel, Options.ThreadCount, Environment.ProcessorCount);

        // Verify tools
        if (verifyTools)
        {
            // Upgrade tools if auto update is enabled
            if (Config.ToolsOptions.AutoUpdate &&
                !Tools.CheckForNewTools())
            {
                // Ignore error, do not stop execution in case of e.g. a site being down
                Log.Logger.Error("Checking for new tools failed but continuing with existing tool versions");
            }

            // Verify tools
            if (!Tools.VerifyTools())
            {
                // Error
                return null;
            }
        }

        // Create program instance
        return new Program();
    }
        
    private bool CreateFileList(List<string> mediaFiles)
    {
        Log.Logger.Information("Creating file and folder list ...");

        // Trim quotes from input paths
        mediaFiles = mediaFiles.Select(file => file.Trim('"')).ToList();

        bool fatalError =false;
        try
        {
            // No need for concurrent collections, number of items are small, and added in bulk, just lock when adding results
            var lockObject = new Object();

            // Process each input in parallel
            mediaFiles.AsParallel()
                .WithDegreeOfParallelism(Options.ThreadCount)
                .WithCancellation(CancelToken())
                .ForAll(fileOrFolder =>
            {
                // Handle cancel request
                CancelToken().ThrowIfCancellationRequested();

                // Test for file or a directory
                var fileAttributes = File.GetAttributes(fileOrFolder);
                if (fileAttributes.HasFlag(FileAttributes.Directory))
                {
                    // Add this directory
                    lock (lockObject)
                    { 
                        DirectoryList.Add(fileOrFolder);
                    }

                    // Create the file list from the directory
                    Log.Logger.Information("Enumerating files in {Directory} ...", fileOrFolder);
                    if (!FileEx.EnumerateDirectory(fileOrFolder, out List<FileInfo> fileInfoList, out _))
                    {
                        // Abort
                        Log.Logger.Error("Failed to enumerate files in directory {Directory}", fileOrFolder);
                        Cancel();
                        CancelToken().ThrowIfCancellationRequested();
                    }

                    // Add file list
                    lock (lockObject)
                    {
                        fileInfoList.ForEach(item => FileList.Add(item.FullName));
                    }
                }
                else
                {
                    // Add this file
                    lock (lockObject)
                    {
                        FileList.Add(fileOrFolder);
                    }
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Cancelled
            fatalError = true;
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            // Error
            fatalError = true;
        }

        // Report
        Log.Logger.Information("Discovered {FileListCount} files from {DirectoryListCount} directories", FileList.Count, DirectoryList.Count);

        return !fatalError;
    }

    public static bool IsCancelledError()
    {
        // Test immediately
        if (CancelSource.IsCancellationRequested)
        {
            return true;
        }

        // There is a race condition between tools exiting on Ctrl-C and reporting an error, and our app's Ctrl-C handler being called
        // In case of a suspected Ctrl-C, yield some time for this handler to be called before testing the state
        return WaitForCancel(100);
    }

    public static bool WaitForCancel(int millisecond)
    {
        return CancelSource.Token.WaitHandle.WaitOne(millisecond);
    }

    public static bool IsCancelled()
    {
        return CancelSource.IsCancellationRequested;
    }

    public static void Cancel()
    {
        // Signal cancel
        CancelSource.Cancel();
    }

    public static CancellationToken CancelToken()
    {
        return CancelSource.Token;
    }

    // Commandline options
    public static CommandLineOptions Options { get; internal set; }

    // Config file options
    public static ConfigFileJsonSchema Config { get; internal set; }

    // Snippet runtime in seconds
    public static readonly TimeSpan SnippetTimeSpan = TimeSpan.FromSeconds(30);

    // Interlaced detection threshold as percentage
    public const double InterlacedThreshold = 5.0 / 100.0;

    // Cancellation token
    private static readonly CancellationTokenSource CancelSource = new();

    // File and directory lists
    private readonly List<string> DirectoryList = new();
    private readonly List<string> FileList = new();
}
