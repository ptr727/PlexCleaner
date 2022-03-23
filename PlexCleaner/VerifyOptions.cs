using InsaneGenius.Utilities;

namespace PlexCleaner;

public class VerifyOptions
{
    public bool AutoRepair { get; set; }
    public bool DeleteInvalidFiles { get; set; }
    public bool RegisterInvalidFiles { get; set; }
    public int MinimumDuration { get; set; }
    public int VerifyDuration { get; set; }
    public int IdetDuration { get; set; }
    public int MaximumBitrate { get; set; }
    public int MinimumFileAge { get; set; }

    public void SetDefaults()
    {
        AutoRepair = true;
        DeleteInvalidFiles = false;
        RegisterInvalidFiles = true;
        MinimumDuration = 300;
        VerifyDuration = 0;
        IdetDuration = 0;
        MaximumBitrate = 100 * Format.MB;
        MinimumFileAge = 0;
    }
}
