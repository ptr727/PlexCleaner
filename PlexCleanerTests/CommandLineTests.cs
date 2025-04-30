using System.CommandLine;
using System.CommandLine.Parsing;
using FluentAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class CommandLineTests(PlexCleanerFixture fixture)
{
    private readonly PlexCleanerFixture _fixture = fixture;

    // TODO: Figure out how to get the access to command arguments without a local delegate
    // https://github.com/dotnet/command-line-api/discussions/2552

    [Theory]
    [InlineData(
        "process --settingsfile=settings.json --mediafiles=/data/foo --mediafiles=/data/bar --testsnippets --parallel --threadcount=2 --quickscan --resultsfile=results.json --logfile=logfile.log --logappend --logwarning --debug",
        "process"
    )]
    [InlineData(
        "monitor --settingsfile=settings.json --mediafiles=/data/foo --mediafiles=/data/bar --parallel --threadcount=2 --quickscan --resultsfile=results.json --preprocess --logfile=logfile.log --logappend --logwarning --debug",
        "monitor"
    )]
    public void Parse_Commandline_Success(string commandline, string command)
    {
        RootCommand rootCommand = CommandLineOptions.CreateRootCommand();
        ParseResult parseResult = rootCommand.Parse(commandline);
        _ = parseResult.Errors.Should().BeEmpty();
        _ = parseResult.CommandResult.Command.Name.Should().Be(command);

        // TODO: How to get access to CommandLineOptions containing name binding options to test values?
        // TODO: How to test for --help and --version?
    }

    [Theory]
    [InlineData("process")]
    [InlineData("monitor")]
    public void Parse_Commandline_Fail(string commandline)
    {
        RootCommand rootCommand = CommandLineOptions.CreateRootCommand();
        ParseResult parseResult = rootCommand.Parse(commandline);
        _ = parseResult.Errors.Should().NotBeEmpty();
    }
}
