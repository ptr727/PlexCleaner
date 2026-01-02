using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace PlexCleaner;

public class ToolInfoJsonSchema
{
    public const int CurrentSchemaVersion = 2;

    [DefaultValue(0)]
    [JsonPropertyOrder(-2)]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public DateTime LastCheck { get; set; }

    public List<MediaToolInfo> Tools { get; } = [];

    public MediaToolInfo GetToolInfo(MediaTool mediaTool) =>
        Tools.FirstOrDefault(t => t.ToolFamily == mediaTool.GetToolFamily());

    public static ToolInfoJsonSchema FromFile(string path) => FromJson(File.ReadAllText(path));

    public static void ToFile(string path, ToolInfoJsonSchema json) =>
        File.WriteAllText(path, ToJson(json));

    private static string ToJson(ToolInfoJsonSchema tools) =>
        JsonSerializer.Serialize(tools, ToolInfoJsonContext.Default.ToolInfoJsonSchema);

    public static ToolInfoJsonSchema FromJson(string json) =>
        JsonSerializer.Deserialize(json, ToolInfoJsonContext.Default.ToolInfoJsonSchema);

    public static bool Upgrade(ToolInfoJsonSchema json)
    {
        // Current version
        if (json.SchemaVersion == CurrentSchemaVersion)
        {
            return true;
        }

        // Unspecified / v0 to v2 was the first set version
        // Tools changed from List<ToolInfo> to List<MediaToolInfo>
        // Not worth the trouble to migrate, just get new tools
        Log.Error(
            "Schema version {SchemaVersion} not supported, run the 'checkfornewtools' command",
            json.SchemaVersion
        );

        return false;
    }
}

// TODO: TypeInfoResolver = SourceGenerationContext.Default.WithAddedModifier(ExcludeObsoletePropertiesModifier),
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
[JsonSerializable(typeof(ToolInfoJsonSchema))]
internal partial class ToolInfoJsonContext : JsonSerializerContext;
