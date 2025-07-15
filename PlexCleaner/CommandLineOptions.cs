using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace PlexCleaner;

// TODO: Migrate to System.CommandLine Beta 5
// https://github.com/dotnet/command-line-api/issues/2576
// https://github.com/dotnet/command-line-api/issues/2628
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

public class CommandLineParser
{
    public CommandLineParser(string[] args)
    {
        Root = CreateRootCommand();
        Result = Root.Parse(args);
    }

    public RootCommand Root { get; init; }
    public ParseResult Result { get; init; }

    private class CommandHandler(Func<ParseResult, int> action) : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult) => action(parseResult);
    }

    private readonly Option<string> _logFileOption = new("--logfile")
    {
        Description = "Path to log file",
        Recursive = true,
    };

    private readonly Option<bool> _logAppendOption = new("--logappend")
    {
        Description = "Append to existing log file",
        Recursive = true,
    };

    private readonly Option<bool> _logWarningOption = new("--logwarning")
    {
        Description = "Log warnings and errors only",
        Recursive = true,
    };

    private readonly Option<bool> _debugOption = new("--debug")
    {
        Description = "Wait for debugger to attach",
        Recursive = true,
    };

    private readonly Option<string> _schemaFileOption = new("--schemafile")
    {
        Description = "Path to schema file",
        Required = true,
    };

    private readonly Option<string> _resultsFileOption = new("--resultsfile")
    {
        Description = "Path to results file",
    };

    private readonly Option<bool> _testSnippetsOption = new("--testsnippets")
    {
        Description = "Create short media file clips",
    };

    private readonly Option<bool> _preProcessOption = new("--preprocess")
    {
        Description = "Pre-process all monitored folders",
    };

    private readonly Option<List<string>> _mediaFilesOption = new("--mediafiles")
    {
        Description = "Path to media file or folder",
        Required = true,
    };

    private readonly Option<string> _settingsFileOption = new("--settingsfile")
    {
        Description = "Path to settings file",
        Required = true,
    };

    private readonly Option<bool> _parallelOption = new("--parallel")
    {
        Description = "Enable parallel file processing",
    };

    private readonly Option<int> _threadCountOption = new("--threadcount")
    {
        Description = "Number of threads for parallel file processing",
    };

    private readonly Option<bool> _quickScanOption = new("--quickscan")
    {
        Description = "Scan only part of the file",
    };

    private RootCommand CreateRootCommand()
    {
        // TODO: https://github.com/dotnet/command-line-api/issues/2597
#pragma warning disable IDE0028 // Simplify collection initialization
        RootCommand rootCommand = new(
            "Utility to optimize media files for Direct Play in Plex, Emby, Jellyfin, etc."
        );
#pragma warning restore IDE0028 // Simplify collection initialization

        // Global options
        rootCommand.Options.Add(_logFileOption);
        rootCommand.Options.Add(_logAppendOption);
        rootCommand.Options.Add(_logWarningOption);
        rootCommand.Options.Add(_debugOption);

        // Commands
        rootCommand.Subcommands.Add(
            new("defaultsettings")
            {
                Description = "Create JSON configuration file using default settings",
                Action = new CommandHandler(_ => Program.DefaultSettingsCommand()),
                Options = { _settingsFileOption },
            }
        );
        rootCommand.Subcommands.Add(
            new("checkfornewtools")
            {
                Description = "Check for new tool versions and download if newer",
                Action = new CommandHandler(_ => Program.CheckForNewToolsCommand()),
                Options = { _settingsFileOption },
            }
        );
        rootCommand.Subcommands.Add(
            new("process")
            {
                Description = "Process media files",
                Action = new CommandHandler(_ => Program.ProcessCommand()),
                Options =
                {
                    _settingsFileOption,
                    _mediaFilesOption,
                    _parallelOption,
                    _threadCountOption,
                    _quickScanOption,
                    _resultsFileOption,
                    _testSnippetsOption,
                },
            }
        );
        rootCommand.Subcommands.Add(
            new("monitor")
            {
                Description = "Monitor for file changes and process changed media files",
                Action = new CommandHandler(_ => Program.MonitorCommand()),
                Options =
                {
                    _settingsFileOption,
                    _mediaFilesOption,
                    _parallelOption,
                    _threadCountOption,
                    _quickScanOption,
                    _preProcessOption,
                },
            }
        );
        rootCommand.Subcommands.Add(
            new("remux")
            {
                Description = "Conditionally re-multiplex media files",
                Action = new CommandHandler(_ => Program.ReMuxCommand()),
                Options =
                {
                    _settingsFileOption,
                    _mediaFilesOption,
                    _parallelOption,
                    _threadCountOption,
                },
            }
        );
        rootCommand.Subcommands.Add(
            new("reencode")
            {
                Description = "Conditionally re-encode media files",
                Action = new CommandHandler(_ => Program.ReEncodeCommand()),
                Options =
                {
                    _settingsFileOption,
                    _mediaFilesOption,
                    _parallelOption,
                    _threadCountOption,
                },
            }
        );
        rootCommand.Subcommands.Add(
            new("deinterlace")
            {
                Description = "Conditionally de-interlace media files",
                Action = new CommandHandler(_ => Program.DeInterlaceCommand()),
                Options =
                {
                    _settingsFileOption,
                    _mediaFilesOption,
                    _parallelOption,
                    _threadCountOption,
                    _quickScanOption,
                },
            }
        );
        rootCommand.Subcommands.Add(
            new("removesubtitles")
            {
                Description = "Remove all subtitle tracks",
                Action = new CommandHandler(_ => Program.RemoveSubtitlesCommand()),
                Options =
                {
                    _settingsFileOption,
                    _mediaFilesOption,
                    _parallelOption,
                    _threadCountOption,
                },
            }
        );
        rootCommand.Subcommands.Add(
            new("removeclosedcaptions")
            {
                Description = "Remove all closed captions",
                Action = new CommandHandler(_ => Program.RemoveClosedCaptionsCommand()),
                Options =
                {
                    _settingsFileOption,
                    _mediaFilesOption,
                    _parallelOption,
                    _threadCountOption,
                    _quickScanOption,
                },
            }
        );
        rootCommand.Subcommands.Add(
            new("verify")
            {
                Description = "Verify media container and stream integrity",
                Action = new CommandHandler(_ => Program.VerifyCommand()),
                Options =
                {
                    _settingsFileOption,
                    _mediaFilesOption,
                    _parallelOption,
                    _threadCountOption,
                    _quickScanOption,
                },
            }
        );
        rootCommand.Subcommands.Add(
            new("createsidecar")
            {
                Description = "Create new sidecar files",
                Action = new CommandHandler(_ => Program.CreateSidecarCommand()),
                Options =
                {
                    _settingsFileOption,
                    _mediaFilesOption,
                    _parallelOption,
                    _threadCountOption,
                },
            }
        );
        rootCommand.Subcommands.Add(
            new("updatesidecar")
            {
                Description = "Create or update sidecar files",
                Action = new CommandHandler(_ => Program.UpdateSidecarCommand()),
                Options =
                {
                    _settingsFileOption,
                    _mediaFilesOption,
                    _parallelOption,
                    _threadCountOption,
                },
            }
        );
        rootCommand.Subcommands.Add(
            new("getsidecar")
            {
                Description = "Print sidecar file information",
                Action = new CommandHandler(_ => Program.GetSidecarCommand()),
                Options =
                {
                    _settingsFileOption,
                    _mediaFilesOption,
                    _parallelOption,
                    _threadCountOption,
                },
            }
        );
        rootCommand.Subcommands.Add(
            new("getmediainfo")
            {
                Description = "Print media file information",
                Action = new CommandHandler(_ => Program.GetMediaInfoCommand()),
                Options =
                {
                    _settingsFileOption,
                    _mediaFilesOption,
                    _parallelOption,
                    _threadCountOption,
                },
            }
        );
        rootCommand.Subcommands.Add(
            new("gettagmap")
            {
                Description = "Print media file attribute mappings",
                Action = new CommandHandler(_ => Program.GetTagMapCommand()),
                Options =
                {
                    _settingsFileOption,
                    _mediaFilesOption,
                    _parallelOption,
                    _threadCountOption,
                },
            }
        );
        rootCommand.Subcommands.Add(
            new("testmediainfo")
            {
                Description = "Test parsing media file information",
                Action = new CommandHandler(_ => Program.TestMediaInfoCommand()),
                Options =
                {
                    _settingsFileOption,
                    _mediaFilesOption,
                    _parallelOption,
                    _threadCountOption,
                },
            }
        );
        rootCommand.Subcommands.Add(
            new("gettoolinfo")
            {
                Description = "Print media tool information",
                Action = new CommandHandler(_ => Program.GetToolInfoCommand()),
                Options =
                {
                    _settingsFileOption,
                    _mediaFilesOption,
                    _parallelOption,
                    _threadCountOption,
                },
            }
        );
        rootCommand.Subcommands.Add(
            new("getversioninfo")
            {
                Description = "Print application and tools version information",
                Action = new CommandHandler(_ => Program.GetVersionInfoCommand()),
                Options = { _settingsFileOption },
            }
        );
        rootCommand.Subcommands.Add(
            new("createschema")
            {
                Description = "Write JSON settings schema to file",
                Action = new CommandHandler(_ => Program.CreateSchemaCommand()),
                Options = { _schemaFileOption },
            }
        );

        return rootCommand;
    }

    public CommandLineOptions Bind()
    {
        CommandLineOptions options = new()
        {
            Debug = Result.GetValue(_debugOption),
            LogAppend = Result.GetValue(_logAppendOption),
            LogWarning = Result.GetValue(_logWarningOption),
            Parallel = Result.GetValue(_parallelOption),
            PreProcess = Result.GetValue(_preProcessOption),
            QuickScan = Result.GetValue(_quickScanOption),
            TestSnippets = Result.GetValue(_testSnippetsOption),
            ThreadCount = Result.GetValue(_threadCountOption),
            MediaFiles = Result.GetValue(_mediaFilesOption) ?? [],
            LogFile = Result.GetValue(_logFileOption) ?? string.Empty,
            ResultsFile = Result.GetValue(_resultsFileOption) ?? string.Empty,
            SchemaFile = Result.GetValue(_schemaFileOption) ?? string.Empty,
            SettingsFile = Result.GetValue(_settingsFileOption) ?? string.Empty,
        };

        return options;
    }
}
