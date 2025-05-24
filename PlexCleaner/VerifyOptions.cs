#region

using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Json.Schema.Generation;

#endregion

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
    [Obsolete("Removed in v2")]
    [JsonExclude]
    public int MinimumDuration { get; set; }

    // v2 : Removed
    [Obsolete("Removed in v2")]
    [JsonExclude]
    public int VerifyDuration { get; set; }

    // v2 : Removed
    [Obsolete("Removed in v2")]
    [JsonExclude]
    public int IdetDuration { get; set; }

    [JsonRequired]
    public int MaximumBitrate { get; set; }

    // v2 : Removed
    [Obsolete("Removed in v2")]
    [JsonExclude]
    public int MinimumFileAge { get; set; }
}

// v2
public record VerifyOptions2 : VerifyOptions1
{
    protected new const int Version = 2;

    public VerifyOptions2() { }

    public VerifyOptions2(VerifyOptions1 verifyOptions1)
        : base(verifyOptions1)
    {
        // No upgrade of schema required
        // Upgrade(VerifyOptions1.Version);
    }

    // Removed properties only

    public void SetDefaults()
    {
        AutoRepair = true;
        DeleteInvalidFiles = false;
        RegisterInvalidFiles = false;
        MaximumBitrate = 100 * 1000 * 1000;
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static")]
    public bool VerifyValues() =>
        // Nothing to do
        true;
}
