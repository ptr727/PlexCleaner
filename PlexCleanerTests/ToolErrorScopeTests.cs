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
    public void RecordSignal_LeavesCountUnchanged()
    {
        using IDisposable toolErrorScope = ToolErrorScope.BeginScope();

        ToolErrorScope.RecordSignal();

        _ = ToolErrorScope.SignalCount.Should().Be(1);
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
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Both counts are read while both scopes are open
        int ReadCount(bool record)
        {
            using IDisposable toolErrorScope = ToolErrorScope.BeginScope();
            if (record)
            {
                ToolErrorScope.Record();
            }
            _ = barrier
                .SignalAndWait(TimeSpan.FromSeconds(30), cancellationToken)
                .Should()
                .BeTrue();
            int count = ToolErrorScope.Count;
            _ = barrier
                .SignalAndWait(TimeSpan.FromSeconds(30), cancellationToken)
                .Should()
                .BeTrue();
            return count;
        }

        Task<int> recording = Task.Run(() => ReadCount(true), cancellationToken);
        Task<int> idle = Task.Run(() => ReadCount(false), cancellationToken);

        _ = (await recording).Should().Be(1);
        _ = (await idle).Should().Be(0);
    }
}
