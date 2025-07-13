using System.Collections.Generic;
using System.Threading.Tasks;
using DotMake.CommandLine;

namespace PlexCleaner;

// TODO: Migrate to System.CommandLine Beta 5
// https://github.com/dotnet/command-line-api/issues/2576
// https://learn.microsoft.com/en-us/dotnet/standard/commandline/migration-guide-2.0.0-beta5

// TODO: NamingConventionBinder is being deprecated, switch to alternative:
// https://github.com/mayuki/Cocona
// https://github.com/Cysharp/ConsoleAppFramework
// https://github.com/spectreconsole/spectre.console
// https://github.com/dotmake-build/command-line
// https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-providers#command-line-configuration-provider

// TODO: https://github.com/dotnet/command-line-api/issues/2593
// TODO: https://github.com/dotmake-build/command-line/issues/40
// TODO: https://github.com/dotmake-build/command-line/issues/41
// TODO: https://github.com/dotmake-build/command-line/issues/42

public class CommandLineOptions
{
    public bool Debug { get; set; }
    public bool LogAppend { get; set; }
    public bool LogWarning { get; set; }
    public bool Parallel { get; set; }
    public bool PreProcess { get; set; }
    public bool QuickScan { get; set; }
    public bool TestSnippets { get; set; }
    public int ThreadCount { get; set; }
    public List<string> MediaFiles { get; set; } = [];
    public string LogFile { get; set; } = string.Empty;
    public string ResultsFile { get; set; } = string.Empty;
    public string SchemaFile { get; set; } = string.Empty;
    public string SettingsFile { get; set; } = string.Empty;
}

public interface IGlobalOptions
{
    [CliOption(
        Description = "Path to log file",
        Name = "logfile",
        Recursive = true,
        Required = false
    )]
    string LogFile { get; set; }

    [CliOption(
        Description = "Append to existing log file",
        Name = "logappend",
        Recursive = true,
        Required = false
    )]
    bool LogAppend { get; set; }

    [CliOption(
        Description = "Log warnings and errors only",
        Name = "logwarning",
        Recursive = true,
        Required = false
    )]
    bool LogWarning { get; set; }

    [CliOption(
        Description = "Wait for debugger to attach",
        Name = "debug",
        Recursive = true,
        Required = false
    )]
    bool Debug { get; set; }
}

public interface ISettingsFileOptions
{
    [CliOption(Description = "Path to settings file", Name = "settingsfile", Required = true)]
    string SettingsFile { get; set; }
}

public interface IMediaFilesOptions
{
    [CliOption(Description = "Path to media file or folder", Name = "mediafiles", Required = true)]
    List<string> MediaFiles { get; set; }
}

public interface IParallelOptions
{
    [CliOption(
        Description = "Enable parallel file processing",
        Name = "parallel",
        Required = false
    )]
    bool Parallel { get; set; }

    [CliOption(
        Description = "Number of threads for parallel file processing",
        Name = "threadcount",
        Required = false
    )]
    int ThreadCount { get; set; }
}

public interface IQuickScanOptions
{
    [CliOption(Description = "Scan only part of the file", Name = "quickscan", Required = false)]
    bool QuickScan { get; set; }
}

public interface ISchemaOptions
{
    [CliOption(Description = "Path to schema file", Name = "schemafile", Required = true)]
    string SchemaFile { get; set; }
}

public interface IProcessOptions : IProcessItemOptions, IParallelOptions
{
    [CliOption(Description = "Path to results file")]
    string ResultsFile { get; set; }

    [CliOption(
        Description = "Create short media file clips",
        Name = "testsnippets",
        Required = false
    )]
    bool TestSnippets { get; set; }
}

public interface IMonitorOptions : IProcessItemOptions, IParallelOptions
{
    [CliOption(
        Description = "Pre-process media files before monitoring",
        Name = "preprocess",
        Required = false
    )]
    bool PreProcess { get; set; }
}

public interface ISettingsMediaOptions : ISettingsFileOptions, IMediaFilesOptions;

public interface IProcessItemOptions : ISettingsMediaOptions, IQuickScanOptions;

