using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

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
}
