using System.ComponentModel.DataAnnotations;

namespace PlexCleaner;

public class MonitorOptions
{
    [Range(0, int.MaxValue)]
    public int MonitorWaitTime { get; set; }
    [Range(0, int.MaxValue)]
    public int FileRetryWaitTime { get; set; }
    [Range(0, int.MaxValue)]
    public int FileRetryCount { get; set; }

    public void SetDefaults()
    {
        MonitorWaitTime = 60;
        FileRetryWaitTime = 5;
        FileRetryCount = 2;
    }
}
