using System.ComponentModel.DataAnnotations;
using InsaneGenius.Utilities;

namespace PlexCleaner;

public class VerifyOptions
{
    [Required]
    public bool AutoRepair { get; set; }

    [Required]
    public bool DeleteInvalidFiles { get; set; }

    [Required]
    public bool RegisterInvalidFiles { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int MaximumBitrate { get; set; }

    public void SetDefaults()
    {
        AutoRepair = true;
        DeleteInvalidFiles = false;
        RegisterInvalidFiles = false;
        MaximumBitrate = 100 * Format.MB;
    }
}
