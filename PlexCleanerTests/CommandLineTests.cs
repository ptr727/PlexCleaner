using AwesomeAssertions;
using PlexCleaner;
using Serilog.Events;
using Xunit;

namespace PlexCleanerTests;

public class CommandLineTests
{
    [Theory]
    [InlineData(
        "removeclosedcaptions",
        "--settingsfile=settings.json",
        "--mediafiles=/data/foo",
        "--parallel",
        "--threadcount=2",
        "--quickscan"
    )]
    public void Parse_Commandline_RemoveClosedCaptions(params string[] args)
    {
        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().BeEmpty();
        _ = parser.Result.CommandResult.Command.Name.Should().Be("removeclosedcaptions");

        CommandLineOptions options = parser.Bind();
        _ = options.Should().NotBeNull();
        _ = options.SettingsFile.Should().Be("settings.json");
        _ = options.MediaFiles.Count.Should().Be(1);
        _ = options.MediaFiles[0].Should().Be("/data/foo");
        _ = options.Parallel.Should().BeTrue();
        _ = options.ThreadCount.Should().Be(2);
        _ = options.QuickScan.Should().BeTrue();
    }

    [Theory]
    [InlineData("gettoolinfo", "--settingsfile=settings.json", "--mediafiles=/data/foo")]
    public void Parse_Commandline_GetToolInfo(params string[] args)
    {
        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().BeEmpty();
        _ = parser.Result.CommandResult.Command.Name.Should().Be("gettoolinfo");

        CommandLineOptions options = parser.Bind();
        _ = options.Should().NotBeNull();
        _ = options.SettingsFile.Should().Be("settings.json");
        _ = options.MediaFiles.Count.Should().Be(1);
        _ = options.MediaFiles[0].Should().Be("/data/foo");
    }

    [Theory]
    [InlineData("getmediainfo", "--settingsfile=settings.json", "--mediafiles=/data/foo")]
    public void Parse_Commandline_GetMediaInfo(params string[] args)
    {
        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().BeEmpty();
        _ = parser.Result.CommandResult.Command.Name.Should().Be("getmediainfo");

        CommandLineOptions options = parser.Bind();
        _ = options.Should().NotBeNull();
        _ = options.SettingsFile.Should().Be("settings.json");
        _ = options.MediaFiles.Count.Should().Be(1);
        _ = options.MediaFiles[0].Should().Be("/data/foo");
    }

    [Theory]
    [InlineData("gettagmap", "--settingsfile=settings.json", "--mediafiles=/data/foo")]
    public void Parse_Commandline_GetTagMap(params string[] args)
    {
        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().BeEmpty();
        _ = parser.Result.CommandResult.Command.Name.Should().Be("gettagmap");

        CommandLineOptions options = parser.Bind();
        _ = options.Should().NotBeNull();
        _ = options.SettingsFile.Should().Be("settings.json");
        _ = options.MediaFiles.Count.Should().Be(1);
        _ = options.MediaFiles[0].Should().Be("/data/foo");
    }

    [Theory]
    [InlineData("updatesidecar", "--settingsfile=settings.json", "--mediafiles=/data/foo")]
    public void Parse_Commandline_UpdateSidecar(params string[] args)
    {
        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().BeEmpty();
        _ = parser.Result.CommandResult.Command.Name.Should().Be("updatesidecar");

        CommandLineOptions options = parser.Bind();
        _ = options.Should().NotBeNull();
        _ = options.SettingsFile.Should().Be("settings.json");
        _ = options.MediaFiles.Count.Should().Be(1);
        _ = options.MediaFiles[0].Should().Be("/data/foo");
    }

