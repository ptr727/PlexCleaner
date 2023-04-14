using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema.Generation;
using Serilog;

namespace PlexCleaner;

// Base
public record ConfigFileJsonSchemaBase
{
    // TODO: How to set the $schema through e.g. attributes on the class?
    // https://stackoverflow.com/questions/71625019/how-to-inject-the-json-schema-value-during-newtonsoft-jsonconvert-serializeobje
    // Schema reference
    [JsonProperty(PropertyName = "$schema", Order = -3)]
    public string Schema { get; } = SchemaUri;

    // Default to 0 if no value specified, and always write the version first
    [DefaultValue(0)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate, Order = -2)]
    public int SchemaVersion { get; set; } = ConfigFileJsonSchema.Version;

    // Schema
    public const string SchemaUri = "https://raw.githubusercontent.com/ptr727/PlexCleaner/main/PlexCleaner.schema.json";
}

// v1
[Obsolete]
public record ConfigFileJsonSchema1 : ConfigFileJsonSchemaBase
{
    // Deprecated
    [Obsolete]
    internal ConvertOptions1 ConvertOptions { get; set; } = new();
    [Obsolete]
    internal ProcessOptions1 ProcessOptions { get; set; } = new();

    [Required]
    [JsonProperty(Order = 1)]
    public ToolsOptions ToolsOptions { get; protected set; } = new();

    [Required]
    [JsonProperty(Order = 5)]
    public MonitorOptions MonitorOptions { get; protected set; } = new();

    [Required]
    [JsonProperty(Order = 4)]
    public VerifyOptions VerifyOptions { get; protected set; } = new();

    // v1
    public const int Version = 1;
}

// v2
[Obsolete]
public record ConfigFileJsonSchema2 : ConfigFileJsonSchema1
{
    public ConfigFileJsonSchema2() { }

    // Copy from v1
    [Obsolete]
    public ConfigFileJsonSchema2(ConfigFileJsonSchema1 configFileJsonSchema1) : base(configFileJsonSchema1)
    {
        Upgrade(configFileJsonSchema1);
    }

    [Obsolete]
    protected void Upgrade(ConfigFileJsonSchema1 configFileJsonSchema1)
    {
        // Upgrade v1 to v2
        ProcessOptions = new ProcessOptions2(configFileJsonSchema1.ProcessOptions);
    }

    [Obsolete] 
    internal new ProcessOptions2 ProcessOptions { get; set; } = new();

    // v2
    public new const int Version = 2;
}

// v3
#pragma warning disable CS0612 // Type or member is obsolete
public record ConfigFileJsonSchema : ConfigFileJsonSchema2
#pragma warning restore CS0612 // Type or member is obsolete
{
    public ConfigFileJsonSchema() { }

    [Obsolete]
    public ConfigFileJsonSchema(ConfigFileJsonSchema1 configFileJsonSchema1) : base(configFileJsonSchema1)
    {
        Upgrade(configFileJsonSchema1);
    }

    [Obsolete]
    public ConfigFileJsonSchema(ConfigFileJsonSchema2 configFileJsonSchema2) : base(configFileJsonSchema2)
    {
        Upgrade(configFileJsonSchema2);
    }

    [Obsolete]
    protected void Upgrade(ConfigFileJsonSchema2 configFileJsonSchema2)
    {
        // Upgrade v1 to v2
        Upgrade((ConfigFileJsonSchema1)configFileJsonSchema2);

        // Upgrade v2 to v3
        ConvertOptions = new ConvertOptions(configFileJsonSchema2.ConvertOptions);
        ProcessOptions = new ProcessOptions(configFileJsonSchema2.ProcessOptions);
    }

    [Required]
    [JsonProperty(Order = 3)]
    public new ConvertOptions ConvertOptions { get; protected set; } = new();

    [Required]
    [JsonProperty(Order = 2)]
    public new ProcessOptions ProcessOptions { get; protected set; } = new();

    // v3
    public new const int Version = 3;

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

        if (configFileJsonSchemaBase.SchemaVersion != Version)
        {
            Log.Logger.Warning("Upgrading ConfigFileJsonSchema from {JsonSchemaVersion} to {CurrentSchemaVersion}", configFileJsonSchemaBase.SchemaVersion, Version);
        }

        // Deserialize the correct version
        switch (configFileJsonSchemaBase.SchemaVersion)
        {
#pragma warning disable CS0612 // Type or member is obsolete
            // Version 1
            case ConfigFileJsonSchema1.Version:
                return new ConfigFileJsonSchema(JsonConvert.DeserializeObject<ConfigFileJsonSchema1>(json, Settings));
            // Version 2
            case ConfigFileJsonSchema2.Version:
                return new ConfigFileJsonSchema(JsonConvert.DeserializeObject<ConfigFileJsonSchema2>(json, Settings));
#pragma warning restore CS0612 // Type or member is obsolete
            // Current version
            case Version:
                return JsonConvert.DeserializeObject<ConfigFileJsonSchema>(json, Settings);
            // Unknown version
            default:
                throw new NotImplementedException();
        }
    }

    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
        StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
        NullValueHandling = NullValueHandling.Ignore,
        // Reuse the already created objects, required for HashSet() case insensitive comparison operator
        ObjectCreationHandling = ObjectCreationHandling.Reuse
        // TODO: Add TraceWriter to log to Serilog
        // TODO: Add a custom resolver to control serialization od deprecated attributes, vs. using internal
        // https://stackoverflow.com/questions/11564091/making-a-property-deserialize-but-not-serialize-with-json-net
    };

    public static void WriteSchemaToFile(string path)
    {
        // Create JSON schema
        var generator = new JSchemaGenerator
        {
            // TODO: How to make the default as required, and just mark individual items as not required?
            DefaultRequired = Required.Default
        };
        var schema = generator.Generate(typeof(ConfigFileJsonSchema));
        schema.Title = "PlexCleaner Configuration Schema";
        schema.SchemaVersion = new Uri(@"http://json-schema.org/draft-06/schema");
        schema.Id = new Uri(SchemaUri);
        
        // Write to file
        File.WriteAllText(path, schema.ToString());
    }
}
