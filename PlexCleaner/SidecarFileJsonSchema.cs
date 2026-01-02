// See ConfigFileJsonSchema.cs for schema update steps

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using InsaneGenius.Utilities;
using Serilog;

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
    public string FfMpegToolVersion { get; set; }

    // v3 : Removed
    [Obsolete("Removed in v3")]
    public string MkvToolVersion { get; set; }

    // v2 : Removed
    [Obsolete("Removed in v2")]
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
    protected new const int Version = 4;

    public SidecarFileJsonSchema4() { }

    public SidecarFileJsonSchema4(SidecarFileJsonSchema1 sidecarFileJsonSchema1)
        : base(sidecarFileJsonSchema1) { }

    public SidecarFileJsonSchema4(SidecarFileJsonSchema2 sidecarFileJsonSchema2)
        : base(sidecarFileJsonSchema2) { }

    public SidecarFileJsonSchema4(SidecarFileJsonSchema3 sidecarFileJsonSchema3)
        : base(sidecarFileJsonSchema3) { }

    // v4 : Added
    [JsonRequired]
    public SidecarFile.StatesType State { get; set; }

    // v4 : Added
    [JsonRequired]
    public string MediaHash { get; set; }
}

// v5
public record SidecarFileJsonSchema5 : SidecarFileJsonSchema4
{
    public new const int Version = 5;

    public SidecarFileJsonSchema5() { }

    public SidecarFileJsonSchema5(SidecarFileJsonSchema1 sidecarFileJsonSchema1)
        : base(sidecarFileJsonSchema1) => Upgrade(SidecarFileJsonSchema1.Version);

    public SidecarFileJsonSchema5(SidecarFileJsonSchema2 sidecarFileJsonSchema2)
        : base(sidecarFileJsonSchema2) => Upgrade(SidecarFileJsonSchema2.Version);

    public SidecarFileJsonSchema5(SidecarFileJsonSchema3 sidecarFileJsonSchema3)
        : base(sidecarFileJsonSchema3) => Upgrade(SidecarFileJsonSchema3.Version);

    public SidecarFileJsonSchema5(SidecarFileJsonSchema4 sidecarFileJsonSchema4)
        : base(sidecarFileJsonSchema4) => Upgrade(SidecarFileJsonSchema4.Version);

    // v5: Changed MediaInfo from XML to JSON
    // No schema change

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

        // v4
        if (version <= SidecarFileJsonSchema4.Version)
        {
            // Get v4 schema
            SidecarFileJsonSchema4 sidecarFileJsonSchema4 = this;

            // v5: Changed MediaInfo from XML to JSON
            sidecarFileJsonSchema4.MediaInfoData = StringCompression.Compress(
                MediaInfoXmlToJson(
                    StringCompression.Decompress(sidecarFileJsonSchema4.MediaInfoData)
                )
            );
        }
#pragma warning restore CS0618 // Type or member is obsolete
    }

    public static string ToJson(SidecarFileJsonSchema json) =>
        JsonSerializer.Serialize(json, SidecarFileJsonContext.Default.SidecarFileJsonSchema5);

    public static SidecarFileJsonSchema FromJson(string json)
    {
        // Deserialize the base class to get the schema version
        SidecarFileJsonSchemaBase sidecarFileJsonSchemaBase = JsonSerializer.Deserialize(
            json,
            SidecarFileJsonContext.Default.SidecarFileJsonSchemaBase
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
                JsonSerializer.Deserialize(
                    json,
                    SidecarFileJsonContext.Default.SidecarFileJsonSchema1
                )
            ),
            SidecarFileJsonSchema2.Version => new SidecarFileJsonSchema(
                JsonSerializer.Deserialize(
                    json,
                    SidecarFileJsonContext.Default.SidecarFileJsonSchema2
                )
            ),
            SidecarFileJsonSchema3.Version => new SidecarFileJsonSchema(
                JsonSerializer.Deserialize(
                    json,
                    SidecarFileJsonContext.Default.SidecarFileJsonSchema3
                )
            ),
            SidecarFileJsonSchema4.Version => new SidecarFileJsonSchema(
                JsonSerializer.Deserialize(
                    json,
                    SidecarFileJsonContext.Default.SidecarFileJsonSchema4
                )
            ),
            Version => JsonSerializer.Deserialize(
                json,
                SidecarFileJsonContext.Default.SidecarFileJsonSchema5
            ),
            _ => throw new NotImplementedException(),
        };
    }

    private static string MediaInfoXmlToJson(string mediaInfoXml)
    {
        // Serialize from XML
        MediaInfoToolXmlSchema.MediaInfo xmlMediaInfo = MediaInfoToolXmlSchema.MediaInfo.FromXml(
            mediaInfoXml
        );

        // Copy to JSON schema
        MediaInfoToolJsonSchema.MediaInfo jsonMediaInfo = new();
        foreach (MediaInfoToolXmlSchema.Track xmlTrack in xmlMediaInfo.Media.Tracks)
        {
            MediaInfoToolJsonSchema.Track jsonTrack = new()
            {
                Type = xmlTrack.Type,
                Id = xmlTrack.Id,
                UniqueId = xmlTrack.UniqueId,
                Duration = xmlTrack.Duration,
                Format = xmlTrack.Format,
                FormatProfile = xmlTrack.FormatProfile,
                FormatLevel = xmlTrack.FormatLevel,
                HdrFormat = xmlTrack.HdrFormat,
                CodecId = xmlTrack.CodecId,
                Language = xmlTrack.Language,
                Default = xmlTrack.Default,
                Forced = xmlTrack.Forced,
                MuxingMode = xmlTrack.MuxingMode,
                StreamOrder = xmlTrack.StreamOrder,
                ScanType = xmlTrack.ScanType,
                Title = xmlTrack.Title,
            };
            jsonMediaInfo.Media.Tracks.Add(jsonTrack);
        }

        // Serialize to JSON
        return JsonSerializer.Serialize(jsonMediaInfo, MediaInfoToolJsonContext.Default.MediaInfo);
    }
}

// TODO:
// TypeInfoResolver = SourceGenerationContext.Default.WithAddedModifier(ExcludeObsoletePropertiesModifier),
[JsonSourceGenerationOptions(
    AllowTrailingCommas = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    IncludeFields = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
    ReadCommentHandling = JsonCommentHandling.Skip,
    WriteIndented = true,
    NewLine = "\r\n"
)]
[JsonSerializable(typeof(SidecarFileJsonSchemaBase))]
[JsonSerializable(typeof(SidecarFileJsonSchema1))]
[JsonSerializable(typeof(SidecarFileJsonSchema2))]
[JsonSerializable(typeof(SidecarFileJsonSchema3))]
[JsonSerializable(typeof(SidecarFileJsonSchema4))]
[JsonSerializable(typeof(SidecarFileJsonSchema))]
internal partial class SidecarFileJsonContext : JsonSerializerContext;