    [Theory]
    [InlineData("getsidecarinfo", "--settingsfile=settings.json", "--mediafiles=/data/foo")]
    public void Parse_Commandline_GetSidecarInfo(params string[] args)
    {
        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().BeEmpty();
        _ = parser.Result.CommandResult.Command.Name.Should().Be("getsidecarinfo");

        CommandLineOptions options = parser.Bind();
        _ = options.Should().NotBeNull();
        _ = options.SettingsFile.Should().Be("settings.json");
        _ = options.MediaFiles.Count.Should().Be(1);
        _ = options.MediaFiles[0].Should().Be("/data/foo");
    }

    [Theory]
    [InlineData("createsidecar", "--settingsfile=settings.json", "--mediafiles=/data/foo")]
    public void Parse_Commandline_CreateSidecar(params string[] args)
    {
        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().BeEmpty();
        _ = parser.Result.CommandResult.Command.Name.Should().Be("createsidecar");

        CommandLineOptions options = parser.Bind();
        _ = options.Should().NotBeNull();
        _ = options.SettingsFile.Should().Be("settings.json");
        _ = options.MediaFiles.Count.Should().Be(1);
        _ = options.MediaFiles[0].Should().Be("/data/foo");
    }

    [Theory]
    [InlineData("verify", "--settingsfile=settings.json", "--mediafiles=/data/foo", "--quickscan")]
    public void Parse_Commandline_Verify(params string[] args)
    {
        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().BeEmpty();
        _ = parser.Result.CommandResult.Command.Name.Should().Be("verify");

        CommandLineOptions options = parser.Bind();
        _ = options.Should().NotBeNull();
        _ = options.SettingsFile.Should().Be("settings.json");
        _ = options.MediaFiles.Count.Should().Be(1);
        _ = options.MediaFiles[0].Should().Be("/data/foo");
        _ = options.QuickScan.Should().BeTrue();
    }

    [Theory]
    [InlineData(
        "deinterlace",
        "--settingsfile=settings.json",
        "--mediafiles=/data/foo",
        "--quickscan"
    )]
    public void Parse_Commandline_DeInterlace(params string[] args)
    {
        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().BeEmpty();
        _ = parser.Result.CommandResult.Command.Name.Should().Be("deinterlace");

        CommandLineOptions options = parser.Bind();
        _ = options.Should().NotBeNull();
        _ = options.SettingsFile.Should().Be("settings.json");
        _ = options.MediaFiles.Count.Should().Be(1);
        _ = options.MediaFiles[0].Should().Be("/data/foo");
        _ = options.QuickScan.Should().BeTrue();
    }

    [Theory]
    [InlineData("reencode", "--settingsfile=settings.json", "--mediafiles=/data/foo")]
    public void Parse_Commandline_ReEncode(params string[] args)
    {
        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().BeEmpty();
        _ = parser.Result.CommandResult.Command.Name.Should().Be("reencode");

        CommandLineOptions options = parser.Bind();
        _ = options.Should().NotBeNull();
        _ = options.SettingsFile.Should().Be("settings.json");
        _ = options.MediaFiles.Count.Should().Be(1);
        _ = options.MediaFiles[0].Should().Be("/data/foo");
    }

    [Theory]
    [InlineData("remux", "--settingsfile=settings.json", "--mediafiles=/data/foo")]
    public void Parse_Commandline_ReMux(params string[] args)
    {
        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().BeEmpty();
        _ = parser.Result.CommandResult.Command.Name.Should().Be("remux");

        CommandLineOptions options = parser.Bind();
        _ = options.Should().NotBeNull();
        _ = options.SettingsFile.Should().Be("settings.json");
        _ = options.MediaFiles.Count.Should().Be(1);
        _ = options.MediaFiles[0].Should().Be("/data/foo");
    }

