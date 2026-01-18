// Schema update steps (also applies to SidecarFileJsonSchema):
// Derive new class from previous version
// Keep all utility functions e.g. Upgrade() in the latest version
// Add copy constructors to the new class to handle upgrading from the previous version
// Mark changed or removed attributes as [Obsolete] and [Json.Schema.Generation.JsonExclude] and remove [JsonRequired]
// Add new attributes to the new class and mark as [JsonRequired]
// Update the Upgrade() method to handle upgrading from the previous version
// Update GlobalUsing.cs global using statements to the latest version

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using Serilog;

namespace PlexCleaner;

// Base
public record ConfigFileJsonSchemaBase
{
    // Schema Id
    protected const string SchemaUri =
        "https://raw.githubusercontent.com/ptr727/PlexCleaner/main/PlexCleaner.schema.json";

    [JsonPropertyName("$schema")]
    [JsonPropertyOrder(-3)]
    public static string Schema => SchemaUri;

    [JsonRequired]
    [JsonPropertyOrder(-2)]
    public int SchemaVersion { get; set; } = ConfigFileJsonSchema.Version;
}

// v1
public record ConfigFileJsonSchema1 : ConfigFileJsonSchemaBase
{
    public const int Version = 1;

    [JsonRequired]
    [JsonPropertyOrder(1)]
    public ToolsOptions1 ToolsOptions { get; set; } = new();

    // v2 : Replaced with ProcessOptions2
    [Obsolete("Replaced with ProcessOptions2 in v2.")]
    public ProcessOptions1 ProcessOptions { get; set; } = new();

    // v3 : Replaced with ConvertOptions2
    [Obsolete("Replaced with ConvertOptions2 in v3.")]
    public ConvertOptions1 ConvertOptions { get; set; } = new();

    // v3 : Replaced with VerifyOptions2
    [Obsolete("Replaced with VerifyOptions2 in v3.")]
    public VerifyOptions1 VerifyOptions { get; set; } = new();

    [JsonRequired]
    [JsonPropertyOrder(5)]
    public MonitorOptions1 MonitorOptions { get; set; } = new();
}

// v2
public record ConfigFileJsonSchema2 : ConfigFileJsonSchema1
{
    public new const int Version = 2;

    public ConfigFileJsonSchema2() { }

    public ConfigFileJsonSchema2(ConfigFileJsonSchema1 configFileJsonSchema1)
        : base(configFileJsonSchema1) { }

    // v2 : Added
    // v3 : Replaced with ProcessOptions3
    [Obsolete("Replaced with ProcessOptions3 in v3.")]
    public new ProcessOptions2 ProcessOptions { get; set; } = new();
}

// v3
public record ConfigFileJsonSchema3 : ConfigFileJsonSchema2
{
    public new const int Version = 3;

    public ConfigFileJsonSchema3() { }

    public ConfigFileJsonSchema3(ConfigFileJsonSchema1 configFileJsonSchema1)
        : base(configFileJsonSchema1) { }

    public ConfigFileJsonSchema3(ConfigFileJsonSchema2 configFileJsonSchema2)
        : base(configFileJsonSchema2) { }

    // v3 : Added
    // v4 : Replaced with ProcessOptions4
    [Obsolete("Replaced with ProcessOptions4 in v4.")]
    public new ProcessOptions3 ProcessOptions { get; set; } = new();

    // v3 : Added
    // v4 : Replaced with ConvertOptions3
    [Obsolete("Replaced with ConvertOptions3 in v4.")]
    public new ConvertOptions2 ConvertOptions { get; set; } = new();

    // v3 : Added
    [JsonRequired]
    [JsonPropertyOrder(4)]
    public new VerifyOptions2 VerifyOptions { get; set; } = new();
}

// v4
public record ConfigFileJsonSchema4 : ConfigFileJsonSchema3
{
    public new const int Version = 4;

    [JsonIgnore]
    public int DeserializedVersion { get; private set; } = Version;

    public ConfigFileJsonSchema4() { }

    public ConfigFileJsonSchema4(ConfigFileJsonSchema1 configFileJsonSchema1)
        : base(configFileJsonSchema1) => Upgrade(ConfigFileJsonSchema1.Version);

