namespace PlexCleaner;

public class MonitorOptions
{
    public int MonitorWaitTime { get; set; }
    public int FileRetryWaitTime { get; set; }
    public int FileRetryCount { get; set; }

    public void SetDefaults()
    {
        MonitorWaitTime = 60;
        FileRetryWaitTime = 5;
        FileRetryCount = 2;
    }
}
