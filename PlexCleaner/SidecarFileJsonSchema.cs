// See ConfigFileJsonSchema.cs for schema update steps

#region

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Json.Schema.Generation;
using Serilog;

#endregion

namespace PlexCleaner;

public record SidecarFileJsonSchemaBase
{
    [JsonRequired]
    [JsonPropertyOrder(-2)]
    public int SchemaVersion { get; set; } = SidecarFileJsonSchema.Version;
}

// v1
public record SidecarFileJsonSchema1 : SidecarFileJsonSchemaBase
{
    protected const int Version = 1;

    // v3 : Removed
    [Obsolete("Removed in v3")]
    [JsonExclude]
    public string FfMpegToolVersion { get; set; }

    // v3 : Removed
    [Obsolete("Removed in v3")]
    [JsonExclude]
    public string MkvToolVersion { get; set; }

    // v2 : Removed
    [Obsolete("Removed in v2")]
    [JsonExclude]
    public string FfIdetInfoData { get; set; }

    [JsonRequired]
    public DateTime MediaLastWriteTimeUtc { get; set; }

    [JsonRequired]
    public long MediaLength { get; set; }

    [JsonRequired]
    [JsonPropertyName("FfProbeInfoData")]
    public string FfProbeData { get; set; }

    [JsonRequired]
    [JsonPropertyName("MkvMergeInfoData")]
    public string MkvMergeData { get; set; }

    [JsonRequired]
    public string MediaInfoToolVersion { get; set; }

    [JsonRequired]
    [JsonPropertyName("MediaInfoData")]
    public string MediaInfoData { get; set; }
}

// v2
public record SidecarFileJsonSchema2 : SidecarFileJsonSchema1
{
    protected new const int Version = 2;

    public SidecarFileJsonSchema2() { }

    public SidecarFileJsonSchema2(SidecarFileJsonSchema1 sidecarFileJsonSchema1)
        : base(sidecarFileJsonSchema1) { }

    // v2 : Added
    // v4 : Removed
    [Obsolete("Removed in v4")]
    [JsonExclude]
    public bool Verified { get; set; }
}

// v3
public record SidecarFileJsonSchema3 : SidecarFileJsonSchema2
{
    protected new const int Version = 3;

    public SidecarFileJsonSchema3() { }

    public SidecarFileJsonSchema3(SidecarFileJsonSchema1 sidecarFileJsonSchema1)
        : base(sidecarFileJsonSchema1) { }

    public SidecarFileJsonSchema3(SidecarFileJsonSchema2 sidecarFileJsonSchema2)
        : base(sidecarFileJsonSchema2) { }

    // v3 : Added
    [JsonRequired]
    public string FfProbeToolVersion { get; set; }

    // v3 : Added
    [JsonRequired]
    public string MkvMergeToolVersion { get; set; }
}

// v4
public record SidecarFileJsonSchema4 : SidecarFileJsonSchema3
{
    public new const int Version = 4;

    public SidecarFileJsonSchema4() { }

    public SidecarFileJsonSchema4(SidecarFileJsonSchema1 sidecarFileJsonSchema1)
        : base(sidecarFileJsonSchema1) => Upgrade(SidecarFileJsonSchema1.Version);

    public SidecarFileJsonSchema4(SidecarFileJsonSchema2 sidecarFileJsonSchema2)
        : base(sidecarFileJsonSchema2) => Upgrade(SidecarFileJsonSchema2.Version);

    public SidecarFileJsonSchema4(SidecarFileJsonSchema3 sidecarFileJsonSchema3)
        : base(sidecarFileJsonSchema3) => Upgrade(SidecarFileJsonSchema3.Version);

    // v4 : Added
    [JsonRequired]
    public SidecarFile.StatesType State { get; set; }

    // v4 : Added
    [JsonRequired]
    public string MediaHash { get; set; }

    private void Upgrade(int version)
    {
#pragma warning disable CS0618 // Type or member is obsolete
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
            State = sidecarFileJsonSchema3.Verified
                ? SidecarFile.StatesType.Verified
                : SidecarFile.StatesType.None;

            // Defaults
            MediaHash = "";
        }
#pragma warning restore CS0618 // Type or member is obsolete

        // v4
    }

    public static string ToJson(SidecarFileJsonSchema json) =>
        JsonSerializer.Serialize(json, ConfigFileJsonSchema.JsonWriteOptions);

    public static SidecarFileJsonSchema FromJson(string json)
    {
        // Deserialize the base class to get the schema version
        SidecarFileJsonSchemaBase sidecarFileJsonSchemaBase =
            JsonSerializer.Deserialize<SidecarFileJsonSchemaBase>(
                json,
                ConfigFileJsonSchema.JsonReadOptions
            );
        if (sidecarFileJsonSchemaBase == null)
        {
            return null;
        }

        if (sidecarFileJsonSchemaBase.SchemaVersion != Version)
        {
            Log.Warning(
                "Converting SidecarFileJsonSchema from {JsonSchemaVersion} to {CurrentSchemaVersion}",
                sidecarFileJsonSchemaBase.SchemaVersion,
                Version
            );
        }

        // Deserialize the correct version
        return sidecarFileJsonSchemaBase.SchemaVersion switch
        {
            SidecarFileJsonSchema1.Version => new SidecarFileJsonSchema(
                JsonSerializer.Deserialize<SidecarFileJsonSchema1>(
                    json,
                    ConfigFileJsonSchema.JsonReadOptions
                )
            ),
            SidecarFileJsonSchema2.Version => new SidecarFileJsonSchema(
                JsonSerializer.Deserialize<SidecarFileJsonSchema2>(
                    json,
                    ConfigFileJsonSchema.JsonReadOptions
                )
            ),
            SidecarFileJsonSchema3.Version => new SidecarFileJsonSchema(
                JsonSerializer.Deserialize<SidecarFileJsonSchema3>(
                    json,
                    ConfigFileJsonSchema.JsonReadOptions
                )
            ),
            Version => JsonSerializer.Deserialize<SidecarFileJsonSchema>(
                json,
                ConfigFileJsonSchema.JsonReadOptions
            ),
            _ => throw new NotImplementedException(),
        };
    }
}
