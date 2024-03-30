using System.ComponentModel.DataAnnotations;

namespace PlexCleaner;

public record MonitorOptions1
{
    protected const int Version = 1;

    [Required]
    [Range(0, int.MaxValue)]
    public int MonitorWaitTime { get; protected set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int FileRetryWaitTime { get; protected set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int FileRetryCount { get; protected set; }

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
