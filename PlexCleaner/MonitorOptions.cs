using System.ComponentModel.DataAnnotations;

namespace PlexCleaner;

public record MonitorOptions1
{
    protected const int Version = 1;

    [Required]
    [Range(0, int.MaxValue)]
    public int MonitorWaitTime { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int FileRetryWaitTime { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int FileRetryCount { get; set; }

    private void Upgrade(int version)
    {
        // Nothing to do
    }

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
