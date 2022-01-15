namespace PlexCleaner;

public class MonitorOptions
{
    public int MonitorWaitTime { get; set; } = 60;
    public int FileRetryWaitTime { get; set; } = 5;
    public int FileRetryCount { get; set; } = 2;
}