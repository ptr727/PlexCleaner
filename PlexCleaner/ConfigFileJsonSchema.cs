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
    public int SchemaVersion { get; set; } = ConfigFileJsonSchema4.Version;

    // Schema
    public const string SchemaUri = "https://raw.githubusercontent.com/ptr727/PlexCleaner/main/PlexCleaner.schema.json";
}

// v1
public record ConfigFileJsonSchema1 : ConfigFileJsonSchemaBase
{
    public const int Version = 1;

    [Required]
    [JsonProperty(Order = 1)]
    public ToolsOptions1 ToolsOptions { get; protected set; } = new();

    // v2 : Replaced with ProcessOptions2
    [Obsolete]
    [JsonProperty(Order = 2)]
    internal ProcessOptions1 ProcessOptions { get; set; } = new();

    // v3 : Replaced with ConvertOptions2
    [Obsolete]
    [JsonProperty(Order = 3)]
    internal ConvertOptions1 ConvertOptions { get; set; } = new();

    // v3 : Replaced with VerifyOptions2
    [Obsolete]
    [JsonProperty(Order = 4)]
    internal VerifyOptions1 VerifyOptions { get; set; } = new();

    [Required]
    [JsonProperty(Order = 5)]
    public MonitorOptions1 MonitorOptions { get; protected set; } = new();
}

// v2
public record ConfigFileJsonSchema2 : ConfigFileJsonSchema1
{
    public new const int Version = 2;

    public ConfigFileJsonSchema2() { }
    public ConfigFileJsonSchema2(ConfigFileJsonSchema1 configFileJsonSchema1) : base(configFileJsonSchema1) { }

    // v2 : Added
    // v3 : Replaced with ProcessOptions3
    [Obsolete]
    [JsonProperty(Order = 2)]
    internal new ProcessOptions2 ProcessOptions { get; set; } = new();
}

// v3
public record ConfigFileJsonSchema3 : ConfigFileJsonSchema2
{
    public new const int Version = 3;

    public ConfigFileJsonSchema3() { }

    public ConfigFileJsonSchema3(ConfigFileJsonSchema1 configFileJsonSchema1) : base(configFileJsonSchema1) { }
    public ConfigFileJsonSchema3(ConfigFileJsonSchema2 configFileJsonSchema2) : base(configFileJsonSchema2) { }

    // v3 : Added
    // v4 : Replaced with ProcessOptions4
    [Obsolete]
    [JsonProperty(Order = 2)]
    internal new ProcessOptions3 ProcessOptions { get; set; } = new();

    // v3 : Added
    [Required]
    [JsonProperty(Order = 3)]
    public new ConvertOptions2 ConvertOptions { get; protected set; } = new();

    // v3 : Added
    [Required]
    [JsonProperty(Order = 4)]
    public new VerifyOptions2 VerifyOptions { get; protected set; } = new();
}

// v4
public record ConfigFileJsonSchema4 : ConfigFileJsonSchema3
{
    public new const int Version = 4;

    public ConfigFileJsonSchema4() { }

    public ConfigFileJsonSchema4(ConfigFileJsonSchema1 configFileJsonSchema1) : base(configFileJsonSchema1)
    {
        Upgrade(ConfigFileJsonSchema1.Version);
    }
    public ConfigFileJsonSchema4(ConfigFileJsonSchema2 configFileJsonSchema2) : base(configFileJsonSchema2)
    {
        Upgrade(ConfigFileJsonSchema2.Version);
    }
    public ConfigFileJsonSchema4(ConfigFileJsonSchema3 configFileJsonSchema3) : base(configFileJsonSchema3)
    {
        Upgrade(ConfigFileJsonSchema3.Version);
    }

