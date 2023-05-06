using System.ComponentModel.DataAnnotations;

namespace PlexCleaner;

public class MonitorOptions
{
    [Required]
    [Range(0, int.MaxValue)]
    public int MonitorWaitTime { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int FileRetryWaitTime { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int FileRetryCount { get; set; }

    public void SetDefaults()
    {
        MonitorWaitTime = 60;
        FileRetryWaitTime = 5;
        FileRetryCount = 2;
    }

    public bool VerifyValues()
    {
        // Nothing to do
        return true;
    }
}
