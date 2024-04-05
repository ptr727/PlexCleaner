using System.Text.Json.Serialization;

namespace PlexCleaner;

public record MonitorOptions1
{
    protected const int Version = 1;

    [JsonRequired]
    public int MonitorWaitTime { get; set; }

    [JsonRequired]
    public int FileRetryWaitTime { get; set; }

    [JsonRequired]
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
