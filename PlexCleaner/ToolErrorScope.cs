namespace PlexCleaner;

// Counts the file's tool runs that failed to launch, were cancelled, or threw, so the media was not judged
internal static class ToolErrorScope
{
    private static readonly AsyncLocal<Session?> s_current = new();

    public static int Count => s_current.Value?.Count ?? 0;

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

    private sealed class Session
    {
        public int Count { get; set; }
    }

    private sealed class Scope(Session? previous) : IDisposable
    {
        public void Dispose() => s_current.Value = previous;
    }
}
