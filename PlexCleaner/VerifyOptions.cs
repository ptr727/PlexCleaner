using InsaneGenius.Utilities;

namespace PlexCleaner;

public class VerifyOptions
{
    public bool AutoRepair { get; set; } = true;
    public bool DeleteInvalidFiles { get; set; }
    public bool RegisterInvalidFiles { get; set; } = true;
    public int MinimumDuration { get; set; } = 300;
    public int VerifyDuration { get; set; }
    public int IdetDuration { get; set; }
    public int MaximumBitrate { get; set; } = 100 * Format.MB;
    public int MinimumFileAge { get; set; }
}