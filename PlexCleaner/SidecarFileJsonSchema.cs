using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Newtonsoft.Json;
using Serilog;

namespace PlexCleaner;

public record SidecarFileJsonSchemaBase
{
    public SidecarFileJsonSchemaBase() { }

    // TODO: Add a schema
    // Schema reference
    // [JsonProperty(PropertyName = "$schema", Order = -3)]
    // public string Schema { get; } = SchemaUri;

    [DefaultValue(0)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate, Order = -2)]
    public int SchemaVersion { get; set; } = SidecarFileJsonSchema.Version;
}

// v1
[Obsolete]
public record SidecarFileJsonSchema1 : SidecarFileJsonSchemaBase
{
    public SidecarFileJsonSchema1() { }

    [Obsolete]
    internal string FfMpegToolVersion { get; set; }
    [Obsolete]
    internal string MkvToolVersion { get; set; }
    [Obsolete]
    internal string FfIdetInfoData { get; set; }

    [Required]
    public DateTime MediaLastWriteTimeUtc { get; set; }

    [Required]
    public long MediaLength { get; set; }

    [Required]
    public string FfProbeInfoData { get; set; }


    [Required]
    public string MkvMergeInfoData { get; set; }

    [Required]
    public string MediaInfoToolVersion { get; set; }

    [Required]
    public string MediaInfoData { get; set; }

    // v1
    public const int Version = 1;
}

// v2
[Obsolete]
public record SidecarFileJsonSchema2 : SidecarFileJsonSchema1
{
    public SidecarFileJsonSchema2() { }

    // Copy from v1
    [Obsolete]
    public SidecarFileJsonSchema2(SidecarFileJsonSchema1 sidecarFileJsonSchema1) : base(sidecarFileJsonSchema1)
    {
        Upgrade(sidecarFileJsonSchema1);
    }

    [Obsolete]
    protected void Upgrade(SidecarFileJsonSchema1 sidecarFileJsonSchema1)
    {
        // Upgrade v1 to v2
        Verified = false;
    }

    [Obsolete]
    internal bool Verified { get; set; }

    // v2
    public new const int Version = 2;
}

// v3
[Obsolete]
public record SidecarFileJsonSchema3 : SidecarFileJsonSchema2
{
    public SidecarFileJsonSchema3() { }

    [Obsolete]
    public SidecarFileJsonSchema3(SidecarFileJsonSchema1 sidecarFileJsonSchema1) : base(sidecarFileJsonSchema1)
    {
        Upgrade(sidecarFileJsonSchema1);
    }

    [Obsolete]
    public SidecarFileJsonSchema3(SidecarFileJsonSchema2 sidecarFileJsonSchema2) : base(sidecarFileJsonSchema2)
    {
        Upgrade(sidecarFileJsonSchema2);
    }

    [Obsolete]
    protected void Upgrade(SidecarFileJsonSchema2 sidecarFileJsonSchema2)
    {
        // Upgrade v1 to v2
        Upgrade((SidecarFileJsonSchema1)sidecarFileJsonSchema2);

        // Upgrade v2 to v3
        State = Verified ? SidecarFile.StatesType.Verified : SidecarFile.StatesType.None;
        FfProbeToolVersion = FfMpegToolVersion;
        MkvMergeToolVersion = MkvToolVersion;
    }

    [Required]
    public string FfProbeToolVersion { get; set; }

    [Required]
    public string MkvMergeToolVersion { get; set; }

    [Required]
    public SidecarFile.StatesType State { get; set; }

    // v3
    public new const int Version = 3;
}

// v4
#pragma warning disable CS0612 // Type or member is obsolete
public record SidecarFileJsonSchema : SidecarFileJsonSchema3
#pragma warning restore CS0612 // Type or member is obsolete
{
    public SidecarFileJsonSchema() { }

    [Obsolete]
    public SidecarFileJsonSchema(SidecarFileJsonSchema1 sidecarFileJsonSchema1) : base(sidecarFileJsonSchema1)
    {
        Upgrade(sidecarFileJsonSchema1);
    }

    [Obsolete]
    public SidecarFileJsonSchema(SidecarFileJsonSchema2 sidecarFileJsonSchema2) : base(sidecarFileJsonSchema2)
    {
        Upgrade(sidecarFileJsonSchema2);
    }

    [Obsolete]
    public SidecarFileJsonSchema(SidecarFileJsonSchema3 sidecarFileJsonSchema3) : base(sidecarFileJsonSchema3)
    {
        Upgrade(sidecarFileJsonSchema3);
    }

    [Obsolete]
    protected void Upgrade(SidecarFileJsonSchema3 sidecarFileJsonSchema3)
    {
        // Upgrade v2 to v3
        Upgrade((SidecarFileJsonSchema2)sidecarFileJsonSchema3);

        // Upgrade v3 to v4
        MediaHash = "";
    }

    [Required]
    public string MediaHash { get; set; }

    // v4
    public new const int Version = 4;

    public static string ToJson(SidecarFileJsonSchema json)
    {
        return JsonConvert.SerializeObject(json, Settings);
    }

    public static SidecarFileJsonSchema FromJson(string json)
    {
        // Deserialize the base class to get the schema version
        var sidecarFileJsonSchemaBase = JsonConvert.DeserializeObject<SidecarFileJsonSchemaBase>(json, Settings);
        if (sidecarFileJsonSchemaBase == null)
        {
            return null;
        }

        if (sidecarFileJsonSchemaBase.SchemaVersion != Version)
        {
            Log.Logger.Warning("Upgrading SidecarFileJsonSchema from {JsonSchemaVersion} to {CurrentSchemaVersion}", sidecarFileJsonSchemaBase.SchemaVersion, Version);
        }

        // Deserialize the correct version
        switch (sidecarFileJsonSchemaBase.SchemaVersion)
        {
#pragma warning disable CS0612 // Type or member is obsolete
            // Version 1
            case SidecarFileJsonSchema1.Version:
                return new SidecarFileJsonSchema(JsonConvert.DeserializeObject<SidecarFileJsonSchema1>(json, Settings));
            // Version 2
            case SidecarFileJsonSchema2.Version:
                return new SidecarFileJsonSchema(JsonConvert.DeserializeObject<SidecarFileJsonSchema2>(json, Settings));
            // Version 3
            case SidecarFileJsonSchema3.Version:
                return new SidecarFileJsonSchema(JsonConvert.DeserializeObject<SidecarFileJsonSchema3>(json, Settings));
#pragma warning restore CS0612 // Type or member is obsolete
            // Current version
            case Version:
                return JsonConvert.DeserializeObject<SidecarFileJsonSchema>(json, Settings);
            // Unknown version
            default:
                throw new NotSupportedException(nameof(sidecarFileJsonSchemaBase.SchemaVersion));
        }
    }

    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented
    };
}