public interface IProcessParallelOptions : IProcessItemOptions, IParallelOptions;

public abstract class CliOptionsBase
{
    public abstract CommandLineOptions Options { get; set; }

    public string SettingsFile
    {
        get => Options.SettingsFile;
        set => Options.SettingsFile = value;
    }

    public List<string> MediaFiles
    {
        get => Options.MediaFiles;
        set => Options.MediaFiles = value;
    }

    public bool QuickScan
    {
        get => Options.QuickScan;
        set => Options.QuickScan = value;
    }

    public bool Parallel
    {
        get => Options.Parallel;
        set => Options.Parallel = value;
    }

    public int ThreadCount
    {
        get => Options.ThreadCount;
        set => Options.ThreadCount = value;
    }

    public string LogFile
    {
        get => Options.LogFile;
        set => Options.LogFile = value;
    }

    public string SchemaFile
    {
        get => Options.SchemaFile;
        set => Options.SchemaFile = value;
    }

    public bool LogAppend
    {
        get => Options.LogAppend;
        set => Options.LogAppend = value;
    }

    public bool LogWarning
    {
        get => Options.LogWarning;
        set => Options.LogWarning = value;
    }

    public bool Debug
    {
        get => Options.Debug;
        set => Options.Debug = value;
    }

    public bool PreProcess
    {
        get => Options.PreProcess;
        set => Options.PreProcess = value;
    }

    public string ResultsFile
    {
        get => Options.ResultsFile;
        set => Options.ResultsFile = value;
    }

    public bool TestSnippets
    {
        get => Options.TestSnippets;
        set => Options.TestSnippets = value;
    }
}

public abstract class CliCommandBase : CliOptionsBase
{
    public required CliRootCommand RootCommand { get; set; }
    public override CommandLineOptions Options
    {
        get => RootCommand.Options;
        set => RootCommand.Options = value;
    }
}

[CliCommand(
    Description = "Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin, etc.",
    ShortFormAutoGenerate = CliNameAutoGenerate.None,
    NameCasingConvention = CliNameCasingConvention.LowerCase
)]
public class CliRootCommand : CliOptionsBase, IGlobalOptions
{
    public override CommandLineOptions Options { get; set; } = new();

    [CliCommand(
        Description = "Create JSON configuration file using default settings",
        Name = "defaultsettings"
    )]
    public class DefaultSettingsCommand
        : CliCommandBase,
            ISettingsFileOptions,
            ICliRunAsyncWithReturn
    {
        public Task<int> RunAsync() => Program.DefaultSettingsCommandAsync();
    }

    [CliCommand(
        Description = "Check for new tool versions and download if newer",
        Name = "checkfornewtools"
    )]
    public class CheckForNewToolsCommand
        : CliCommandBase,
            ISettingsFileOptions,
            ICliRunAsyncWithReturn
    {
        public Task<int> RunAsync() => Program.CheckForNewToolsCommandAsync();
    }

    [CliCommand(Description = "Write JSON settings schema to file", Name = "createschema")]
    public class CreateSchemaCommand : CliCommandBase, ISchemaOptions, ICliRunAsyncWithReturn
    {
        public Task<int> RunAsync() => Program.CreateSchemaCommandAsync();
    }

    [CliCommand(Description = "Process media files", Name = "process")]
    public class ProcessCommand : CliCommandBase, IProcessOptions, ICliRunAsyncWithReturn
    {
        public Task<int> RunAsync() => Program.ProcessCommandAsync();
    }

    [CliCommand(
        Description = "Monitor for file changes and process changed media files",
        Name = "monitor"
    )]
    public class MonitorCommand : CliCommandBase, IMonitorOptions, ICliRunAsyncWithReturn
    {
        public Task<int> RunAsync() => Program.MonitorCommandAsync();
    }

    [CliCommand(Description = "Conditionally re-multiplex media files", Name = "remux")]
    public class ReMuxCommand : CliCommandBase, IProcessItemOptions, ICliRunAsyncWithReturn
    {
        public Task<int> RunAsync() => Program.ReMuxCommandAsync();
    }

