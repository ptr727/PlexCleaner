using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;

namespace PlexCleaner;

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

    public ParseResult Result { get; init; }
    public bool BypassStartup =>
        Result.Errors.Count > 0
        || Result.CommandResult.Children.Any(symbolResult =>
            symbolResult is OptionResult optionResult
            && s_cliBypassList.Contains(optionResult.Option.Name, StringComparer.OrdinalIgnoreCase)
        );

    private static readonly List<string> s_cliBypassList = ["--help", "--version"];
    private RootCommand Root { get; init; }

    private class CommandHandler(Func<ParseResult, int> action) : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult) => action(parseResult);
    }

    private readonly Option<string> _logFileOption = new("--logfile")
    {
        Description = "Path to log file",
        HelpName = "filepath",
        Recursive = true,
    };

    private readonly Option<bool> _logAppendOption = new("--logappend")
    {
        Description = "Append to existing log file",
        HelpName = "boolean",
        Recursive = true,
    };

    private readonly Option<bool> _logWarningOption = new("--logwarning")
    {
        Description = "Log warnings and errors only",
        HelpName = "boolean",
        Recursive = true,
    };

    private readonly Option<bool> _debugOption = new("--debug")
    {
        Description = "Wait for debugger to attach",
        HelpName = "boolean",
        Recursive = true,
    };

    private readonly Option<string> _schemaFileOption = new("--schemafile")
    {
        Description = "Path to schema file",
        HelpName = "filepath",
        Required = true,
    };

    private readonly Option<string> _resultsFileOption = new("--resultsfile")
    {
        Description = "Path to results file",
        HelpName = "filepath",
    };

    private readonly Option<bool> _testSnippetsOption = new("--testsnippets")
    {
        Description = "Create short media file clips",
        HelpName = "boolean",
    };

    private readonly Option<bool> _preProcessOption = new("--preprocess")
    {
        Description = "Pre-process all monitored folders",
        HelpName = "boolean",
    };

    private readonly Option<List<string>> _mediaFilesOption = new("--mediafiles")
    {
        Description = "Path to media file or folder",
        HelpName = "filepath",
        Required = true,
    };

    private readonly Option<string> _settingsFileOption = new("--settingsfile")
    {
        Description = "Path to settings file",
        HelpName = "filepath",
        Required = true,
    };

    private readonly Option<bool> _parallelOption = new("--parallel")
    {
        Description = "Enable parallel file processing",
        HelpName = "boolean",
    };

    private readonly Option<int> _threadCountOption = new("--threadcount")
    {
        Description = "Number of threads for parallel file processing",
        HelpName = "integer",
    };

    private readonly Option<bool> _quickScanOption = new("--quickscan")
    {
        Description = "Scan only part of the file",
        HelpName = "boolean",
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
                Description = "Create default JSON settings file",
                Action = new CommandHandler(_ => Program.DefaultSettingsCommand()),
                Options = { _settingsFileOption },
            }
        );
        rootCommand.Subcommands.Add(
            new("checkfornewtools")
            {
                Description = "Check for and download new tool versions",
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
                Description = "Monitor file changes and process changed files",
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
                Description = "Remove all closed caption tracks",
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
            new("getsidecarinfo")
            {
                Description = "Print media sidecar information",
                Action = new CommandHandler(_ => Program.GetSidecarInfoCommand()),
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
            new("gettagmap")
            {
                Description = "Print media tool attribute mappings",
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
                Description = "Test parsing media tool information",
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
            new("getversioninfo")
            {
                Description = "Print application and media tool version information",
                Action = new CommandHandler(_ => Program.GetVersionInfoCommand()),
                Options = { _settingsFileOption },
            }
        );
        rootCommand.Subcommands.Add(
            new("createschema")
            {
                Description = "Create JSON settings schema file",
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
