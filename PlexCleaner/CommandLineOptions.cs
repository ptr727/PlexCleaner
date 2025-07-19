using System.Collections.Generic;
using System.CommandLine;
using DotMake.CommandLine;

namespace PlexCleaner;

// TODO: Migrate to System.CommandLine Beta 5
// https://github.com/dotnet/command-line-api/issues/2576
// https://learn.microsoft.com/en-us/dotnet/standard/commandline/migration-guide-2.0.0-beta5

// TODO: NamingConventionBinder is being deprecated, alternatives:
// https://github.com/mayuki/Cocona
//  Not updated
// https://github.com/Cysharp/ConsoleAppFramework
//  https://github.com/Cysharp/ConsoleAppFramework/issues/140
// https://github.com/spectreconsole/spectre.console
// https://github.com/dotmake-build/command-line
//  https://github.com/dotmake-build/command-line/issues/40
// https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-providers#command-line-configuration-provider

// TODO: https://github.com/dotnet/command-line-api/issues/2593
// TODO: https://github.com/dotnet/command-line-api/issues/2628

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

// TODO: https://github.com/dotmake-build/command-line/issues/42
// TODO: https://github.com/dotmake-build/command-line/issues/46
[CliCommand] // TODO: Remove, required else exception at runtime
public class CommandLineParser
{
    internal CommandLineParser() { } // TODO: Remove, required because of CliCommand attribute

    private readonly CliResult _cliResult;

    public CommandLineParser(string[] args) => _cliResult = Cli.Parse<CliRootCommand>(args);

    public ParseResult Result => _cliResult.ParseResult;

