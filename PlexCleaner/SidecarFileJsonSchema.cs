// See ConfigFileJsonSchema.cs for schema update steps

using System;
using System.IO;
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
    public const int Version = 1;

    // v3 : Removed
    [Obsolete("Removed in v3")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
    public string FfMpegToolVersion { get; set; } = string.Empty;

    // v3 : Removed
    [Obsolete("Removed in v3")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
    public string MkvToolVersion { get; set; } = string.Empty;

    // v2 : Removed
    [Obsolete("Removed in v2")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
    public string FfIdetInfoData { get; set; } = string.Empty;

    [JsonRequired]
    public DateTime MediaLastWriteTimeUtc { get; set; }

    [JsonRequired]
    public long MediaLength { get; set; }

    [JsonRequired]
    [JsonPropertyName("FfProbeInfoData")]
    public string FfProbeData { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("MkvMergeInfoData")]
    public string MkvMergeData { get; set; } = string.Empty;

    [JsonRequired]
    public string MediaInfoToolVersion { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("MediaInfoData")]
    public string MediaInfoData { get; set; } = string.Empty;
}

// v2
public record SidecarFileJsonSchema2 : SidecarFileJsonSchema1
{
    public new const int Version = 2;

    public SidecarFileJsonSchema2() { }

    public SidecarFileJsonSchema2(SidecarFileJsonSchema1 sidecarFileJsonSchema1)
        : base(sidecarFileJsonSchema1) { }

    // v2 : Added
    // v4 : Removed
    [Obsolete("Removed in v4")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
    public bool Verified { get; set; }
}

// v3
public record SidecarFileJsonSchema3 : SidecarFileJsonSchema2
{
    public new const int Version = 3;

    public SidecarFileJsonSchema3() { }

    public SidecarFileJsonSchema3(SidecarFileJsonSchema1 sidecarFileJsonSchema1)
        : base(sidecarFileJsonSchema1) { }

    public SidecarFileJsonSchema3(SidecarFileJsonSchema2 sidecarFileJsonSchema2)
        : base(sidecarFileJsonSchema2) { }

    // v3 : Added
    [JsonRequired]
    public string FfProbeToolVersion { get; set; } = string.Empty;

    // v3 : Added
    [JsonRequired]
    public string MkvMergeToolVersion { get; set; } = string.Empty;
}

// v4
public record SidecarFileJsonSchema4 : SidecarFileJsonSchema3
{
    public new const int Version = 4;

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
    public string MediaHash { get; set; } = string.Empty;
}

// v5
public record SidecarFileJsonSchema5 : SidecarFileJsonSchema4
{
    public new const int Version = 5;

    [JsonIgnore]
    public int DeserializedVersion { get; private set; } = Version;

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
            MediaHash = string.Empty;
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
            MediaHash = string.Empty;
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
            MediaHash = string.Empty;
        }

        // v4
        if (version <= SidecarFileJsonSchema4.Version)
        {
            // Get v4 schema
            SidecarFileJsonSchema4 sidecarFileJsonSchema4 = this;

            // v5: Changed MediaInfo schema from XML to JSON
            // Convert MediaInfo XML attributes to JSON
            string decompressedXml = StringCompression.Decompress(
                sidecarFileJsonSchema4.MediaInfoData
            );
            string jsonData = MediaInfoXmlParser.GenericXmlToJson(decompressedXml);
            sidecarFileJsonSchema4.MediaInfoData = StringCompression.Compress(jsonData);
        }
#pragma warning restore CS0618 // Type or member is obsolete

        // v5

        // Set schema version to current and save original version
        SchemaVersion = Version;
        DeserializedVersion = version;
    }

    public static SidecarFileJsonSchema FromFile(string path) => FromJson(File.ReadAllText(path));

    public static SidecarFileJsonSchema OpenAndUpgrade(string path)
    {
        SidecarFileJsonSchema sidecarJson = FromFile(path);
        if (sidecarJson.DeserializedVersion != Version)
        {
            Log.Warning(
                "Writing SidecarFileJsonSchema upgraded from version {LoadedVersion} to {CurrentVersion}, {FileName}",
                sidecarJson.DeserializedVersion,
                Version,
                path
            );
            ToFile(path, sidecarJson);
        }
        return sidecarJson;
    }

    public static void ToFile(string path, SidecarFileJsonSchema json)
    {
        // Set the schema version to the current version
        json.SchemaVersion = Version;

        // Write JSON to file
        File.WriteAllText(path, ToJson(json));
    }

    private static string ToJson(SidecarFileJsonSchema json) =>
        JsonSerializer.Serialize(json, SidecarFileJsonContext.Default.SidecarFileJsonSchema5);

    // Will throw on failure to deserialize
    private static SidecarFileJsonSchema FromJson(string json)
    {
        // Deserialize the base class to get the schema version
        SidecarFileJsonSchemaBase sidecarFileJsonSchemaBase =
            JsonSerializer.Deserialize(
                json,
                SidecarFileJsonContext.Default.SidecarFileJsonSchemaBase
            ) ?? throw new JsonException("Failed to deserialize SidecarFileJsonSchemaBase");

        if (sidecarFileJsonSchemaBase.SchemaVersion != Version)
        {
            Log.Warning(
                "Converting SidecarFileJsonSchema version from {JsonSchemaVersion} to {CurrentSchemaVersion}",
                sidecarFileJsonSchemaBase.SchemaVersion,
                Version
            );
        }

        // Deserialize the correct version
        switch (sidecarFileJsonSchemaBase.SchemaVersion)
        {
            case SidecarFileJsonSchema1.Version:
                SidecarFileJsonSchema1 sidecarFileJsonSchema1 =
                    JsonSerializer.Deserialize(
                        json,
                        SidecarFileJsonContext.Default.SidecarFileJsonSchema1
                    ) ?? throw new JsonException("Failed to deserialize SidecarFileJsonSchema1");
                return new SidecarFileJsonSchema(sidecarFileJsonSchema1);

            case SidecarFileJsonSchema2.Version:
                SidecarFileJsonSchema2 sidecarFileJsonSchema2 =
                    JsonSerializer.Deserialize(
                        json,
                        SidecarFileJsonContext.Default.SidecarFileJsonSchema2
                    ) ?? throw new JsonException("Failed to deserialize SidecarFileJsonSchema2");
                return new SidecarFileJsonSchema(sidecarFileJsonSchema2);
            case SidecarFileJsonSchema3.Version:
                SidecarFileJsonSchema3 sidecarFileJsonSchema3 =
                    JsonSerializer.Deserialize(
                        json,
                        SidecarFileJsonContext.Default.SidecarFileJsonSchema3
                    ) ?? throw new JsonException("Failed to deserialize SidecarFileJsonSchema3");
                return new SidecarFileJsonSchema(sidecarFileJsonSchema3);

            case SidecarFileJsonSchema4.Version:
                SidecarFileJsonSchema4 sidecarFileJsonSchema4 =
                    JsonSerializer.Deserialize(
                        json,
                        SidecarFileJsonContext.Default.SidecarFileJsonSchema4
                    ) ?? throw new JsonException("Failed to deserialize SidecarFileJsonSchema4");
                return new SidecarFileJsonSchema(sidecarFileJsonSchema4);
            case Version:
                SidecarFileJsonSchema sidecarFileJsonSchema =
                    JsonSerializer.Deserialize(
                        json,
                        SidecarFileJsonContext.Default.SidecarFileJsonSchema5
                    ) ?? throw new JsonException("Failed to deserialize SidecarFileJsonSchema5");
                return sidecarFileJsonSchema;
            default:
                throw new NotSupportedException(
                    $"Unsupported schema version: {sidecarFileJsonSchemaBase.SchemaVersion}"
                );
        }
    }
}

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
