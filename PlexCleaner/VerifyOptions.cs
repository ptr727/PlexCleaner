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
    [Json.Schema.Generation.JsonExclude]
    public int MinimumDuration { get; set; }

    // v2 : Removed
    [Obsolete]
    [Json.Schema.Generation.JsonExclude]
    public int VerifyDuration { get; set; }

    // v2 : Removed
    [Obsolete]
    [Json.Schema.Generation.JsonExclude]
    public int IdetDuration { get; set; }

    [JsonRequired]
    public int MaximumBitrate { get; set; }

    // v2 : Removed
    [Obsolete]
    [Json.Schema.Generation.JsonExclude]
    public int MinimumFileAge { get; set; }
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