    [Theory]
    [InlineData(
        "monitor",
        "--settingsfile=settings.json",
        "--mediafiles=/data/foo",
        "--mediafiles=/data/bar",
        "--parallel",
        "--threadcount=2",
        "--quickscan",
        "--loglevel=Debug",
        "--logfile=logfile.log",
        "--logelevate",
        "--logclear",
        "--debug",
        "--preprocess"
    )]
    public void Parse_Commandline_Monitor(params string[] args)
    {
        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().BeEmpty();
        _ = parser.Result.CommandResult.Command.Name.Should().Be("monitor");

        CommandLineOptions options = parser.Bind();
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
        _ = options.LogLevel.Should().Be(LogEventLevel.Debug);
        _ = options.LogFile!.Name.Should().Be("logfile.log");
        _ = options.LogElevate.Should().BeTrue();
        _ = options.LogClear.Should().BeTrue();
        _ = options.Debug.Should().BeTrue();
        _ = options.PreProcess.Should().BeTrue();
    }

    [Theory]
    [InlineData(
        "process",
        "--settingsfile=settings.json",
        "--mediafiles=/data/foo",
        "--mediafiles=/data/bar",
        "--testsnippets",
        "--parallel",
        "--threadcount=2",
        "--quickscan",
        "--resultsfile=results.json",
        "--logfile=logfile.log",
        "--logappend",
        "--logwarning",
        "--debug"
    )]
    public void Parse_Commandline_Process(params string[] args)
    {
        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().BeEmpty();
        _ = parser.Result.CommandResult.Command.Name.Should().Be("process");

        CommandLineOptions options = parser.Bind();
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
        _ = options.LogFile!.Name.Should().Be("logfile.log");
        // Deprecated flags still parse for backward compatibility
        _ = options.LogAppend.Should().BeTrue();
        _ = options.LogWarning.Should().BeTrue();
        // --loglevel omitted, so it defaults to Information
        _ = options.LogLevel.Should().Be(LogEventLevel.Information);
        _ = options.Debug.Should().BeTrue();
    }

    [Theory]
    [InlineData("checkfornewtools", "--settingsfile=settings.json")]
    public void Parse_Commandline_CheckForNewTools(params string[] args)
    {
        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().BeEmpty();
        _ = parser.Result.CommandResult.Command.Name.Should().Be("checkfornewtools");

        CommandLineOptions options = parser.Bind();
        _ = options.Should().NotBeNull();
        _ = options.SettingsFile.Should().Be("settings.json");
    }

    [Theory]
    [InlineData("defaultsettings", "--settingsfile=settings.json")]
    public void Parse_Commandline_DefaultSettings(params string[] args)
    {
        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().BeEmpty();
        _ = parser.Result.CommandResult.Command.Name.Should().Be("defaultsettings");

        CommandLineOptions options = parser.Bind();
        _ = options.Should().NotBeNull();
        _ = options.SettingsFile.Should().Be("settings.json");
    }

    [Theory]
    [InlineData("createschema", "--schemafile=schema.json")]
    public void Parse_Commandline_CreateSchema(params string[] args)
    {
        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().BeEmpty();
        _ = parser.Result.CommandResult.Command.Name.Should().Be("createschema");

        CommandLineOptions options = parser.Bind();
        _ = options.Should().NotBeNull();
        _ = options.SchemaFile.Should().Be("schema.json");
    }

    [Theory]
    [InlineData("getversioninfo", "--settingsfile=settings.json")]
    public void Parse_Commandline_GetVersionInfo(params string[] args)
    {
        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().BeEmpty();
        _ = parser.Result.CommandResult.Command.Name.Should().Be("getversioninfo");

        CommandLineOptions options = parser.Bind();
        _ = options.Should().NotBeNull();
        _ = options.SettingsFile.Should().Be("settings.json");
    }

    [Theory]
    [InlineData("removesubtitles", "--settingsfile=settings.json", "--mediafiles=/data/foo")]
    public void Parse_Commandline_RemoveSubtitles(params string[] args)
    {
        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().BeEmpty();
        _ = parser.Result.CommandResult.Command.Name.Should().Be("removesubtitles");

        CommandLineOptions options = parser.Bind();
        _ = options.Should().NotBeNull();
        _ = options.SettingsFile.Should().Be("settings.json");
        _ = options.MediaFiles.Should().NotBeNullOrEmpty();
        _ = options.MediaFiles.Count.Should().Be(1);
        _ = options.MediaFiles[0].Should().Be("/data/foo");
    }

