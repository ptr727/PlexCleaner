using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public sealed class ToolErrorScopeTests
{
    [Fact]
    public async Task Record_OnPoolThread_ReachesScope()
    {
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();

        await Task.Run(ToolErrorScope.Record, TestContext.Current.CancellationToken);

        _ = ToolErrorScope.Count.Should().Be(1);
    }

    [Fact]
    public void Record_OutsideScope_IsIgnored()
    {
        ToolErrorScope.Record();

        _ = ToolErrorScope.Count.Should().Be(0);
    }

    [Fact]
    public void ProcessFiles_BeginsScopePerFile()
    {
        int threadCount = Program.Options.ThreadCount;
        Program.Options.ThreadCount = 1;
        try
        {
            List<int> counts = [];
            _ = ProcessDriver
                .ProcessFiles(
                    ["first.mkv", "second.mkv"],
                    nameof(ProcessFiles_BeginsScopePerFile),
                    false,
                    _ =>
                    {
                        ToolErrorScope.Record();
                        counts.Add(ToolErrorScope.Count);
                        return true;
                    }
                )
                .Should()
                .BeTrue();

            _ = counts.Should().Equal(1, 1);
            _ = ToolErrorScope.Count.Should().Be(0);
        }
        finally
        {
            Program.Options.ThreadCount = threadCount;
        }
    }
}