    public CommandLineOptions Bind() => _cliResult.Bind<CliRootCommand>().Options;

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
        [CliOption(
            Description = "Path to media file or folder",
            Name = "mediafiles",
            Required = true
        )]
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
        [CliOption(
            Description = "Scan only part of the file",
            Name = "quickscan",
            Required = false
        )]
        bool QuickScan { get; set; }
    }

    public interface ISchemaOptions
    {
        [CliOption(Description = "Path to schema file", Name = "schemafile", Required = true)]
        string SchemaFile { get; set; }
    }

    public interface IProcessOptions : IProcessItemOptions, IParallelOptions
    {
        [CliOption(Description = "Path to results file", Name = "resultsfile", Required = false)]
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
        Name = "PlexCleaner", // TODO: Remove
        Description = "Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin, etc.",
        ShortFormAutoGenerate = CliNameAutoGenerate.None
    )]
    public class CliRootCommand : CliOptionsBase, IGlobalOptions
    {
        public override CommandLineOptions Options { get; set; } = new();

        public DefaultSettingsCommand DefaultSettings { get; set; }
        public CheckForNewToolsCommand CheckForNewTools { get; set; }
        public CreateSchemaCommand CreateSchema { get; set; }
        public ProcessCommand Process { get; set; }
        public MonitorCommand Monitor { get; set; }
        public ReMuxCommand ReMux { get; set; }
        public ReEncodeCommand ReEncode { get; set; }
        public DeInterlaceCommand DeInterlace { get; set; }
        public VerifyCommand Verify { get; set; }
        public CreateSidecarCommand CreateSidecar { get; set; }
        public GetSidecarCommand GetSidecar { get; set; }
        public UpdateSidecarCommand UpdateSidecar { get; set; }
        public GetTagMapCommand GetTagMap { get; set; }
        public GetMediaInfoCommand GetMediaInfo { get; set; }
        public TestMediaInfoCommand TestMediaInfo { get; set; }
        public GetToolInfoCommand GetToolInfo { get; set; }
        public RemoveSubtitlesCommand RemoveSubtitles { get; set; }
        public RemoveClosedCaptionsCommand RemoveClosedCaptions { get; set; }
        public GetVersionInfoCommand GetVersionInfo { get; set; }

        [CliCommand(
            Description = "Create JSON configuration file using default settings",
            Name = "defaultsettings"
        )]
        public class DefaultSettingsCommand
            : CliCommandBase,
                ISettingsFileOptions,
                ICliRunWithReturn
        {
            public int Run() => Program.DefaultSettingsCommand();
        }

        [CliCommand(
            Description = "Check for new tool versions and download if newer",
            Name = "checkfornewtools"
        )]
        public class CheckForNewToolsCommand
            : CliCommandBase,
                ISettingsFileOptions,
                ICliRunWithReturn
        {
            public int Run() => Program.CheckForNewToolsCommand();
        }

        [CliCommand(Description = "Write JSON settings schema to file", Name = "createschema")]
        public class CreateSchemaCommand : CliCommandBase, ISchemaOptions, ICliRunWithReturn
        {
            public int Run() => Program.CreateSchemaCommand();
        }

        [CliCommand(Description = "Process media files", Name = "process")]
        public class ProcessCommand : CliCommandBase, IProcessOptions, ICliRunWithReturn
        {
            public int Run() => Program.ProcessCommand();
        }

        [CliCommand(
            Description = "Monitor for file changes and process changed media files",
            Name = "monitor"
        )]
        public class MonitorCommand : CliCommandBase, IMonitorOptions, ICliRunWithReturn
        {
            public int Run() => Program.MonitorCommand();
        }

        [CliCommand(Description = "Conditionally re-multiplex media files", Name = "remux")]
        public class ReMuxCommand : CliCommandBase, IProcessItemOptions, ICliRunWithReturn
        {
            public int Run() => Program.ReMuxCommand();
        }

        [CliCommand(Description = "Conditionally re-encode media files", Name = "reencode")]
        public class ReEncodeCommand : CliCommandBase, IProcessItemOptions, ICliRunWithReturn
        {
            public int Run() => Program.ReEncodeCommand();
        }

        [CliCommand(Description = "Conditionally de-interlace media files", Name = "deinterlace")]
        public class DeInterlaceCommand : CliCommandBase, IProcessItemOptions, ICliRunWithReturn
        {
            public int Run() => Program.DeInterlaceCommand();
        }

        [CliCommand(Description = "Verify media container and stream integrity", Name = "verify")]
        public class VerifyCommand : CliCommandBase, IProcessItemOptions, ICliRunWithReturn
        {
            public int Run() => Program.VerifyCommand();
        }

        [CliCommand(Description = "Create new sidecar files", Name = "createsidecar")]
        public class CreateSidecarCommand : CliCommandBase, ISettingsMediaOptions, ICliRunWithReturn
        {
            public int Run() => Program.CreateSidecarCommand();
        }

        [CliCommand(Description = "Print sidecar file information", Name = "getsidecar")]
        public class GetSidecarCommand : CliCommandBase, ISettingsMediaOptions, ICliRunWithReturn
        {
            public int Run() => Program.GetSidecarCommand();
        }

        [CliCommand(Description = "Create or update sidecar files", Name = "updatesidecar")]
        public class UpdateSidecarCommand : CliCommandBase, ISettingsMediaOptions, ICliRunWithReturn
        {
            public int Run() => Program.UpdateSidecarCommand();
        }

        [CliCommand(Description = "Print media file attribute mappings", Name = "gettagmap")]
        public class GetTagMapCommand : CliCommandBase, ISettingsMediaOptions, ICliRunWithReturn
        {
            public int Run() => Program.GetTagMapCommand();
        }

        [CliCommand(Description = "Print media file information", Name = "getmediainfo")]
        public class GetMediaInfoCommand : CliCommandBase, ISettingsMediaOptions, ICliRunWithReturn
        {
            public int Run() => Program.GetMediaInfoCommand();
        }

        [CliCommand(Description = "Test parsing media file information", Name = "testmediainfo")]
        public class TestMediaInfoCommand : CliCommandBase, ISettingsMediaOptions, ICliRunWithReturn
        {
            public int Run() => Program.TestMediaInfoCommand();
        }

        [CliCommand(Description = "Print media tool information", Name = "gettoolinfo")]
        public class GetToolInfoCommand : CliCommandBase, ISettingsMediaOptions, ICliRunWithReturn
        {
            public int Run() => Program.GetToolInfoCommand();
        }

        [CliCommand(Description = "Remove all subtitle tracks", Name = "removesubtitles")]
        public class RemoveSubtitlesCommand
            : CliCommandBase,
                ISettingsMediaOptions,
                ICliRunWithReturn
        {
            public int Run() => Program.RemoveSubtitlesCommand();
        }

        [CliCommand(
            Description = "Remove closed captions from video stream",
            Name = "removeclosedcaptions"
        )]
        public class RemoveClosedCaptionsCommand
            : CliCommandBase,
                IProcessParallelOptions,
                ICliRunWithReturn
        {
            public int Run() => Program.RemoveClosedCaptionsCommand();
        }

        [CliCommand(
            Description = "Print application and tools version information",
            Name = "getversioninfo"
        )]
        public class GetVersionInfoCommand : CliCommandBase, ISettingsFileOptions, ICliRunWithReturn
        {
            public int Run() => Program.GetVersionInfoCommand();
        }
    }
}
