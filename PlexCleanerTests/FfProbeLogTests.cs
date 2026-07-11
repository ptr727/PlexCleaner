using System.Globalization;
using AwesomeAssertions;
using PlexCleaner;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace PlexCleanerTests;

// Sequential because these swap the global Serilog Log.Logger
[Collection("Sequential")]
public class FfProbeLogTests
{
    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    // Capture events emitted during the action by temporarily redirecting the static Serilog logger
    private static List<LogEvent> Capture(Action action)
    {
        CapturingSink sink = new();
        ILogger original = Log.Logger;
        Logger capture = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();
        Log.Logger = capture;
        try
        {
            action();
        }
        finally
        {
            Log.Logger = original;
            capture.Dispose();
        }
        return sink.Events;
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \r\n  ")]
    public void LogErrorOutput_EmptyOrWhitespace_LogsNothing(string error) =>
        // A cancelled process produces no stderr; do not log an empty "" line
        _ = Capture(() => FfProbe.Tool.LogErrorOutput(error)).Should().BeEmpty();

    [Fact]
    public void LogErrorOutput_WithText_LogsAsError()
    {
        List<LogEvent> events = Capture(() => FfProbe.Tool.LogErrorOutput("boom"));
        _ = events.Should().ContainSingle();
        _ = events[0].Level.Should().Be(LogEventLevel.Error);
        _ = events[0].RenderMessage(CultureInfo.InvariantCulture).Should().Contain("boom");
    }
}
