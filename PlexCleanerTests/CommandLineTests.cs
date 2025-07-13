using System.CommandLine;
using AwesomeAssertions;
using DotMake.CommandLine;
using PlexCleaner;
using Xunit;
using static PlexCleaner.CliRootCommand;

namespace PlexCleanerTests;

public class CommandLineTests(PlexCleanerFixture fixture)
{
    private readonly PlexCleanerFixture _fixture = fixture;

    [Theory]
    [InlineData(
        "removeclosedcaptions --settingsfile=settings.json --mediafiles=/data/foo --parallel --threadcount=2 --quickscan"
    )]
    public void Parse_Commandline_RemoveClosedCaptions(string commandline)
    {
        ParseResult parseResult = Cli.Parse<CliRootCommand>(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("removeclosedcaptions");

        RemoveClosedCaptionsCommand command = parseResult.Bind<RemoveClosedCaptionsCommand>();
        _ = command.Options.Should().NotBeNull();
        _ = command.Options.SettingsFile.Should().Be("settings.json");
        _ = command.Options.MediaFiles.Count.Should().Be(1);
        _ = command.Options.MediaFiles[0].Should().Be("/data/foo");
        _ = command.Options.Parallel.Should().BeTrue();
        _ = command.Options.ThreadCount.Should().Be(2);
        _ = command.Options.QuickScan.Should().BeTrue();
    }

    [Theory]
    [InlineData("gettoolinfo --settingsfile=settings.json --mediafiles=/data/foo")]
    public void Parse_Commandline_GetToolInfo(string commandline)
    {
        ParseResult parseResult = Cli.Parse<CliRootCommand>(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("gettoolinfo");

        GetToolInfoCommand command = parseResult.Bind<GetToolInfoCommand>();
        _ = command.Options.Should().NotBeNull();
        _ = command.Options.SettingsFile.Should().Be("settings.json");
        _ = command.Options.MediaFiles.Count.Should().Be(1);
        _ = command.Options.MediaFiles[0].Should().Be("/data/foo");
    }

    [Theory]
    [InlineData("getmediainfo --settingsfile=settings.json --mediafiles=/data/foo")]
    public void Parse_Commandline_GetMediaInfo(string commandline)
    {
        ParseResult parseResult = Cli.Parse<CliRootCommand>(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("getmediainfo");

        GetMediaInfoCommand command = parseResult.Bind<GetMediaInfoCommand>();
        _ = command.Options.Should().NotBeNull();
        _ = command.Options.SettingsFile.Should().Be("settings.json");
        _ = command.Options.MediaFiles.Count.Should().Be(1);
        _ = command.Options.MediaFiles[0].Should().Be("/data/foo");
    }

    [Theory]
    [InlineData("gettagmap --settingsfile=settings.json --mediafiles=/data/foo")]
    public void Parse_Commandline_GetTagMap(string commandline)
    {
        ParseResult parseResult = Cli.Parse<CliRootCommand>(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("gettagmap");

        GetTagMapCommand command = parseResult.Bind<GetTagMapCommand>();
        _ = command.Options.Should().NotBeNull();
        _ = command.Options.SettingsFile.Should().Be("settings.json");
        _ = command.Options.MediaFiles.Count.Should().Be(1);
        _ = command.Options.MediaFiles[0].Should().Be("/data/foo");
    }

    [Theory]
    [InlineData("updatesidecar --settingsfile=settings.json --mediafiles=/data/foo")]
    public void Parse_Commandline_UpdateSidecar(string commandline)
    {
        ParseResult parseResult = Cli.Parse<CliRootCommand>(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("updatesidecar");

        UpdateSidecarCommand command = parseResult.Bind<UpdateSidecarCommand>();
        _ = command.Options.Should().NotBeNull();
        _ = command.Options.SettingsFile.Should().Be("settings.json");
        _ = command.Options.MediaFiles.Count.Should().Be(1);
        _ = command.Options.MediaFiles[0].Should().Be("/data/foo");
    }

    [Theory]
    [InlineData("getsidecar --settingsfile=settings.json --mediafiles=/data/foo")]
    public void Parse_Commandline_GetSidecar(string commandline)
    {
        ParseResult parseResult = Cli.Parse<CliRootCommand>(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("getsidecar");

        GetSidecarCommand command = parseResult.Bind<GetSidecarCommand>();
        _ = command.Options.Should().NotBeNull();
        _ = command.Options.SettingsFile.Should().Be("settings.json");
        _ = command.Options.MediaFiles.Count.Should().Be(1);
        _ = command.Options.MediaFiles[0].Should().Be("/data/foo");
    }

    [Theory]
    [InlineData("createsidecar --settingsfile=settings.json --mediafiles=/data/foo")]
    public void Parse_Commandline_CreateSidecar(string commandline)
    {
        ParseResult parseResult = Cli.Parse<CliRootCommand>(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("createsidecar");

        CreateSidecarCommand command = parseResult.Bind<CreateSidecarCommand>();
        _ = command.Options.Should().NotBeNull();
        _ = command.Options.SettingsFile.Should().Be("settings.json");
        _ = command.Options.MediaFiles.Count.Should().Be(1);
        _ = command.Options.MediaFiles[0].Should().Be("/data/foo");
    }

    [Theory]
    [InlineData("verify --settingsfile=settings.json --mediafiles=/data/foo --quickscan")]
    public void Parse_Commandline_Verify(string commandline)
    {
        ParseResult parseResult = Cli.Parse<CliRootCommand>(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("verify");

        VerifyCommand command = parseResult.Bind<VerifyCommand>();
        _ = command.Options.Should().NotBeNull();
        _ = command.Options.SettingsFile.Should().Be("settings.json");
        _ = command.Options.MediaFiles.Count.Should().Be(1);
        _ = command.Options.MediaFiles[0].Should().Be("/data/foo");
        _ = command.Options.QuickScan.Should().BeTrue();
    }

    [Theory]
    [InlineData("deinterlace --settingsfile=settings.json --mediafiles=/data/foo --quickscan")]
    public void Parse_Commandline_DeInterlace(string commandline)
    {
        ParseResult parseResult = Cli.Parse<CliRootCommand>(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("deinterlace");

        DeInterlaceCommand command = parseResult.Bind<DeInterlaceCommand>();
        _ = command.Options.Should().NotBeNull();
        _ = command.Options.SettingsFile.Should().Be("settings.json");
        _ = command.Options.MediaFiles.Count.Should().Be(1);
        _ = command.Options.MediaFiles[0].Should().Be("/data/foo");
        _ = command.Options.QuickScan.Should().BeTrue();
    }

    [Theory]
    [InlineData("reencode --settingsfile=settings.json --mediafiles=/data/foo")]
    public void Parse_Commandline_ReEncode(string commandline)
    {
        ParseResult parseResult = Cli.Parse<CliRootCommand>(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("reencode");

        ReEncodeCommand command = parseResult.Bind<ReEncodeCommand>();
        _ = command.Options.Should().NotBeNull();
        _ = command.Options.SettingsFile.Should().Be("settings.json");
        _ = command.Options.MediaFiles.Count.Should().Be(1);
        _ = command.Options.MediaFiles[0].Should().Be("/data/foo");
    }

    [Theory]
    [InlineData("remux --settingsfile=settings.json --mediafiles=/data/foo")]
    public void Parse_Commandline_ReMux(string commandline)
    {
        ParseResult parseResult = Cli.Parse<CliRootCommand>(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("remux");

        ReMuxCommand command = parseResult.Bind<ReMuxCommand>();
        _ = command.Options.Should().NotBeNull();
        _ = command.Options.SettingsFile.Should().Be("settings.json");
        _ = command.Options.MediaFiles.Count.Should().Be(1);
        _ = command.Options.MediaFiles[0].Should().Be("/data/foo");
    }

    [Theory]
    [InlineData(
        "monitor --settingsfile=settings.json --mediafiles=/data/foo --mediafiles=/data/bar --parallel --threadcount=2 --quickscan --logfile=logfile.log --logappend --logwarning --debug --preprocess"
    )]
    public void Parse_Commandline_Monitor(string commandline)
    {
        ParseResult parseResult = Cli.Parse<CliRootCommand>(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("monitor");

        MonitorCommand command = parseResult.Bind<MonitorCommand>();
        _ = command.Options.Should().NotBeNull();
        _ = command.Options.SettingsFile.Should().Be("settings.json");
        _ = command.Options.MediaFiles.Should().NotBeNullOrEmpty();
        _ = command.Options.MediaFiles.Count.Should().Be(2);
        _ = command.Options.MediaFiles[0].Should().Be("/data/foo");
        _ = command.Options.MediaFiles[1].Should().Be("/data/bar");
        _ = command.Options.TestSnippets.Should().BeFalse();
        _ = command.Options.Parallel.Should().BeTrue();
        _ = command.Options.ThreadCount.Should().Be(2);
        _ = command.Options.QuickScan.Should().BeTrue();
        _ = command.Options.LogFile.Should().Be("logfile.log");
        _ = command.Options.LogAppend.Should().BeTrue();
        _ = command.Options.LogWarning.Should().BeTrue();
        _ = command.Options.Debug.Should().BeTrue();
        _ = command.Options.PreProcess.Should().BeTrue();
    }

    [Theory]
    [InlineData(
        "process --settingsfile=settings.json --mediafiles=/data/foo --mediafiles=/data/bar --testsnippets --parallel --threadcount=2 --quickscan --resultsfile=results.json --logfile=logfile.log --logappend --logwarning --debug"
    )]
    public void Parse_Commandline_Process(string commandline)
    {
        ParseResult parseResult = Cli.Parse<CliRootCommand>(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("process");

        ProcessCommand command = parseResult.Bind<ProcessCommand>();
        _ = command.Options.Should().NotBeNull();
        _ = command.Options.SettingsFile.Should().Be("settings.json");
        _ = command.Options.MediaFiles.Should().NotBeNullOrEmpty();
        _ = command.Options.MediaFiles.Count.Should().Be(2);
        _ = command.Options.MediaFiles[0].Should().Be("/data/foo");
        _ = command.Options.MediaFiles[1].Should().Be("/data/bar");
        _ = command.Options.TestSnippets.Should().BeTrue();
        _ = command.Options.Parallel.Should().BeTrue();
        _ = command.Options.ThreadCount.Should().Be(2);
        _ = command.Options.QuickScan.Should().BeTrue();
        _ = command.Options.ResultsFile.Should().Be("results.json");
        _ = command.Options.LogFile.Should().Be("logfile.log");
        _ = command.Options.LogAppend.Should().BeTrue();
        _ = command.Options.LogWarning.Should().BeTrue();
        _ = command.Options.Debug.Should().BeTrue();
    }

    [Theory]
    [InlineData("checkfornewtools --settingsfile=settings.json")]
    public void Parse_Commandline_CheckForNewTools(string commandline)
    {
        ParseResult parseResult = Cli.Parse<CliRootCommand>(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("checkfornewtools");

        CheckForNewToolsCommand command = parseResult.Bind<CheckForNewToolsCommand>();
        _ = command.Options.Should().NotBeNull();
        _ = command.Options.SettingsFile.Should().Be("settings.json");
    }

    [Theory]
    [InlineData("defaultsettings --settingsfile=settings.json")]
    public void Parse_Commandline_DefaultSettings(string commandline)
    {
        ParseResult parseResult = Cli.Parse<CliRootCommand>(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("defaultsettings");

        DefaultSettingsCommand command = parseResult.Bind<DefaultSettingsCommand>();
        _ = command.Options.Should().NotBeNull();
        _ = command.Options.SettingsFile.Should().Be("settings.json");
    }

    [Theory]
    [InlineData("createschema --schemafile=schema.json")]
    public void Parse_Commandline_CreateSchema(string commandline)
    {
        ParseResult parseResult = Cli.Parse<CliRootCommand>(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("createschema");

        CreateSchemaCommand command = parseResult.Bind<CreateSchemaCommand>();
        _ = command.Options.Should().NotBeNull();
        _ = command.Options.SchemaFile.Should().Be("schema.json");
    }

    [Theory]
    [InlineData("getversioninfo --settingsfile=settings.json")]
    public void Parse_Commandline_GetVersionInfo(string commandline)
    {
        ParseResult parseResult = Cli.Parse<CliRootCommand>(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("getversioninfo");

        GetVersionInfoCommand command = parseResult.Bind<GetVersionInfoCommand>();
        _ = command.Options.Should().NotBeNull();
        _ = command.Options.SettingsFile.Should().Be("settings.json");
    }

    [Theory]
    [InlineData("removesubtitles --settingsfile=settings.json --mediafiles=/data/foo")]
    public void Parse_Commandline_RemoveSubtitles(string commandline)
    {
        ParseResult parseResult = Cli.Parse<CliRootCommand>(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be("removesubtitles");

        RemoveSubtitlesCommand command = parseResult.Bind<RemoveSubtitlesCommand>();
        _ = command.Options.Should().NotBeNull();
        _ = command.Options.SettingsFile.Should().Be("settings.json");
        _ = command.Options.MediaFiles.Should().NotBeNullOrEmpty();
        _ = command.Options.MediaFiles.Count.Should().Be(1);
        _ = command.Options.MediaFiles[0].Should().Be("/data/foo");
    }
}