    public ConfigFileJsonSchema4(ConfigFileJsonSchema2 configFileJsonSchema2)
        : base(configFileJsonSchema2) => Upgrade(ConfigFileJsonSchema2.Version);

    public ConfigFileJsonSchema4(ConfigFileJsonSchema3 configFileJsonSchema3)
        : base(configFileJsonSchema3) => Upgrade(ConfigFileJsonSchema3.Version);

    // v4 : Added
    [JsonRequired]
    [JsonPropertyOrder(2)]
    public new ProcessOptions4 ProcessOptions { get; set; } = new();

    // v4 : Added
    [JsonRequired]
    [JsonPropertyOrder(3)]
    public new ConvertOptions3 ConvertOptions { get; set; } = new();

    private void Upgrade(int version)
    {
        // v1
        if (version == ConfigFileJsonSchema1.Version)
        {
            // ToolsOptions1
            // ProcessOptions1 *
            // ConvertOptions1 *
            // VerifyOptions1 *
            // MonitorOptions1
            ConfigFileJsonSchema1 configFileJsonSchema1 = this;

            // Upgrade to current version
#pragma warning disable CS0618 // Type or member is obsolete
            ProcessOptions = new ProcessOptions4(configFileJsonSchema1.ProcessOptions);
            ConvertOptions = new ConvertOptions3(configFileJsonSchema1.ConvertOptions);
            VerifyOptions = new VerifyOptions2(configFileJsonSchema1.VerifyOptions);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        // v2
        if (version == ConfigFileJsonSchema2.Version)
        {
            // ToolsOptions1
            // ProcessOptions2 *
            // ConvertOptions1 *
            // VerifyOptions1 *
            // MonitorOptions1
            ConfigFileJsonSchema2 configFileJsonSchema2 = this;

            // Upgrade to current version
#pragma warning disable CS0618 // Type or member is obsolete
            ProcessOptions = new ProcessOptions4(configFileJsonSchema2.ProcessOptions);
            ConvertOptions = new ConvertOptions3(configFileJsonSchema2.ConvertOptions);
            VerifyOptions = new VerifyOptions2(configFileJsonSchema2.VerifyOptions);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        // v3
        if (version == ConfigFileJsonSchema3.Version)
        {
            // ToolsOptions1
            // ProcessOptions3 *
            // ConvertOptions2 *
            // VerifyOptions2
            // MonitorOptions1
            ConfigFileJsonSchema3 configFileJsonSchema3 = this;

            // Upgrade to current version
#pragma warning disable CS0618 // Type or member is obsolete
            ProcessOptions = new ProcessOptions4(configFileJsonSchema3.ProcessOptions);
            ConvertOptions = new ConvertOptions3(configFileJsonSchema3.ConvertOptions);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        // v4:
        // ToolsOptions1
        // ProcessOptions4
        // ConvertOptions3
        // VerifyOptions2
        // MonitorOptions1

        // Set schema version to current and save original version
        SchemaVersion = Version;
        DeserializedVersion = version;
    }

    public void SetDefaults()
    {
        ToolsOptions.SetDefaults();
        ConvertOptions.SetDefaults();
        ProcessOptions.SetDefaults();
        MonitorOptions.SetDefaults();
        VerifyOptions.SetDefaults();
    }

    public bool VerifyValues() =>
        ToolsOptions.VerifyValues()
        && ConvertOptions.VerifyValues()
        && ProcessOptions.VerifyValues()
        && MonitorOptions.VerifyValues()
        && VerifyOptions.VerifyValues();

    public static void WriteDefaultsToFile(string path)
    {
        // Set defaults
        ConfigFileJsonSchema config = new();
        config.SetDefaults();

        // Write to file
        ToFile(path, config);
    }

    public static ConfigFileJsonSchema FromFile(string path) => FromJson(File.ReadAllText(path));

    public static ConfigFileJsonSchema OpenAndUpgrade(string path)
    {
        ConfigFileJsonSchema configJson = FromFile(path);
        if (configJson.DeserializedVersion != Version)
        {
            Log.Warning(
                "Writing ConfigFileJsonSchema upgraded from version {LoadedVersion} to {CurrentVersion}, {FileName}",
                configJson.DeserializedVersion,
                Version,
                path
            );
            ToFile(path, configJson);
        }
        return configJson;
    }

    public static void ToFile(string path, ConfigFileJsonSchema json)
    {
        // Set the schema version to the current version
        json.SchemaVersion = Version;

        // Write JSON to file
        File.WriteAllText(path, ToJson(json));
    }

    private static string ToJson(ConfigFileJsonSchema json) =>
        JsonSerializer.Serialize(json, ConfigFileJsonContext.Default.ConfigFileJsonSchema4);

    // Will throw on failure to deserialize
    private static ConfigFileJsonSchema FromJson(string json)
    {
        // Deserialize the base class to get the schema version
        ConfigFileJsonSchemaBase configFileJsonSchemaBase =
            JsonSerializer.Deserialize(json, ConfigFileJsonContext.Default.ConfigFileJsonSchemaBase)
            ?? throw new JsonException("Failed to deserialize ConfigFileJsonSchemaBase");

        if (configFileJsonSchemaBase.SchemaVersion != Version)
        {
            Log.Warning(
                "Converting ConfigFileJsonSchema version from {JsonSchemaVersion} to {CurrentSchemaVersion}",
                configFileJsonSchemaBase.SchemaVersion,
                Version
            );
        }

        // Deserialize the correct version
        switch (configFileJsonSchemaBase.SchemaVersion)
        {
            case ConfigFileJsonSchema1.Version:
                ConfigFileJsonSchema1 configFileJsonSchema1 =
                    JsonSerializer.Deserialize(
                        json,
                        ConfigFileJsonContext.Default.ConfigFileJsonSchema1
                    ) ?? throw new JsonException("Failed to deserialize ConfigFileJsonSchema1");
                return new ConfigFileJsonSchema(configFileJsonSchema1);
            case ConfigFileJsonSchema2.Version:
                ConfigFileJsonSchema2 configFileJsonSchema2 =
                    JsonSerializer.Deserialize(
                        json,
                        ConfigFileJsonContext.Default.ConfigFileJsonSchema2
                    ) ?? throw new JsonException("Failed to deserialize ConfigFileJsonSchema2");
                return new ConfigFileJsonSchema(configFileJsonSchema2);
            case ConfigFileJsonSchema3.Version:
                ConfigFileJsonSchema3 configFileJsonSchema3 =
                    JsonSerializer.Deserialize(
                        json,
                        ConfigFileJsonContext.Default.ConfigFileJsonSchema3
                    ) ?? throw new JsonException("Failed to deserialize ConfigFileJsonSchema3");
                return new ConfigFileJsonSchema(configFileJsonSchema3);
            case Version:
                ConfigFileJsonSchema configFileJsonSchema4 =
                    JsonSerializer.Deserialize(
                        json,
                        ConfigFileJsonContext.Default.ConfigFileJsonSchema4
                    ) ?? throw new JsonException("Failed to deserialize ConfigFileJsonSchema4");
                return configFileJsonSchema4;
            default:
                throw new NotSupportedException(
                    $"Unsupported schema version: {configFileJsonSchemaBase.SchemaVersion}"
                );
        }
    }

    public static void WriteSchemaToFile(string path)
    {
        // Create JSON schema
        JsonNode schemaNode = ConfigFileJsonContext.Default.Options.GetJsonSchemaAsNode(
            typeof(ConfigFileJsonSchema)
        );
        string schemaJson = schemaNode.ToJsonString(ConfigFileJsonContext.Default.Options);

        // Write to file
        File.WriteAllText(path, schemaJson);
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
[JsonSerializable(typeof(ConfigFileJsonSchemaBase))]
[JsonSerializable(typeof(ConfigFileJsonSchema1))]
[JsonSerializable(typeof(ConfigFileJsonSchema2))]
[JsonSerializable(typeof(ConfigFileJsonSchema3))]
[JsonSerializable(typeof(ConfigFileJsonSchema))]
internal partial class ConfigFileJsonContext : JsonSerializerContext;
