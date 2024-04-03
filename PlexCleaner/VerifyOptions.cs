using System;
using System.Text.Json.Serialization;
using InsaneGenius.Utilities;

namespace PlexCleaner;

// v1
public record VerifyOptions1
{
    protected const int Version = 1;

    [JsonRequired]
    public bool AutoRepair { get; set; }

    [JsonRequired]
    public bool DeleteInvalidFiles { get; set; }

    [JsonRequired]
    public bool RegisterInvalidFiles { get; set; }

    // v2 : Removed
    [Obsolete]
    public int MinimumDuration { internal get; set; }

    // v2 : Removed
    [Obsolete]
    public int VerifyDuration { internal get; set; }

    // v2 : Removed
    [Obsolete]
    public int IdetDuration { internal get; set; }

    [JsonRequired]
    public int MaximumBitrate { get; set; }

    // v2 : Removed
    [Obsolete]
    public int MinimumFileAge { internal get; set; }
} 

// v2
public record VerifyOptions2 : VerifyOptions1
{
    protected new const int Version = 2;

    public VerifyOptions2() { }
    public VerifyOptions2(VerifyOptions1 verifyOptions1) : base(verifyOptions1) 
    { 
        Upgrade(VerifyOptions1.Version);
    }

    // Removed properties only

    private void Upgrade(int version) 
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
