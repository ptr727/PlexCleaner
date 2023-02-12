using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema.Generation;

namespace PlexCleaner;

public class ConfigFileJsonSchemaBase
{
    // TODO: How to set the $schema throug e.g. attributes on the class?
    // https://stackoverflow.com/questions/71625019/how-to-inject-the-json-schema-value-during-newtonsoft-jsonconvert-serializeobje
    // Schema reference
    [JsonProperty(PropertyName = "$schema", Order = -3)]
    public string Schema { get; } = SchemaUri;

    // Default to 0 if no value specified, and always write the version first
    [DefaultValue(0)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate, Order = -2)]
    public int SchemaVersion { get; set; } = ConfigFileJsonSchema.Version;

    private const string SchemaUri = "https://raw.githubusercontent.com/ptr727/PlexCleaner/develop/PlexCleaner.schema.json";
}

[Obsolete("Replaced in Schema v2", false)]
public class ConfigFileJsonSchema1 : ConfigFileJsonSchemaBase
{
    public ToolsOptions ToolsOptions { get; set; } = new();
    public ConvertOptions1 ConvertOptions { get; set; } = new();
    public ProcessOptions1 ProcessOptions { get; set; } = new();
    public MonitorOptions MonitorOptions { get; set; } = new();
    public VerifyOptions VerifyOptions { get; set; } = new();

    public const int Version = 1;
}

[Obsolete("Replaced in Schema v3", false)]
public class ConfigFileJsonSchema2 : ConfigFileJsonSchemaBase
{
    public ToolsOptions ToolsOptions { get; set; } = new();
    public ConvertOptions1 ConvertOptions { get; set; } = new();
    public ProcessOptions ProcessOptions { get; set; } = new();
    public MonitorOptions MonitorOptions { get; set; } = new();
    public VerifyOptions VerifyOptions { get; set; } = new();

    public const int Version = 2;
}

public class ConfigFileJsonSchema : ConfigFileJsonSchemaBase
{
    public ConfigFileJsonSchema() { }

#pragma warning disable CS0618 // Type or member is obsolete
    public ConfigFileJsonSchema(ConfigFileJsonSchema1 configFileJsonSchema1)
#pragma warning restore CS0618 // Type or member is obsolete
    {
        // Keep the original schema version
        SchemaVersion = configFileJsonSchema1.SchemaVersion;

        // Assign same values
        ToolsOptions = configFileJsonSchema1.ToolsOptions;
        MonitorOptions = configFileJsonSchema1.MonitorOptions;
        VerifyOptions = configFileJsonSchema1.VerifyOptions;

        // Create current version from old version
        ConvertOptions = new ConvertOptions(configFileJsonSchema1.ConvertOptions);
        ProcessOptions = new ProcessOptions(configFileJsonSchema1.ProcessOptions);
    }

#pragma warning disable CS0618 // Type or member is obsolete
    public ConfigFileJsonSchema(ConfigFileJsonSchema2 configFileJsonSchema2)
#pragma warning restore CS0618 // Type or member is obsolete
    {
        // Keep the original schema version
        SchemaVersion = configFileJsonSchema2.SchemaVersion;

        // Assign same values
        ToolsOptions = configFileJsonSchema2.ToolsOptions;
        ProcessOptions = configFileJsonSchema2.ProcessOptions;
        MonitorOptions = configFileJsonSchema2.MonitorOptions;
        VerifyOptions = configFileJsonSchema2.VerifyOptions;

        // Create current version from old version
        ConvertOptions = new ConvertOptions(configFileJsonSchema2.ConvertOptions);
    }

    [Required]
    public ToolsOptions ToolsOptions { get; set; } = new();
    [Required]
    public ConvertOptions ConvertOptions { get; set; } = new();
    [Required]
    public ProcessOptions ProcessOptions { get; set; } = new();
    [Required]
    public MonitorOptions MonitorOptions { get; set; } = new();
    [Required]
    public VerifyOptions VerifyOptions { get; set; } = new();

    public const int Version = 3;

    public void SetDefaults()
    {
        ToolsOptions.SetDefaults();
        ConvertOptions.SetDefaults();
        ProcessOptions.SetDefaults();
        MonitorOptions.SetDefaults();
        VerifyOptions.SetDefaults();
    }

    public static void WriteDefaultsToFile(string path)
    {
        // Set defaults
        ConfigFileJsonSchema config = new();
        config.SetDefaults();

        // Write to file
        ToFile(path, config);
    }

    public static ConfigFileJsonSchema FromFile(string path)
    {
        return FromJson(File.ReadAllText(path));
    }

    public static void ToFile(string path, ConfigFileJsonSchema json)
    {
        // Set the schema to the current version
        json.SchemaVersion = Version;

        // Write JSON to file
        File.WriteAllText(path, ToJson(json));
    }

    private static string ToJson(ConfigFileJsonSchema settings)
    {
        return JsonConvert.SerializeObject(settings, Settings);
    }

    private static ConfigFileJsonSchema FromJson(string json)
    {
        // Deserialize the base class to get the schema version
        var configFileJsonSchemaBase = JsonConvert.DeserializeObject<ConfigFileJsonSchemaBase>(json, Settings);
        if (configFileJsonSchemaBase == null)
        {
            return null;
        }

        int schemaVersion = configFileJsonSchemaBase.SchemaVersion;

        // Deserialize the correct version
        switch (schemaVersion)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            // Version 1
            case ConfigFileJsonSchema1.Version:
                // Create current version from old version
                return new ConfigFileJsonSchema(JsonConvert.DeserializeObject<ConfigFileJsonSchema1>(json, Settings));
            // Version 2
            case ConfigFileJsonSchema2.Version:
                // Create current version from old version
                return new ConfigFileJsonSchema(JsonConvert.DeserializeObject<ConfigFileJsonSchema2>(json, Settings));
#pragma warning restore CS0618 // Type or member is obsolete
            // Current version
            case Version:
                return JsonConvert.DeserializeObject<ConfigFileJsonSchema>(json, Settings);
            // Unknown version
            default:
                throw new NotSupportedException(nameof(schemaVersion));
        }
    }

    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
        StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
        NullValueHandling = NullValueHandling.Ignore,
        // We expect containers to be cleared before deserializing
        // Make sure that collections are not read-only (get; set;) else deserialized values will be appended
        // https://stackoverflow.com/questions/35482896/clear-collections-before-adding-items-when-populating-existing-objects
        ObjectCreationHandling = ObjectCreationHandling.Replace
        // TODO: Add TraceWriter to log to Serilog
    };

    public static void WriteSchemaToFile(string path)
    {
        // Create JSON schema
        var generator = new JSchemaGenerator
        {
            // TODO: How can I make the default schema required, and just mark individual items as not required?
            DefaultRequired = Required.Default
        };
        var schema = generator.Generate(typeof(ConfigFileJsonSchema));
        schema.Title = "PlexCleaner Configuration Schema";
        schema.SchemaVersion = new Uri(@"http://json-schema.org/draft-06/schema");
        schema.Id = new Uri(@"https://raw.githubusercontent.com/ptr727/PlexCleaner/main/PlexCleaner.schema.json");
        
        // Write to file
        File.WriteAllText(path, schema.ToString());
    }
}
