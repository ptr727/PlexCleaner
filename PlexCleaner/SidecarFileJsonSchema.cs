// Current schema version
global using SidecarFileJsonSchema = PlexCleaner.SidecarFileJsonSchema4;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace PlexCleaner;

public record SidecarFileJsonSchemaBase
{
    [DefaultValue(0)]
    [JsonPropertyOrder(-2)]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int SchemaVersion { get; set; } = SidecarFileJsonSchema.Version;
}

// v1
public record SidecarFileJsonSchema1 : SidecarFileJsonSchemaBase
{
    protected const int Version = 1;

    // v3 : Removed
    [Obsolete]
    public string FfMpegToolVersion { internal get; set; }

    // v3 : Removed
    [Obsolete]
    public string MkvToolVersion { internal get; set; }
    
    // v2 : Removed
    [Obsolete]
    public string FfIdetInfoData { internal get; set; }

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
}

// v2
public record SidecarFileJsonSchema2 : SidecarFileJsonSchema1
{
    protected new const int Version = 2;

    public SidecarFileJsonSchema2() { }
    public SidecarFileJsonSchema2(SidecarFileJsonSchema1 sidecarFileJsonSchema1) : base(sidecarFileJsonSchema1) { }

    // v2 : Added
    // v4 : Removed
    [Obsolete]
    public bool Verified { internal get; set; }
}

// v3
public record SidecarFileJsonSchema3 : SidecarFileJsonSchema2
{
    protected new const int Version = 3;

    public SidecarFileJsonSchema3() { }
    public SidecarFileJsonSchema3(SidecarFileJsonSchema1 sidecarFileJsonSchema1) : base(sidecarFileJsonSchema1) { }
    public SidecarFileJsonSchema3(SidecarFileJsonSchema2 sidecarFileJsonSchema2) : base(sidecarFileJsonSchema2) { }

    // v3 : Added
    [Required]
    public string FfProbeToolVersion { get; set; }

    // v3 : Added
    [Required]
    public string MkvMergeToolVersion { get; set; }
}

// v4
public record SidecarFileJsonSchema4 : SidecarFileJsonSchema3
{
    public new const int Version = 4;

    public SidecarFileJsonSchema4() { }
    public SidecarFileJsonSchema4(SidecarFileJsonSchema1 sidecarFileJsonSchema1) : base(sidecarFileJsonSchema1) 
    {
        Upgrade(SidecarFileJsonSchema1.Version);
    }
    public SidecarFileJsonSchema4(SidecarFileJsonSchema2 sidecarFileJsonSchema2) : base(sidecarFileJsonSchema2)
    {
        Upgrade(SidecarFileJsonSchema2.Version);
    }
    public SidecarFileJsonSchema4(SidecarFileJsonSchema3 sidecarFileJsonSchema3) : base(sidecarFileJsonSchema3)
    {
        Upgrade(SidecarFileJsonSchema3.Version);
    }

    // v4 : Added
    [Required]
    public SidecarFile.StatesType State { get; set; }

    // v4 : Added
    [Required]
    public string MediaHash { get; set; }

#pragma warning disable CS0612 // Type or member is obsolete
    private void Upgrade(int version)
    {
        // v1
        if (version <= SidecarFileJsonSchema1.Version)
        {
            // Get v1 schema
            // SidecarFileJsonSchema1 sidecarFileJsonSchema1 = this;

            // Defaults
            State = SidecarFile.StatesType.None;
            MediaHash = "";
        }

        // v2
        if (version <= SidecarFileJsonSchema2.Version)
        {
            // Get v2 schema
            SidecarFileJsonSchema2 sidecarFileJsonSchema2 = this;

            // Upgrade v2 to v3
            FfProbeToolVersion = sidecarFileJsonSchema2.FfMpegToolVersion;
            MkvMergeToolVersion = sidecarFileJsonSchema2.MkvToolVersion;

            // Defaults
            State = SidecarFile.StatesType.None;
            MediaHash = "";
        }

        // v3
        if (version <= SidecarFileJsonSchema3.Version)
        {
            // Get v3 schema
            SidecarFileJsonSchema3 sidecarFileJsonSchema3 = this;

            // Upgrade v3 to v4
            State = sidecarFileJsonSchema3.Verified ? SidecarFile.StatesType.Verified : SidecarFile.StatesType.None;

            // Defaults
            MediaHash = "";
        }

        // v4
    }
#pragma warning restore CS0612 // Type or member is obsolete

    public static string ToJson(SidecarFileJsonSchema json)
    {
        return JsonSerializer.Serialize(json, ConfigFileJsonSchema.JsonWriteOptions);
    }

    public static SidecarFileJsonSchema FromJson(string json)
    {
        // Deserialize the base class to get the schema version
        var sidecarFileJsonSchemaBase = JsonSerializer.Deserialize<SidecarFileJsonSchemaBase>(json, ConfigFileJsonSchema.JsonReadOptions);
        if (sidecarFileJsonSchemaBase == null)
        {
            return null;
        }

        if (sidecarFileJsonSchemaBase.SchemaVersion != Version)
        {
            Log.Logger.Warning("Upgrading SidecarFileJsonSchema from {JsonSchemaVersion} to {CurrentSchemaVersion}", sidecarFileJsonSchemaBase.SchemaVersion, Version);
        }

        // Deserialize the correct version
        return sidecarFileJsonSchemaBase.SchemaVersion switch
        {
            SidecarFileJsonSchema1.Version => new SidecarFileJsonSchema(JsonSerializer.Deserialize<SidecarFileJsonSchema1>(json, ConfigFileJsonSchema.JsonReadOptions)),
            SidecarFileJsonSchema2.Version => new SidecarFileJsonSchema(JsonSerializer.Deserialize<SidecarFileJsonSchema2>(json, ConfigFileJsonSchema.JsonReadOptions)),
            SidecarFileJsonSchema3.Version => new SidecarFileJsonSchema(JsonSerializer.Deserialize<SidecarFileJsonSchema3>(json, ConfigFileJsonSchema.JsonReadOptions)),
            Version => JsonSerializer.Deserialize<SidecarFileJsonSchema>(json, ConfigFileJsonSchema.JsonReadOptions),
            _ => throw new NotImplementedException()
        };
    }
}