    // v4 : Added
    [Required]
    [JsonProperty(Order = 2)]
    public new ProcessOptions4 ProcessOptions { get; protected set; } = new();

#pragma warning disable CS0612 // Type or member is obsolete
    protected void Upgrade(int version)
    {
        // v1
        if (version == ConfigFileJsonSchema1.Version)
        {
            // Get v1 schema
            ConfigFileJsonSchema1 configFileJsonSchema1 = this;

            // Upgrade to current version
            // ToolsOptions = new ToolsOptions(configFileJsonSchema1.ToolsOptions);
            ConvertOptions = new ConvertOptions2(configFileJsonSchema1.ConvertOptions);
            ProcessOptions = new ProcessOptions4(configFileJsonSchema1.ProcessOptions);
            VerifyOptions = new VerifyOptions2(configFileJsonSchema1.VerifyOptions);
            // MonitorOptions = new MonitorOptions(configFileJsonSchema1.MonitorOptions);
        }

        // v2
        if (version == ConfigFileJsonSchema2.Version)
        {
            // Get v2 schema
            ConfigFileJsonSchema2 configFileJsonSchema2 = this;

            // Upgrade to current version
            // ToolsOptions = new ToolsOptions(configFileJsonSchema2.ToolsOptions);
            ConvertOptions = new ConvertOptions2(configFileJsonSchema2.ConvertOptions);
            ProcessOptions = new ProcessOptions4(configFileJsonSchema2.ProcessOptions);
            VerifyOptions = new VerifyOptions2(configFileJsonSchema2.VerifyOptions);
            // MonitorOptions = new MonitorOptions(configFileJsonSchema2.MonitorOptions);
        }

        // v3
        if (version == ConfigFileJsonSchema3.Version)
        {
            // Get v3 schema
            ConfigFileJsonSchema3 configFileJsonSchema3 = this;

            // Upgrade to current version
            // ToolsOptions = new ToolsOptions(configFileJsonSchema3.ToolsOptions);
            ConvertOptions = new ConvertOptions2(configFileJsonSchema3.ConvertOptions);
            ProcessOptions = new ProcessOptions4(configFileJsonSchema3.ProcessOptions);
            VerifyOptions = new VerifyOptions2(configFileJsonSchema3.VerifyOptions);
            // MonitorOptions = new MonitorOptions(configFileJsonSchema3.MonitorOptions);
        }

        // v4
    }
#pragma warning restore CS0612 // Type or member is obsolete

    public void SetDefaults()
    {
        ToolsOptions.SetDefaults();
        ConvertOptions.SetDefaults();
        ProcessOptions.SetDefaults();
        MonitorOptions.SetDefaults();
        VerifyOptions.SetDefaults();
    }

    public bool VerifyValues()
    {
        if (!ToolsOptions.VerifyValues() ||
            !ConvertOptions.VerifyValues() ||
            !ProcessOptions.VerifyValues() ||
            !MonitorOptions.VerifyValues() ||
            !VerifyOptions.VerifyValues())
        {
            return false;
        }

        return true;
    }

    public static void WriteDefaultsToFile(string path)
    {
        // Set defaults
        ConfigFileJsonSchema4 config = new();
        config.SetDefaults();

        // Write to file
        ToFile(path, config);
    }

    public static ConfigFileJsonSchema4 FromFile(string path)
    {
        return FromJson(File.ReadAllText(path));
    }

    public static void ToFile(string path, ConfigFileJsonSchema4 json)
    {
        // Set the schema version to the current version
        json.SchemaVersion = Version;

        // Write JSON to file
        File.WriteAllText(path, ToJson(json));
    }

    private static string ToJson(ConfigFileJsonSchema4 settings)
    {
        return JsonConvert.SerializeObject(settings, Settings);
    }

    private static ConfigFileJsonSchema4 FromJson(string json)
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
        return configFileJsonSchemaBase.SchemaVersion switch
        {
            // Version 1
            ConfigFileJsonSchema1.Version => new ConfigFileJsonSchema4(JsonConvert.DeserializeObject<ConfigFileJsonSchema1>(json, Settings)),
            // Version 2
            ConfigFileJsonSchema2.Version => new ConfigFileJsonSchema4(JsonConvert.DeserializeObject<ConfigFileJsonSchema2>(json, Settings)),
            // Version 3
            ConfigFileJsonSchema3.Version => new ConfigFileJsonSchema4(JsonConvert.DeserializeObject<ConfigFileJsonSchema3>(json, Settings)),
            // Current version
            Version => JsonConvert.DeserializeObject<ConfigFileJsonSchema4>(json, Settings),
            // Unknown version
            _ => throw new NotImplementedException(),
        };
    }

    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
        StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
        NullValueHandling = NullValueHandling.Ignore,
        // Reuse the already created objects, required for HashSet() case insensitive comparison operator
        ObjectCreationHandling = ObjectCreationHandling.Reuse
        // TODO: Add TraceWriter to log to Serilog
        // TODO: Add a custom resolver to control serialization of deprecated attributes, vs. using internal
        // https://stackoverflow.com/questions/11564091/making-a-property-deserialize-but-not-serialize-with-json-net
        // TODO: .NET 8 supports populating readonly properties, no need for set
        // https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8/runtime#read-only-properties
    };

    public static void WriteSchemaToFile(string path)
    {
        // Create JSON schema
        var generator = new JSchemaGenerator
        {
            // TODO: How to make the default as required, and just mark individual items as not required?
            DefaultRequired = Required.Default
        };
        var schema = generator.Generate(typeof(ConfigFileJsonSchema4));
        schema.Title = "PlexCleaner Configuration Schema";
        schema.SchemaVersion = new Uri("http://json-schema.org/draft-06/schema");
        schema.Id = new Uri(SchemaUri);

        // Write to file
        File.WriteAllText(path, schema.ToString());
    }
}
