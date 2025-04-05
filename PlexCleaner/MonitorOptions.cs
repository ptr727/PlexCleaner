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

    public void SetDefaults()
    {
        MonitorWaitTime = 60;
        FileRetryWaitTime = 5;
        FileRetryCount = 2;
    }

#pragma warning disable CA1822 // Mark members as static
    public bool VerifyValues() =>
        // Nothing to do
        true;
#pragma warning restore CA1822 // Mark members as static
}
