using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlexCleaner;

public record ProcessResultJsonSchema
{
    [DefaultValue(0)]
    [JsonPropertyOrder(-2)]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public const int CurrentSchemaVersion = 1;

    public List<ProcessResult> Results { get; } = [];

    public static ProcessResultJsonSchema FromFile(string path) => FromJson(File.ReadAllText(path));

    public static void ToFile(string path, ProcessResultJsonSchema json) => File.WriteAllText(path, ToJson(json));

    private static string ToJson(ProcessResultJsonSchema tools) => JsonSerializer.Serialize(tools, ConfigFileJsonSchema.JsonWriteOptions);

    public static ProcessResultJsonSchema FromJson(string json) => JsonSerializer.Deserialize<ProcessResultJsonSchema>(json, ConfigFileJsonSchema.JsonReadOptions);
}
