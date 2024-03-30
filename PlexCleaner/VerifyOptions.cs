using System;
using System.ComponentModel.DataAnnotations;
using InsaneGenius.Utilities;

namespace PlexCleaner;

// v1
public record VerifyOptions1
{
    public const int Version = 1;

    [Required]
    public bool AutoRepair { get; set; }

    [Required]
    public bool DeleteInvalidFiles { get; set; }

    [Required]
    public bool RegisterInvalidFiles { get; set; }

    // v2 : Removed
    [Obsolete]
    [Range(0, int.MaxValue)]
    internal int MinimumDuration { get; set; }

    // v2 : Removed
    [Obsolete]
    [Range(0, int.MaxValue)]
    internal int VerifyDuration { get; set; }

    // v2 : Removed
    [Obsolete]
    [Range(0, int.MaxValue)]
    internal int IdetDuration { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int MaximumBitrate { get; set; }

    // v2 : Removed
    [Obsolete]
    [Range(0, int.MaxValue)]
    internal int MinimumFileAge { get; set; }
} 

// v2
// Removed properties only
public record VerifyOptions2 : VerifyOptions1
{
    public new const int Version = 2;

    public VerifyOptions2() { }

    public VerifyOptions2(VerifyOptions1 verifyOptions1) : base(verifyOptions1)
    {
        Upgrade(VerifyOptions1.Version);
    }

    public void Upgrade(int version) 
    { 
        // Nothing to do
    }

    public void SetDefaults()
    {
        AutoRepair = true;
        DeleteInvalidFiles = false;
        RegisterInvalidFiles = false;
        MaximumBitrate = 100 * Format.MB;
    }

    public bool VerifyValues()
    {
        // Nothing to do
        return true;
    }
}
