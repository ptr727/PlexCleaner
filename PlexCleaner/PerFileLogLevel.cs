using Serilog.Core;
using Serilog.Events;

namespace PlexCleaner;

// Per-file dynamic log-level elevation.
// Each media file is processed as a logical "session" with a configured floor level
// (Warning when --logwarning is set). The first Warning/Error logged, or the first
// processing-state change, elevates the effective level to Information for the rest
// of that file's processing, so remediation detail is surfaced live even when the
// global floor would otherwise suppress it. Files needing no work stay quiet.
internal static class PerFileLogLevel
{
    private static readonly AsyncLocal<Holder?> s_current = new();

    // Begin a per-file logging session at the given floor level; dispose to end it.
    public static IDisposable BeginScope(LogEventLevel floor)
    {
        Holder? previous = s_current.Value;
        s_current.Value = new Holder { Effective = floor };
        return new Scope(previous);
    }

    // Elevate the active session to Information; no-op outside a session or once elevated.
    public static void Elevate()
    {
        Holder? holder = s_current.Value;
        if (holder is not null && !holder.Elevated)
        {
            holder.Effective = LogEventLevel.Information;
            holder.Elevated = true;
        }
    }

    // Serilog filter gating events by the active session's effective level, falling back
    // to the configured floor outside any session. LogOverride context always passes so
    // verbose startup logging is unaffected; a Warning/Error inside a session elevates it.
    internal sealed class Filter(LogEventLevel floor) : ILogEventFilter
    {
        private readonly string _overrideContext = typeof(Extensions.LogOverride).FullName!;

        public bool IsEnabled(LogEvent logEvent)
        {
            // Always pass verbose LogOverride-context events (startup logging)
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

            Holder? holder = s_current.Value;
            if (holder is null)
            {
                // Outside any file session, honor the configured floor
                return logEvent.Level >= floor;
            }

            // First Warning/Error in the session elevates the remaining logging
            if (logEvent.Level >= LogEventLevel.Warning)
            {
                Elevate();
                return true;
            }

            return logEvent.Level >= holder.Effective;
        }
    }

    private sealed class Holder
    {
        public LogEventLevel Effective { get; set; }
        public bool Elevated { get; set; }
    }

    private sealed class Scope(Holder? previous) : IDisposable
    {
        public void Dispose() => s_current.Value = previous;
    }
}