    [CliCommand(Description = "Conditionally re-encode media files", Name = "reencode")]
    public class ReEncodeCommand : CliCommandBase, IProcessItemOptions, ICliRunAsyncWithReturn
    {
        public Task<int> RunAsync() => Program.ReEncodeCommandAsync();
    }

    [CliCommand(Description = "Conditionally de-interlace media files", Name = "deinterlace")]
    public class DeInterlaceCommand : CliCommandBase, IProcessItemOptions, ICliRunAsyncWithReturn
    {
        public Task<int> RunAsync() => Program.DeInterlaceCommandAsync();
    }

    [CliCommand(Description = "Verify media container and stream integrity", Name = "verify")]
    public class VerifyCommand : CliCommandBase, IProcessItemOptions, ICliRunAsyncWithReturn
    {
        public Task<int> RunAsync() => Program.VerifyCommandAsync();
    }

    [CliCommand(Description = "Create new sidecar files", Name = "createsidecar")]
    public class CreateSidecarCommand
        : CliCommandBase,
            ISettingsMediaOptions,
            ICliRunAsyncWithReturn
    {
        public Task<int> RunAsync() => Program.CreateSidecarCommandAsync();
    }

    [CliCommand(Description = "Print sidecar file information", Name = "getsidecar")]
    public class GetSidecarCommand : CliCommandBase, ISettingsMediaOptions, ICliRunAsyncWithReturn
    {
        public Task<int> RunAsync() => Program.GetSidecarCommandAsync();
    }

    [CliCommand(Description = "Create or update sidecar files", Name = "updatesidecar")]
    public class UpdateSidecarCommand
        : CliCommandBase,
            ISettingsMediaOptions,
            ICliRunAsyncWithReturn
    {
        public Task<int> RunAsync() => Program.UpdateSidecarCommandAsync();
    }

    [CliCommand(Description = "Print media file attribute mappings", Name = "gettagmap")]
    public class GetTagMapCommand : CliCommandBase, ISettingsMediaOptions, ICliRunAsyncWithReturn
    {
        public Task<int> RunAsync() => Program.GetTagMapCommandAsync();
    }

    [CliCommand(Description = "Print media file information", Name = "getmediainfo")]
    public class GetMediaInfoCommand : CliCommandBase, ISettingsMediaOptions, ICliRunAsyncWithReturn
    {
        public Task<int> RunAsync() => Program.GetMediaInfoCommandAsync();
    }

    [CliCommand(Description = "Test parsing media file information", Name = "testmediainfo")]
    public class TestMediaInfoCommand
        : CliCommandBase,
            ISettingsMediaOptions,
            ICliRunAsyncWithReturn
    {
        public Task<int> RunAsync() => Program.TestMediaInfoCommandAsync();
    }

    [CliCommand(Description = "Print media tool information", Name = "gettoolinfo")]
    public class GetToolInfoCommand : CliCommandBase, ISettingsMediaOptions, ICliRunAsyncWithReturn
    {
        public Task<int> RunAsync() => Program.GetToolInfoCommandAsync();
    }

    [CliCommand(Description = "Remove all subtitle tracks", Name = "removesubtitles")]
    public class RemoveSubtitlesCommand
        : CliCommandBase,
            ISettingsMediaOptions,
            ICliRunAsyncWithReturn
    {
        public Task<int> RunAsync() => Program.RemoveSubtitlesCommandAsync();
    }

    [CliCommand(
        Description = "Remove closed captions from video stream",
        Name = "removeclosedcaptions"
    )]
    public class RemoveClosedCaptionsCommand
        : CliCommandBase,
            IProcessParallelOptions,
            ICliRunAsyncWithReturn
    {
        public Task<int> RunAsync() => Program.RemoveClosedCaptionsCommandAsync();
    }

    [CliCommand(
        Description = "Print application and tools version information",
        Name = "getversioninfo"
    )]
    public class GetVersionInfoCommand
        : CliCommandBase,
            ISettingsFileOptions,
            ICliRunAsyncWithReturn
    {
        public Task<int> RunAsync() => Program.GetVersionInfoCommandAsync();
    }
}
