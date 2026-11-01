using System.Globalization;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace PlexCleanerTests;

internal static class LogCapture
{
    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    // Swaps the global Log.Logger, so every caller joins the Sequential collection
    public static IReadOnlyList<LogEvent> Capture(Action action)
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

    public static List<string> Messages(IReadOnlyList<LogEvent> events, LogEventLevel level) =>
        [
            .. events
                .Where(e => e.Level == level)
                .Select(e => e.RenderMessage(CultureInfo.InvariantCulture)),
        ];
}