    [Theory]
    [InlineData(
        "removeclosedcaptions",
        "--settingsfile=settings.json",
        "--mediafiles=/data/foo",
        "--parallel",
        "--threadcount=4",
        "--quickscan"
    )]
    public void Parse_Commandline_RemoveClosedCaptions_Full(params string[] args)
    {
        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().BeEmpty();
        _ = parser.Result.CommandResult.Command.Name.Should().Be("removeclosedcaptions");

        CommandLineOptions options = parser.Bind();
        _ = options.Should().NotBeNull();
        _ = options.SettingsFile.Should().Be("settings.json");
        _ = options.MediaFiles.Count.Should().Be(1);
        _ = options.MediaFiles[0].Should().Be("/data/foo");
        _ = options.Parallel.Should().BeTrue();
        _ = options.ThreadCount.Should().Be(4);
        _ = options.QuickScan.Should().BeTrue();
    }

    [Theory]
    [InlineData(
        "testmediainfo",
        "--settingsfile=settings.json",
        "--mediafiles=/data/foo",
        "--parallel",
        "--threadcount=2"
    )]
    public void Parse_Commandline_TestMediaInfo(params string[] args)
    {
        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().BeEmpty();
        _ = parser.Result.CommandResult.Command.Name.Should().Be("testmediainfo");

        CommandLineOptions options = parser.Bind();
        _ = options.Should().NotBeNull();
        _ = options.SettingsFile.Should().Be("settings.json");
        _ = options.MediaFiles.Count.Should().Be(1);
        _ = options.MediaFiles[0].Should().Be("/data/foo");
        _ = options.Parallel.Should().BeTrue();
        _ = options.ThreadCount.Should().Be(2);
    }

    [Fact]
    public void Parse_Commandline_Custom_ExistingPluginAssembly_Binds()
    {
        // AcceptExistingOnly validates the path at parse time, so use a file that exists
        string existing = typeof(CommandLineTests).Assembly.Location;
        string[] args =
        [
            "custom",
            "--settingsfile=settings.json",
            $"--pluginassembly={existing}",
            "--mediafiles=/data/foo",
        ];

        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().BeEmpty();
        _ = parser.Result.CommandResult.Command.Name.Should().Be("custom");

        CommandLineOptions options = parser.Bind();
        FileInfo? pluginAssembly = options.PluginAssembly;
        _ = pluginAssembly.Should().NotBeNull();
        _ = pluginAssembly.FullName.Should().Be(new FileInfo(existing).FullName);
    }

