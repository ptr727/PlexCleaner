using Serilog.Core;
using Serilog.Events;

namespace PlexCleaner;

internal static class PerFileLogLevel
{
    private static readonly AsyncLocal<Session?> s_current = new();

    public static IDisposable BeginScope(LogEventLevel floor)
    {
        Session? previous = s_current.Value;
        s_current.Value = new Session { Effective = floor };
        return new Scope(previous);
    }

    public static void Elevate()
    {
        Session? session = s_current.Value;
        if (session is not null && !session.Elevated)
        {
            // Only lower the floor toward Information; if the configured level is already more verbose
            // (Debug or Verbose) it must not be raised, so elevation never hides sub-Information output
            if (session.Effective > LogEventLevel.Information)
            {
                session.Effective = LogEventLevel.Information;
            }
            session.Elevated = true;
        }
    }

    internal sealed class Filter(LogEventLevel floor) : ILogEventFilter
    {
        private readonly string _overrideContext = typeof(Extensions.LogOverride).FullName!;

        public bool IsEnabled(LogEvent logEvent)
        {
            // LogOverride context bypasses the floor
            if (
                logEvent.Properties.TryGetValue(
                    Constants.SourceContextPropertyName,
                    out LogEventPropertyValue? source
                )
                && source is ScalarValue { Value: string context }
                && context == _overrideContext
            )
            {
                return true;
            }

            // No session default evaluation
            Session? session = s_current.Value;
            if (session is null)
            {
                return logEvent.Level >= floor;
            }

            // Elevate future logging events on any warning or error
            if (logEvent.Level >= LogEventLevel.Warning)
            {
                Elevate();
                return true;
            }

            // Default evaluation
            return logEvent.Level >= session.Effective;
        }
    }

    private sealed class Session
    {
        public LogEventLevel Effective { get; set; }
        public bool Elevated { get; set; }
    }

    private sealed class Scope(Session? previous) : IDisposable
    {
        public void Dispose() => s_current.Value = previous;
    }
}
