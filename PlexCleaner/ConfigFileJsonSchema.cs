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
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Json.Schema;
using Json.Schema.Generation;
using Serilog;

namespace PlexCleaner;

// Base
public record ConfigFileJsonSchemaBase
{
    [JsonPropertyName("$schema")]
    [JsonPropertyOrder(-3)]
    public string Schema { get; } = SchemaUri;

    [JsonRequired]
    [JsonPropertyOrder(-2)]
    public int SchemaVersion { get; set; } = ConfigFileJsonSchema.Version;

    // Schema Id
    protected const string SchemaUri =
        "https://raw.githubusercontent.com/ptr727/PlexCleaner/main/PlexCleaner.schema.json";
}

// v1
public record ConfigFileJsonSchema1 : ConfigFileJsonSchemaBase
{
    protected const int Version = 1;

    [JsonRequired]
    [JsonPropertyOrder(1)]
    public ToolsOptions1 ToolsOptions { get; set; } = new();

    // v2 : Replaced with ProcessOptions2
    [Obsolete("Replaced with ProcessOptions2 in v2.")]
    [JsonExclude]
    public ProcessOptions1 ProcessOptions { get; set; } = new();

    // v3 : Replaced with ConvertOptions2
    [Obsolete("Replaced with ConvertOptions2 in v3.")]
    [JsonExclude]
    public ConvertOptions1 ConvertOptions { get; set; } = new();

    // v3 : Replaced with VerifyOptions2
    [Obsolete("Replaced with VerifyOptions2 in v3.")]
    [JsonExclude]
    public VerifyOptions1 VerifyOptions { get; set; } = new();

    [JsonRequired]
    [JsonPropertyOrder(5)]
    public MonitorOptions1 MonitorOptions { get; set; } = new();
}

// v2
public record ConfigFileJsonSchema2 : ConfigFileJsonSchema1
{
    protected new const int Version = 2;

    public ConfigFileJsonSchema2() { }

    public ConfigFileJsonSchema2(ConfigFileJsonSchema1 configFileJsonSchema1)
        : base(configFileJsonSchema1) { }

    // v2 : Added
    // v3 : Replaced with ProcessOptions3
    [Obsolete("Replaced with ProcessOptions3 in v3.")]
    [JsonExclude]
    public new ProcessOptions2 ProcessOptions { get; set; } = new();
}

// v3
public record ConfigFileJsonSchema3 : ConfigFileJsonSchema2
{
    protected new const int Version = 3;

    public ConfigFileJsonSchema3() { }

    public ConfigFileJsonSchema3(ConfigFileJsonSchema1 configFileJsonSchema1)
        : base(configFileJsonSchema1) { }

    public ConfigFileJsonSchema3(ConfigFileJsonSchema2 configFileJsonSchema2)
        : base(configFileJsonSchema2) { }

    // v3 : Added
    // v4 : Replaced with ProcessOptions4
    [Obsolete("Replaced with ProcessOptions4 in v4.")]
    [JsonExclude]
    public new ProcessOptions3 ProcessOptions { get; set; } = new();

    // v3 : Added
    // v4 : Replaced with ConvertOptions3
    [Obsolete("Replaced with ConvertOptions3 in v4.")]
    [JsonExclude]
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
        // v4:
        // ToolsOptions1
        // ProcessOptions4
        // ConvertOptions3
        // VerifyOptions2
        // MonitorOptions1

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

        // v4
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

    public static void ToFile(string path, ConfigFileJsonSchema json)
    {
        // Set the schema version to the current version
        json.SchemaVersion = Version;

        // Write JSON to file
        File.WriteAllText(path, ToJson(json));
    }

    private static string ToJson(ConfigFileJsonSchema json) =>
        JsonSerializer.Serialize(json, JsonWriteOptions);

    private static ConfigFileJsonSchema FromJson(string json)
    {
        // Deserialize the base class to get the schema version
        ConfigFileJsonSchemaBase configFileJsonSchemaBase =
            JsonSerializer.Deserialize<ConfigFileJsonSchemaBase>(json, JsonReadOptions);
        if (configFileJsonSchemaBase == null)
        {
            return null;
        }

        if (configFileJsonSchemaBase.SchemaVersion != Version)
        {
            Log.Warning(
                "Converting ConfigFileJsonSchema from {JsonSchemaVersion} to {CurrentSchemaVersion}",
                configFileJsonSchemaBase.SchemaVersion,
                Version
            );
        }

        // Deserialize the correct version
        return configFileJsonSchemaBase.SchemaVersion switch
        {
            ConfigFileJsonSchema1.Version => new ConfigFileJsonSchema(
                JsonSerializer.Deserialize<ConfigFileJsonSchema1>(json, JsonReadOptions)
            ),
            ConfigFileJsonSchema2.Version => new ConfigFileJsonSchema(
                JsonSerializer.Deserialize<ConfigFileJsonSchema2>(json, JsonReadOptions)
            ),
            ConfigFileJsonSchema3.Version => new ConfigFileJsonSchema(
                JsonSerializer.Deserialize<ConfigFileJsonSchema3>(json, JsonReadOptions)
            ),
            Version => JsonSerializer.Deserialize<ConfigFileJsonSchema>(json, JsonReadOptions),
            _ => throw new NotImplementedException(),
        };
    }

    public static void WriteSchemaToFile(string path)
    {
        // TODO: Use refiners to filter out [Obsolete] attributes
        // Use [Obsolete] and [Json.Schema.Generation.JsonExclude] on all obsolete attributes
        // https://github.com/gregsdennis/json-everything/issues/703
        // https://docs.json-everything.net/schema/schemagen/schema-generation/#schema-schemagen-refiners

        // TODO: Avoid using [Required] as it is defined in System.ComponentModel.DataAnnotations and in Json.Schema.Generation, avoid "using Json.Schema.Generation"
        // Use [JsonRequired] on all must be serialized attributes.
        // https://github.com/gregsdennis/json-everything/issues/702

        // Create JSON schema
        // TODO: Schema version should really be set based on generator internals
        const string schemaVersion = "https://json-schema.org/draft/2020-12/schema";
        JsonSchema schemaBuilder = new JsonSchemaBuilder()
            .FromType<ConfigFileJsonSchema>(
                new SchemaGeneratorConfiguration { PropertyOrder = PropertyOrder.ByName }
            )
            .Title("PlexCleaner Configuration Schema")
            .Id(new Uri(SchemaUri))
            .Schema(new Uri(schemaVersion))
            .Build();
        string jsonSchema = JsonSerializer.Serialize(schemaBuilder, JsonWriteOptions);

        // Write to file
        File.WriteAllText(path, jsonSchema);
    }

    public static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        AllowTrailingCommas = true,
        IncludeFields = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        IncludeFields = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver().WithAddedModifier(
            ExcludeObsoletePropertiesModifier
        ),
        WriteIndented = true,
    };

    private static void ExcludeObsoletePropertiesModifier(JsonTypeInfo typeInfo)
    {
        // Only process objects
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        // Iterate over all properties
        foreach (JsonPropertyInfo property in typeInfo.Properties)
        {
            // Do not serialize [Obsolete] items
            if (property.AttributeProvider?.IsDefined(typeof(ObsoleteAttribute), true) == true)
            {
                property.ShouldSerialize = (_, _) => false;
            }
        }
    }
}
