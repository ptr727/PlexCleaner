using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using InsaneGenius.Utilities;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Debugging;
using Serilog.Sinks.SystemConsole.Themes;

namespace PlexCleaner;

internal class Program
{
    private static int Main()
    {
        // Wait for debugger to attach
        WaitForDebugger();

        // Create default logger
        CreateLogger(null);

        // Create a timer to keep the system from going to sleep
        KeepAwake.PreventSleep();
        using System.Timers.Timer keepAwakeTimer = new(30 * 1000);
        keepAwakeTimer.Elapsed += KeepAwake.OnTimedEvent;
        keepAwakeTimer.AutoReset = true;
        keepAwakeTimer.Start();

        // Create the commandline and execute commands
        int ret = CommandLineOptions.Invoke();

        // Stop the timer
        keepAwakeTimer.Stop();
        keepAwakeTimer.Dispose();
        KeepAwake.AllowSleep();

        // Flush the logs
        Log.CloseAndFlush();

        return ret;
    }

    private static void WaitForDebugger()
    {
        // Use the raw commandline and look for --debug
        if (Environment.CommandLine.Contains("--debug"))
        {
            // Wait for a debugger to be attached
            Console.WriteLine("Waiting for debugger to attach...");
            while (!System.Diagnostics.Debugger.IsAttached)
            {
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

        // Log to console
        loggerConfiguration.WriteTo.Console(theme: AnsiConsoleTheme.Code, outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] <{ThreadId}> {Message}{NewLine}{Exception}");

        // Log to file
        // outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
        // Remove lj to quote strings
        if (!string.IsNullOrEmpty(logfile))
        {
            loggerConfiguration.WriteTo.Async(action => action.File(logfile, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] <{ThreadId}> {Message}{NewLine}{Exception}"));
        }

        // Create static Serilog logger
        Log.Logger = loggerConfiguration.CreateLogger();

        // Set library logger to Serilog logger
        LoggerFactory loggerFactory = new();
        loggerFactory.AddSerilog(Log.Logger);
        LogOptions.CreateLogger(loggerFactory);
    }

    internal static int WriteDefaultSettingsCommand(CommandLineOptions options)
    {
        Log.Logger.Information("Writing default settings to {SettingsFile}", options.SettingsFile);

        // Save default config
        ConfigFileJsonSchema.WriteDefaultsToFile(options.SettingsFile);

        return 0;
    }

    internal static int CheckForNewToolsCommand(CommandLineOptions options)
    {
        // Do not verify tools
        Program program = Create(options, false);
        if (program == null)
        {
            return -1;
        }

        // Update tools
        // Make sure that the tools exist
        return Tools.CheckForNewTools() && Tools.VerifyTools() ? 0 : -1;
    }

    internal static int ProcessCommand(CommandLineOptions options)
    {
        Program program = Create(options, true);
        if (program == null)
        {
            return -1;
        }

        // Get file list
        if (!program.CreateFileList(options.MediaFiles))
        {
            return -1;
        }

        // Process all files
        Process process = new();
        if (!process.ProcessFiles(program.FileList) || 
            IsCancelledError())
        {
            return -1;
        }
        return Process.DeleteEmptyFolders(program.DirectoryList) ? 0 : -1;
    }

    internal static int MonitorCommand(CommandLineOptions options)
    {
        Program program = Create(options, true);
        if (program == null)
        {
            return -1;
        }

        Monitor monitor = new();
        return monitor.MonitorFolders(options.MediaFiles) ? 0 : -1;
    }

    internal static int ReMuxCommand(CommandLineOptions options)
    {
        Program program = Create(options, true);
        if (program == null)
        {
            return -1;
        }

        if (!program.CreateFileList(options.MediaFiles))
        {
            return -1;
        }

        Process process = new();
        return process.ReMuxFiles(program.FileList) ? 0 : -1;
    }

    internal static int ReEncodeCommand(CommandLineOptions options)
    {
        Program program = Create(options, true);
        if (program == null)
        {
            return -1;
        }

        if (!program.CreateFileList(options.MediaFiles))
        {
            return -1;
        }

        return Process.ReEncodeFiles(program.FileList) ? 0 : -1;
    }

    internal static int DeInterlaceCommand(CommandLineOptions options)
    {
        Program program = Create(options, true);
        if (program == null)
        {
            return -1;
        }

        if (!program.CreateFileList(options.MediaFiles))
        {
            return -1;
        }

        return Process.DeInterlaceFiles(program.FileList) ? 0 : -1;
    }

    internal static int CreateSidecarCommand(CommandLineOptions options)
    {
        Program program = Create(options, true);
        if (program == null)
        {
            return -1;
        }

        if (!program.CreateFileList(options.MediaFiles))
        {
            return -1;
        }

        return Process.CreateSidecarFiles(program.FileList) ? 0 : -1;
    }

    internal static int GetSidecarInfoCommand(CommandLineOptions options)
    {
        Program program = Create(options, true);
        if (program == null)
        {
            return -1;
        }

        if (!program.CreateFileList(options.MediaFiles))
        {
            return -1;
        }

        return Process.GetSidecarFiles(program.FileList) ? 0 : -1;
    }

    internal static int GetTagMapCommand(CommandLineOptions options)
    {
        Program program = Create(options, true);
        if (program == null)
        {
            return -1;
        }

        if (!program.CreateFileList(options.MediaFiles))
        {
            return -1;
        }

        return Process.GetTagMapFiles(program.FileList) ? 0 : -1;
    }

    internal static int GetMediaInfoCommand(CommandLineOptions options)
    {
        Program program = Create(options, true);
        if (program == null)
        {
            return -1;
        }

        if (!program.CreateFileList(options.MediaFiles))
        {
            return -1;
        }

        return Process.GetMediaInfoFiles(program.FileList) ? 0 : -1;
    }

    internal static int GetToolInfoCommand(CommandLineOptions options)
    {
        Program program = Create(options, true);
        if (program == null)
        {
            return -1;
        }

        if (!program.CreateFileList(options.MediaFiles))
        {
            return -1;
        }

        return Process.GetToolInfoFiles(program.FileList) ? 0 : -1;
    }

    internal static int RemoveSubtitlesCommand(CommandLineOptions options)
    {
        Program program = Create(options, true);
        if (program == null)
        {
            return -1;
        }

        if (!program.CreateFileList(options.MediaFiles))
        {
            return -1;
        }

        return Process.RemoveSubtitlesFiles(program.FileList) ? 0 : -1;
    }

    // Add a reference to this class in the event handler arguments
    private static void CancelHandlerEx(object s, ConsoleCancelEventArgs e)
    {
        CancelHandler(e);
    }

    private static void CancelHandler(ConsoleCancelEventArgs e)
    {
        Log.Logger.Warning("Cancel key pressed");
        e.Cancel = true;

        // Signal the cancel event
        // We could signal Cancel directly now that it is static
        Break();
    }

    private Program()
    {
        // Register cancel handler
        Console.CancelKeyPress += CancelHandlerEx;
    }

    ~Program()
    {
        // Unregister cancel handler
        Console.CancelKeyPress -= CancelHandlerEx;
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
            Log.Logger.Error("{FileName} is not a valid JSON file", options.SettingsFile);
            return null;
        }

        // Compare the schema version
        if (config.SchemaVersion != ConfigFileJsonSchema.Version)
        {
            Log.Logger.Warning("Settings JSON schema mismatch : {SchemaVersion} != {Version}, {FileName}",
                config.SchemaVersion,
                ConfigFileJsonSchema.Version,
                options.SettingsFile);

            // Upgrade the file schema
            Log.Logger.Information("Writing upgraded settings file : {FileName}", options.SettingsFile);
            ConfigFileJsonSchema.ToFile(options.SettingsFile, config);
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
        string appVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        string runtimeVersion = Environment.Version.ToString();
        Log.Logger.Information("Application Version : {AppVersion}, Runtime Version : {RuntimeVersion}", appVersion, runtimeVersion);

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
                return null;
            }

            // Verify tools
            if (!Tools.VerifyTools())
            {
                return null;
            }
        }

        // Create program instance
        return new Program();
    }

    private static void Break()
    {
        // Signal the cancel event
        Cancel();
    }

    private bool CreateFileList(List<string> mediaFiles)
    {
        Log.Logger.Information("Creating file and folder list ...");

        // Trim quotes from input paths
        mediaFiles = mediaFiles.Select(file => file.Trim('"')).ToList();

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
                    // TODO: Create a variant that returns strings
                    if (!FileEx.EnumerateDirectory(fileOrFolder, out List<FileInfo> fileInfoList, out _))
                    {
                        // Abort
                        Log.Logger.Error("Failed to enumerate files in directory {Directory}", fileOrFolder);
                        throw new OperationCanceledException();
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
            return false;
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            // Error
            return false;
        }

        // Report
        Log.Logger.Information("Discovered {FileListCount} files from {DirectoryListCount} directories", FileList.Count, DirectoryList.Count);

        return true;
    }

    public static bool IsCancelledError()
    {
        // There is a race condition between tools exiting on Ctrl-C and reporting an error, and our app's Ctrl-C handler being called
        // In case of a suspected Ctrl-C, yield some time for this handler to be called before testing the state
        return IsCancelled(100);
    }

    public static bool IsCancelled(int milliseconds = 0)
    {
        return CancelSource.Token.WaitHandle.WaitOne(milliseconds);
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

    public static CommandLineOptions Options { get; private set; }
    public static ConfigFileJsonSchema Config { get; private set; }

    private static readonly CancellationTokenSource CancelSource = new();

    private readonly List<string> DirectoryList = new();
    private readonly List<string> FileList = new();
}
