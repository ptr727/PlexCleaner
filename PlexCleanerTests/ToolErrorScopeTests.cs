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
                    ["name.avi", "name.mkv"],
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
        }
        finally
        {
            Program.Options.ThreadCount = threadCount;
        }
    }

    [Fact]
    public async Task Record_ConcurrentScopes_AreIsolated()
    {
        using Barrier barrier = new(2);
        Task<int> recording = Task.Run(
            () =>
            {
                using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
                ToolErrorScope.Record();
                barrier.SignalAndWait(TestContext.Current.CancellationToken);
                return ToolErrorScope.Count;
            },
            TestContext.Current.CancellationToken
        );
        Task<int> idle = Task.Run(
            () =>
            {
                using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
                barrier.SignalAndWait(TestContext.Current.CancellationToken);
                return ToolErrorScope.Count;
            },
            TestContext.Current.CancellationToken
        );

        _ = (await recording).Should().Be(1);
        _ = (await idle).Should().Be(0);
    }
}
