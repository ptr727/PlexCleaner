namespace PlexCleaner;

// Counts the file's tool runs that failed to launch, were cancelled, or threw, so the media was not judged
internal static class ToolErrorScope
{
    private static readonly AsyncLocal<Session?> s_current = new();

    public static int Count => s_current.Value?.Count ?? 0;

    // Counts the file's FfMpeg verify runs a signal ended, an external kill reads the same as a crash on corrupt media
    public static int SignalCount => s_current.Value?.SignalCount ?? 0;

    public static IDisposable BeginScope()
    {
        Session? previous = s_current.Value;
        s_current.Value = new Session();
        return new Scope(previous);
    }

    public static void Record()
    {
        if (s_current.Value is { } session)
        {
            session.Count++;
        }
    }

    public static void RecordSignal()
    {
        if (s_current.Value is { } session)
        {
            session.SignalCount++;
        }
    }

    private sealed class Session
    {
        public int Count { get; set; }
        public int SignalCount { get; set; }
    }

    private sealed class Scope(Session? previous) : IDisposable
    {
        public void Dispose() => s_current.Value = previous;
    }
}
