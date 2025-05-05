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
    [DefaultValue(0)]
    [JsonPropertyOrder(-2)]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public const int CurrentSchemaVersion = 2;

    public DateTime LastCheck { get; set; }

    public List<MediaToolInfo> Tools { get; } = [];

    public MediaToolInfo GetToolInfo(MediaTool mediaTool) =>
        Tools.FirstOrDefault(t => t.ToolFamily == mediaTool.GetToolFamily());

    public static ToolInfoJsonSchema FromFile(string path) => FromJson(File.ReadAllText(path));

    public static void ToFile(string path, ToolInfoJsonSchema json) =>
        File.WriteAllText(path, ToJson(json));

    private static string ToJson(ToolInfoJsonSchema tools) =>
        JsonSerializer.Serialize(tools, ConfigFileJsonSchema.JsonWriteOptions);

    public static ToolInfoJsonSchema FromJson(string json) =>
        JsonSerializer.Deserialize<ToolInfoJsonSchema>(json, ConfigFileJsonSchema.JsonReadOptions);

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
