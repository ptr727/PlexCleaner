using System.CommandLine;
using System.CommandLine.Parsing;
using FluentAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class CommandLineTests(PlexCleanerFixture fixture)
{
    private readonly PlexCleanerFixture _fixture = fixture;

    // TODO: Figure out how to get the access to command arguments calling Parse() without a local delegate
    // https://github.com/dotnet/command-line-api/discussions/2552

    [Theory]
    [InlineData(
        "removeclosedcaptions --settingsfile=settings.json --mediafiles=/data/foo --parallel --threadcount=2 --quickscan"
    )]
    public void Parse_Commandline_RemoveClosedCaptions(string commandline)
    {
        bool didRun = false;
        int RemoveClosedCaptionsFunc(CommandLineOptions options)
        {
            _ = options.Should().NotBeNull();
            _ = options.SettingsFile.Should().Be("settings.json");
            _ = options.MediaFiles.Count.Should().Be(1);
            _ = options.MediaFiles[0].Should().Be("/data/foo");
            _ = options.Parallel.Should().BeTrue();
            _ = options.ThreadCount.Should().Be(2);
            _ = options.QuickScan.Should().BeTrue();
            didRun = true;
            return 0;
        }
        CommandLineOptions.s_removeClosedCaptionsFunc = RemoveClosedCaptionsFunc;

        RootCommand rootCommand = CommandLineOptions.CreateRootCommand();
        ParseResult parseResult = rootCommand.Parse(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("removeclosedcaptions");
        _ = parseResult.Invoke().Should().Be(0);
        _ = didRun.Should().BeTrue();
    }

    [Theory]
    [InlineData("gettoolinfo --settingsfile=settings.json --mediafiles=/data/foo")]
    public void Parse_Commandline_GetToolInfo(string commandline)
    {
        bool didRun = false;
        int GetToolInfoFunc(CommandLineOptions options)
        {
            _ = options.Should().NotBeNull();
            _ = options.SettingsFile.Should().Be("settings.json");
            _ = options.MediaFiles.Count.Should().Be(1);
            _ = options.MediaFiles[0].Should().Be("/data/foo");
            didRun = true;
            return 0;
        }
        CommandLineOptions.s_getToolInfoFunc = GetToolInfoFunc;

        RootCommand rootCommand = CommandLineOptions.CreateRootCommand();
        ParseResult parseResult = rootCommand.Parse(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("gettoolinfo");
        _ = parseResult.Invoke().Should().Be(0);
        _ = didRun.Should().BeTrue();
    }

    [Theory]
    [InlineData("getmediainfo --settingsfile=settings.json --mediafiles=/data/foo")]
    public void Parse_Commandline_GetMediaInfo(string commandline)
    {
        bool didRun = false;
        int GetMediaInfoFunc(CommandLineOptions options)
        {
            _ = options.Should().NotBeNull();
            _ = options.SettingsFile.Should().Be("settings.json");
            _ = options.MediaFiles.Count.Should().Be(1);
            _ = options.MediaFiles[0].Should().Be("/data/foo");
            didRun = true;
            return 0;
        }
        CommandLineOptions.s_getMediaInfoFunc = GetMediaInfoFunc;

        RootCommand rootCommand = CommandLineOptions.CreateRootCommand();
        ParseResult parseResult = rootCommand.Parse(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("getmediainfo");
        _ = parseResult.Invoke().Should().Be(0);
        _ = didRun.Should().BeTrue();
    }

    [Theory]
    [InlineData("gettagmap --settingsfile=settings.json --mediafiles=/data/foo")]
    public void Parse_Commandline_GetTagMap(string commandline)
    {
        bool didRun = false;
        int GetTagMapFunc(CommandLineOptions options)
        {
            _ = options.Should().NotBeNull();
            _ = options.SettingsFile.Should().Be("settings.json");
            _ = options.MediaFiles.Count.Should().Be(1);
            _ = options.MediaFiles[0].Should().Be("/data/foo");
            didRun = true;
            return 0;
        }
        CommandLineOptions.s_getTagMapFunc = GetTagMapFunc;

        RootCommand rootCommand = CommandLineOptions.CreateRootCommand();
        ParseResult parseResult = rootCommand.Parse(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("gettagmap");
        _ = parseResult.Invoke().Should().Be(0);
        _ = didRun.Should().BeTrue();
    }

    [Theory]
    [InlineData("updatesidecar --settingsfile=settings.json --mediafiles=/data/foo")]
    public void Parse_Commandline_UpdateSidecar(string commandline)
    {
        bool didRun = false;
        int UpdateSidecarFunc(CommandLineOptions options)
        {
            _ = options.Should().NotBeNull();
            _ = options.SettingsFile.Should().Be("settings.json");
            _ = options.MediaFiles.Count.Should().Be(1);
            _ = options.MediaFiles[0].Should().Be("/data/foo");
            didRun = true;
            return 0;
        }
        CommandLineOptions.s_updateSidecarFunc = UpdateSidecarFunc;

        RootCommand rootCommand = CommandLineOptions.CreateRootCommand();
        ParseResult parseResult = rootCommand.Parse(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("updatesidecar");
        _ = parseResult.Invoke().Should().Be(0);
        _ = didRun.Should().BeTrue();
    }

    [Theory]
    [InlineData("getsidecarinfo --settingsfile=settings.json --mediafiles=/data/foo")]
    public void Parse_Commandline_GetSidecarInfo(string commandline)
    {
        bool didRun = false;
        int GetSidecarInfoFunc(CommandLineOptions options)
        {
            _ = options.Should().NotBeNull();
            _ = options.SettingsFile.Should().Be("settings.json");
            _ = options.MediaFiles.Count.Should().Be(1);
            _ = options.MediaFiles[0].Should().Be("/data/foo");
            didRun = true;
            return 0;
        }
        CommandLineOptions.s_getSidecarInfoFunc = GetSidecarInfoFunc;

        RootCommand rootCommand = CommandLineOptions.CreateRootCommand();
        ParseResult parseResult = rootCommand.Parse(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("getsidecarinfo");
        _ = parseResult.Invoke().Should().Be(0);
        _ = didRun.Should().BeTrue();
    }

    [Theory]
    [InlineData("createsidecar --settingsfile=settings.json --mediafiles=/data/foo")]
    public void Parse_Commandline_CreateSidecar(string commandline)
    {
        bool didRun = false;
        int CreateSidecarFunc(CommandLineOptions options)
        {
            _ = options.Should().NotBeNull();
            _ = options.SettingsFile.Should().Be("settings.json");
            _ = options.MediaFiles.Count.Should().Be(1);
            _ = options.MediaFiles[0].Should().Be("/data/foo");
            didRun = true;
            return 0;
        }
        CommandLineOptions.s_createSidecarFunc = CreateSidecarFunc;

        RootCommand rootCommand = CommandLineOptions.CreateRootCommand();
        ParseResult parseResult = rootCommand.Parse(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("createsidecar");
        _ = parseResult.Invoke().Should().Be(0);
        _ = didRun.Should().BeTrue();
    }

    [Theory]
    [InlineData("verify --settingsfile=settings.json --mediafiles=/data/foo --quickscan")]
    public void Parse_Commandline_Verify(string commandline)
    {
        bool didRun = false;
        int VerifyFunc(CommandLineOptions options)
        {
            _ = options.Should().NotBeNull();
            _ = options.SettingsFile.Should().Be("settings.json");
            _ = options.MediaFiles.Count.Should().Be(1);
            _ = options.MediaFiles[0].Should().Be("/data/foo");
            _ = options.QuickScan.Should().BeTrue();
            didRun = true;
            return 0;
        }
        CommandLineOptions.s_verifyFunc = VerifyFunc;

        RootCommand rootCommand = CommandLineOptions.CreateRootCommand();
        ParseResult parseResult = rootCommand.Parse(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("verify");
        _ = parseResult.Invoke().Should().Be(0);
        _ = didRun.Should().BeTrue();
    }

    [Theory]
    [InlineData("deinterlace --settingsfile=settings.json --mediafiles=/data/foo --quickscan")]
    public void Parse_Commandline_DeInterlace(string commandline)
    {
        bool didRun = false;
        int DeInterlaceFunc(CommandLineOptions options)
        {
            _ = options.Should().NotBeNull();
            _ = options.SettingsFile.Should().Be("settings.json");
            _ = options.MediaFiles.Count.Should().Be(1);
            _ = options.MediaFiles[0].Should().Be("/data/foo");
            _ = options.QuickScan.Should().BeTrue();
            didRun = true;
            return 0;
        }
        CommandLineOptions.s_deInterlaceFunc = DeInterlaceFunc;

        RootCommand rootCommand = CommandLineOptions.CreateRootCommand();
        ParseResult parseResult = rootCommand.Parse(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("deinterlace");
        _ = parseResult.Invoke().Should().Be(0);
        _ = didRun.Should().BeTrue();
    }

    [Theory]
    [InlineData("reencode --settingsfile=settings.json --mediafiles=/data/foo")]
    public void Parse_Commandline_ReEncode(string commandline)
    {
        bool didRun = false;
        int ReEncodeFunc(CommandLineOptions options)
        {
            _ = options.Should().NotBeNull();
            _ = options.SettingsFile.Should().Be("settings.json");
            _ = options.MediaFiles.Count.Should().Be(1);
            _ = options.MediaFiles[0].Should().Be("/data/foo");
            didRun = true;
            return 0;
        }
        CommandLineOptions.s_reEncodeFunc = ReEncodeFunc;

        RootCommand rootCommand = CommandLineOptions.CreateRootCommand();
        ParseResult parseResult = rootCommand.Parse(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("reencode");
        _ = parseResult.Invoke().Should().Be(0);
        _ = didRun.Should().BeTrue();
    }

    [Theory]
    [InlineData("remux --settingsfile=settings.json --mediafiles=/data/foo")]
    public void Parse_Commandline_ReMux(string commandline)
    {
        bool didRun = false;
        int ReMuxFunc(CommandLineOptions options)
        {
            _ = options.Should().NotBeNull();
            _ = options.SettingsFile.Should().Be("settings.json");
            _ = options.MediaFiles.Count.Should().Be(1);
            _ = options.MediaFiles[0].Should().Be("/data/foo");
            didRun = true;
            return 0;
        }
        CommandLineOptions.s_reMuxFunc = ReMuxFunc;

        RootCommand rootCommand = CommandLineOptions.CreateRootCommand();
        ParseResult parseResult = rootCommand.Parse(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("remux");
        _ = parseResult.Invoke().Should().Be(0);
        _ = didRun.Should().BeTrue();
    }

    [Theory]
    [InlineData(
        "monitor --settingsfile=settings.json --mediafiles=/data/foo --mediafiles=/data/bar --parallel --threadcount=2 --quickscan --logfile=logfile.log --logappend --logwarning --debug --preprocess"
    )]
    public void Parse_Commandline_Monitor(string commandline)
    {
        bool didRun = false;
        int MonitorFunc(CommandLineOptions options)
        {
            _ = options.Should().NotBeNull();
            _ = options.SettingsFile.Should().Be("settings.json");
            _ = options.MediaFiles.Should().NotBeNullOrEmpty();
            _ = options.MediaFiles.Count.Should().Be(2);
            _ = options.MediaFiles[0].Should().Be("/data/foo");
            _ = options.MediaFiles[1].Should().Be("/data/bar");
            _ = options.TestSnippets.Should().BeFalse();
            _ = options.Parallel.Should().BeTrue();
            _ = options.ThreadCount.Should().Be(2);
            _ = options.QuickScan.Should().BeTrue();
            _ = options.LogFile.Should().Be("logfile.log");
            _ = options.LogAppend.Should().BeTrue();
            _ = options.LogWarning.Should().BeTrue();
            _ = options.Debug.Should().BeTrue();
            _ = options.PreProcess.Should().BeTrue();
            didRun = true;
            return 0;
        }
        CommandLineOptions.s_monitorFunc = MonitorFunc;

        RootCommand rootCommand = CommandLineOptions.CreateRootCommand();
        ParseResult parseResult = rootCommand.Parse(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("monitor");
        _ = parseResult.Invoke().Should().Be(0);
        _ = didRun.Should().BeTrue();
    }

    [Theory]
    [InlineData(
        "process --settingsfile=settings.json --mediafiles=/data/foo --mediafiles=/data/bar --testsnippets --parallel --threadcount=2 --quickscan --resultsfile=results.json --logfile=logfile.log --logappend --logwarning --debug"
    )]
    public void Parse_Commandline_Process(string commandline)
    {
        bool didRun = false;
        int ProcessFunc(CommandLineOptions options)
        {
            _ = options.Should().NotBeNull();
            _ = options.SettingsFile.Should().Be("settings.json");
            _ = options.MediaFiles.Should().NotBeNullOrEmpty();
            _ = options.MediaFiles.Count.Should().Be(2);
            _ = options.MediaFiles[0].Should().Be("/data/foo");
            _ = options.MediaFiles[1].Should().Be("/data/bar");
            _ = options.TestSnippets.Should().BeTrue();
            _ = options.Parallel.Should().BeTrue();
            _ = options.ThreadCount.Should().Be(2);
            _ = options.QuickScan.Should().BeTrue();
            _ = options.ResultsFile.Should().Be("results.json");
            _ = options.LogFile.Should().Be("logfile.log");
            _ = options.LogAppend.Should().BeTrue();
            _ = options.LogWarning.Should().BeTrue();
            _ = options.Debug.Should().BeTrue();
            didRun = true;
            return 0;
        }
        CommandLineOptions.s_processFunc = ProcessFunc;

        RootCommand rootCommand = CommandLineOptions.CreateRootCommand();
        ParseResult parseResult = rootCommand.Parse(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("process");
        _ = parseResult.Invoke().Should().Be(0);
        _ = didRun.Should().BeTrue();
    }

    [Theory]
    [InlineData("checkfornewtools --settingsfile=settings.json")]
    public void Parse_Commandline_CheckForNewTools(string commandline)
    {
        bool didRun = false;
        int CheckForNewToolsFunc(CommandLineOptions options)
        {
            _ = options.Should().NotBeNull();
            _ = options.SettingsFile.Should().Be("settings.json");
            didRun = true;
            return 0;
        }
        CommandLineOptions.s_checkForNewToolsFunc = CheckForNewToolsFunc;

        RootCommand rootCommand = CommandLineOptions.CreateRootCommand();
        ParseResult parseResult = rootCommand.Parse(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("checkfornewtools");
        _ = parseResult.Invoke().Should().Be(0);
        _ = didRun.Should().BeTrue();
    }

    [Theory]
    [InlineData("defaultsettings --settingsfile=settings.json")]
    public void Parse_Commandline_DefaultSettings(string commandline)
    {
        bool didRun = false;
        int DefaultSettingsFunc(CommandLineOptions options)
        {
            _ = options.Should().NotBeNull();
            _ = options.SettingsFile.Should().Be("settings.json");
            didRun = true;
            return 0;
        }
        CommandLineOptions.s_defaultSettingsFunc = DefaultSettingsFunc;

        RootCommand rootCommand = CommandLineOptions.CreateRootCommand();
        ParseResult parseResult = rootCommand.Parse(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("defaultsettings");
        _ = parseResult.Invoke().Should().Be(0);
        _ = didRun.Should().BeTrue();
    }

    [Theory]
    [InlineData("createschema --schemafile=schema.json")]
    public void Parse_Commandline_CreateSchema(string commandline)
    {
        bool didRun = false;
        int CreateSchemaFunc(CommandLineOptions options)
        {
            _ = options.Should().NotBeNull();
            _ = options.SchemaFile.Should().Be("schema.json");
            didRun = true;
            return 0;
        }
        CommandLineOptions.s_createSchemaFunc = CreateSchemaFunc;

        RootCommand rootCommand = CommandLineOptions.CreateRootCommand();
        ParseResult parseResult = rootCommand.Parse(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("createschema");
        _ = parseResult.Invoke().Should().Be(0);
        _ = didRun.Should().BeTrue();
    }

    [Theory]
    [InlineData("getversioninfo --settingsfile=settings.json")]
    public void Parse_Commandline_GetVersionInfo(string commandline)
    {
        bool didRun = false;
        int GetVersionInfoFunc(CommandLineOptions options)
        {
            _ = options.Should().NotBeNull();
            _ = options.SettingsFile.Should().Be("settings.json");
            didRun = true;
            return 0;
        }
        CommandLineOptions.s_getVersionInfoFunc = GetVersionInfoFunc;

        RootCommand rootCommand = CommandLineOptions.CreateRootCommand();
        ParseResult parseResult = rootCommand.Parse(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("getversioninfo");
        _ = parseResult.Invoke().Should().Be(0);
        _ = didRun.Should().BeTrue();
    }

    [Theory]
    [InlineData("removesubtitles --settingsfile=settings.json --mediafiles=/data/foo")]
    public void Parse_Commandline_RemoveSubtitles(string commandline)
    {
        bool didRun = false;
        int RemoveSubtitlesFunc(CommandLineOptions options)
        {
            _ = options.Should().NotBeNull();
            _ = options.SettingsFile.Should().Be("settings.json");
            _ = options.MediaFiles.Should().NotBeNullOrEmpty();
            _ = options.MediaFiles.Count.Should().Be(1);
            _ = options.MediaFiles[0].Should().Be("/data/foo");
            didRun = true;
            return 0;
        }
        CommandLineOptions.s_removeSubtitlesFunc = RemoveSubtitlesFunc;

        RootCommand rootCommand = CommandLineOptions.CreateRootCommand();
        ParseResult parseResult = rootCommand.Parse(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("removesubtitles");
        _ = parseResult.Invoke().Should().Be(0);
        _ = didRun.Should().BeTrue();
    }
}
