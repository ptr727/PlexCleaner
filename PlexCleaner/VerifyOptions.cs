using System.ComponentModel.DataAnnotations;
using InsaneGenius.Utilities;

namespace PlexCleaner;

public class VerifyOptions
{
    public bool AutoRepair { get; set; }
    public bool DeleteInvalidFiles { get; set; }
    public bool RegisterInvalidFiles { get; set; }
    [Range(0, int.MaxValue)]
    public int MinimumDuration { get; set; }
    [Range(0, int.MaxValue)]
    public int VerifyDuration { get; set; }
    [Range(0, int.MaxValue)]
    public int IdetDuration { get; set; }
    [Range(0, int.MaxValue)]
    public int MaximumBitrate { get; set; }
    [Range(0, int.MaxValue)]
    public int MinimumFileAge { get; set; }

    public void SetDefaults()
    {
        AutoRepair = true;
        DeleteInvalidFiles = false;
        RegisterInvalidFiles = false;
        MinimumDuration = 300;
        VerifyDuration = 0;
        IdetDuration = 0;
        MaximumBitrate = 100 * Format.MB;
        MinimumFileAge = 0;
    }
}
