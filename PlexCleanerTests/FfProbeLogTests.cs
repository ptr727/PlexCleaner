using System.Globalization;
using AwesomeAssertions;
using PlexCleaner;
using Serilog.Events;
using Xunit;

namespace PlexCleanerTests;

// Sequential because these swap the global Serilog Log.Logger
[Collection("Sequential")]
public class FfProbeLogTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   \r\n  ")]
    public void LogErrorOutput_EmptyOrWhitespace_LogsNothing(string error) =>
        // A cancelled process produces no stderr; do not log an empty "" line
        _ = LogCapture.Capture(() => FfProbe.Tool.LogErrorOutput(error)).Should().BeEmpty();

    [Fact]
    public void LogErrorOutput_WithText_LogsAsError()
    {
        IReadOnlyList<LogEvent> events = LogCapture.Capture(() =>
            FfProbe.Tool.LogErrorOutput("boom")
        );
        _ = events.Should().ContainSingle();
        _ = events[0].Level.Should().Be(LogEventLevel.Error);
        _ = events[0].RenderMessage(CultureInfo.InvariantCulture).Should().Contain("boom");
    }
}
