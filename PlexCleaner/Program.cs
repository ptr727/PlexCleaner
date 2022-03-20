using InsaneGenius.Utilities;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Debugging;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace PlexCleaner;

internal class Program
{
    private static int Main()
    {
        // Create default logger
        CreateLogger(null);

        // Create a 15s timer to keep the system from going to sleep
        // TODO: The system occasionally still goes to sleep when debugging, why?
        KeepAwake.PreventSleep();
        using System.Timers.Timer keepAwakeTimer = new(15 * 1000);
        keepAwakeTimer.Elapsed += KeepAwake.OnTimedEvent;
        keepAwakeTimer.AutoReset = true;
        keepAwakeTimer.Start();

        // Create the commandline and execute commands
        RootCommand rootCommand = CommandLineOptions.CreateRootCommand();
        int ret = rootCommand.Invoke(Environment.CommandLine);

        // Stop the timer
        keepAwakeTimer.Stop();
        keepAwakeTimer.Dispose();
        KeepAwake.AllowSleep();

        // Flush the logs
        Log.CloseAndFlush();

        return ret;
    }

    private static void CreateLogger(string logfile)
    {
        // Enable Serilog debug output to the console
        SelfLog.Enable(Console.Error);

        // Log to console
        // outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        // Remove lj to quote strings
        LoggerConfiguration loggerConfiguration = new();
        loggerConfiguration.WriteTo.Console(theme: AnsiConsoleTheme.Code, outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message}{NewLine}{Exception}");

        // Log to file
        // outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
        // Remove lj to quote strings
        if (!string.IsNullOrEmpty(logfile))
        {
            loggerConfiguration.WriteTo.Async(action => action.File(logfile, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message}{NewLine}{Exception}"));
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
        return process.ProcessFiles(program.FileInfoList) &&
               Process.DeleteEmptyFolders(program.FolderList) ? 0 : -1;
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
        return process.ReMuxFiles(program.FileInfoList) ? 0 : -1;
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

        return Process.ReEncodeFiles(program.FileInfoList) ? 0 : -1;
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

        return Process.DeInterlaceFiles(program.FileInfoList) ? 0 : -1;
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

        return Process.CreateSidecarFiles(program.FileInfoList) ? 0 : -1;
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

        return Process.GetSidecarFiles(program.FileInfoList) ? 0 : -1;
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

        return Process.GetTagMapFiles(program.FileInfoList) ? 0 : -1;
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

        return Process.GetMediaInfoFiles(program.FileInfoList) ? 0 : -1;
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

        return Process.GetToolInfoFiles(program.FileInfoList) ? 0 : -1;
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

        return Process.RemoveSubtitlesFiles(program.FileInfoList) ? 0 : -1;
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
        // Load config from JSON
        if (!File.Exists(options.SettingsFile))
        {
            Log.Logger.Error("Settings file not found : {SettingsFile}", options.SettingsFile);
            return null;
        }
        Log.Logger.Information("Loading settings from : {SettingsFile}", options.SettingsFile);
        ConfigFileJsonSchema config = ConfigFileJsonSchema.FromFile(options.SettingsFile);

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

        // Set the static options from the loaded settings
        Options = options;
        Config = config;

        // Set the FileEx options
        FileEx.Options.TestNoModify = Options.TestNoModify;
        FileEx.Options.RetryCount = config.MonitorOptions.FileRetryCount;
        FileEx.Options.RetryWaitTime = config.MonitorOptions.FileRetryWaitTime;

        // Set the FileEx Cancel object
        FileEx.Options.Cancel = CancelSource.Token;

        // Use log file
        if (!string.IsNullOrEmpty(options.LogFile))
        {
            // Delete if not in append mode
            if (!options.LogAppend &&
                !FileEx.DeleteFile(options.LogFile))
            {
                Log.Logger.Error("Failed to clear the logfile : {LogFile}", options.LogFile);
                return null;
            }

            // Recreate the logger with a file
            CreateLogger(options.LogFile);
            Log.Logger.Information("Logging output to : {LogFile}", options.LogFile);
        }

        // Log app and runtime version
        string appVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        string runtimeVersion = Environment.Version.ToString();
        Log.Logger.Information("Application Version : {AppVersion}, Runtime Version : {RuntimeVersion}", appVersion, runtimeVersion);

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

    private bool CreateFileList(List<string> files)
    {
        Log.Logger.Information("Creating file and folder list ...");

        // Trim quotes from input paths
        files = files.Select(file => file.Trim('"')).ToList();

        // Process all entries
        foreach (string fileOrFolder in files)
        {
            // File or a directory
            FileAttributes fileAttributes;
            try
            {
                fileAttributes = File.GetAttributes(fileOrFolder);
            }
            catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
            {
                return false;
            }

            if (fileAttributes.HasFlag(FileAttributes.Directory))
            {
                // Add this directory
                DirectoryInfo dirInfo = new(fileOrFolder);
                DirectoryInfoList.Add(dirInfo);
                FolderList.Add(fileOrFolder);

                // Create the file list from the directory
                Log.Logger.Information("Getting files and folders from {Directory} ...", dirInfo.FullName);
                if (!FileEx.EnumerateDirectory(fileOrFolder, out List<FileInfo> fileInfoList, out List<DirectoryInfo> directoryInfoList))
                {
                    Log.Logger.Error("Failed to enumerate directory {Directory}", fileOrFolder);
                    return false;
                }
                FileInfoList.AddRange(fileInfoList);
                DirectoryInfoList.AddRange(directoryInfoList);
            }
            else
            {
                // Add this file
                FileInfoList.Add(new FileInfo(fileOrFolder));
            }
        }

        // Report
        Log.Logger.Information("Discovered {DirectoryInfoListCount} directories and {FileInfoListCount} files", DirectoryInfoList.Count, FileInfoList.Count);

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

    public static CommandLineOptions Options { get; private set; }
    public static ConfigFileJsonSchema Config { get; private set; }

    private static readonly CancellationTokenSource CancelSource = new();

    private readonly List<string> FolderList = new();
    private readonly List<DirectoryInfo> DirectoryInfoList = new();
    private readonly List<FileInfo> FileInfoList = new();
}