    [Fact]
    public void Parse_Commandline_Custom_MissingPluginAssembly_Fails()
    {
        // A non-existent plugin path fails at parse time via AcceptExistingOnly
        string[] args =
        [
            "custom",
            "--settingsfile=settings.json",
            "--pluginassembly=/does/not/exist.dll",
            "--mediafiles=/data/foo",
        ];

        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("--version")]
    [InlineData("defaultsettings", "--help")]
    [InlineData("checkfornewtools", "--help")]
    [InlineData("process", "--help")]
    [InlineData("remux", "--help")]
    [InlineData("reencode", "--help")]
    [InlineData("deinterlace", "--help")]
    [InlineData("removesubtitles", "--help")]
    [InlineData("removeclosedcaptions", "--help")]
    [InlineData("verify", "--help")]
    [InlineData("createsidecar", "--help")]
    [InlineData("updatesidecar", "--help")]
    [InlineData("getsidecarinfo", "--help")]
    [InlineData("getmediainfo", "--help")]
    [InlineData("gettagmap", "--help")]
    [InlineData("testmediainfo", "--help")]
    [InlineData("gettoolinfo", "--help")]
    [InlineData("getversioninfo", "--help")]
    [InlineData("createschema", "--help")]
    public void Parse_Commandline_Help(params string[] args)
    {
        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData]
    [InlineData("--foo")]
    [InlineData("foo")]
    [InlineData("defaultsettings", "--settingsfile=settings.json", "--foo")]
    [InlineData("defaultsettings")]
    [InlineData("checkfornewtools")]
    [InlineData("process")]
    [InlineData("remux")]
    [InlineData("reencode")]
    [InlineData("deinterlace")]
    [InlineData("removesubtitles")]
    [InlineData("removeclosedcaptions")]
    [InlineData("verify")]
    [InlineData("createsidecar")]
    [InlineData("updatesidecar")]
    [InlineData("getsidecarinfo")]
    [InlineData("getmediainfo")]
    [InlineData("gettagmap")]
    [InlineData("testmediainfo")]
    [InlineData("gettoolinfo")]
    [InlineData("getversioninfo")]
    [InlineData("createschema")]
    public void Parse_Commandline_Fail(params string[] args)
    {
        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(
        "process",
        "--settingsfile=settings.json",
        "--mediafiles=/data/foo",
        "--loglevel=bogus"
    )]
    public void Parse_Commandline_InvalidLogLevel_Fails(params string[] args)
    {
        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("process", "--settingsfile=settings.json", "--mediafiles=/data/foo")]
    public void Parse_Commandline_DefaultLogLevel_IsInformation(params string[] args)
    {
        CommandLineParser parser = new(args);
        _ = parser.Result.Errors.Should().BeEmpty();

        CommandLineOptions options = parser.Bind();
        _ = options.LogLevel.Should().Be(LogEventLevel.Information);
        _ = options.LogElevate.Should().BeFalse();
        _ = options.LogClear.Should().BeFalse();
        _ = options.LogFile.Should().BeNull();
    }

    [Fact]
    public void FromCommandLine_LogWarning_MapsToWarningLevelWithoutElevation()
    {
        // Deprecated --logwarning maps to the Warning level only; it does not enable elevation
        CommandLineOptions options = new() { LogWarning = true };
        LoggerFactory.Options logOptions = LoggerFactory.FromCommandLine(options);
        _ = logOptions.Level.Should().Be(LogEventLevel.Warning);
        _ = logOptions.Elevate.Should().BeFalse();
    }

    [Fact]
    public void EffectiveLevel_LogWarning_IsWarningRegardlessOfLogLevel()
    {
        // The filter (FromCommandLine) and the per-file session scope (Process.cs) must agree on the
        // level, so the deprecated --logwarning maps to Warning through the shared EffectiveLevel helper
        CommandLineOptions options = new()
        {
            LogWarning = true,
            LogLevel = LogEventLevel.Information,
        };
        _ = LoggerFactory.EffectiveLevel(options).Should().Be(LogEventLevel.Warning);
        _ = LoggerFactory.FromCommandLine(options).Level.Should().Be(LogEventLevel.Warning);
    }

    [Fact]
    public void EffectiveLevel_NoLogWarning_UsesLogLevel()
    {
        CommandLineOptions options = new() { LogLevel = LogEventLevel.Debug };
        _ = LoggerFactory.EffectiveLevel(options).Should().Be(LogEventLevel.Debug);
    }

    [Fact]
    public void FromCommandLine_LogLevelAndElevate_MapThrough()
    {
        CommandLineOptions options = new()
        {
            LogLevel = LogEventLevel.Debug,
            LogElevate = true,
            LogClear = true,
        };
        LoggerFactory.Options logOptions = LoggerFactory.FromCommandLine(options);
        _ = logOptions.Level.Should().Be(LogEventLevel.Debug);
        _ = logOptions.Elevate.Should().BeTrue();
        _ = logOptions.FileClear.Should().BeTrue();
    }
}
