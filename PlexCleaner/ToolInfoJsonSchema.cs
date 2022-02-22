using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace PlexCleaner;

public class ToolInfoJsonSchema
{
    [DefaultValue(0)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public const int CurrentSchemaVersion = 2;

    public DateTime LastCheck { get; set; }

    public List<MediaToolInfo> Tools { get; } = new();

    public MediaToolInfo GetToolInfo(MediaTool mediaTool)
    {
        // Match tool by family
        return Tools.FirstOrDefault(t => t.ToolFamily == mediaTool.GetToolFamily());
    }

    public MediaToolInfo GetToolInfo(MediaToolInfo mediaToolInfo)
    {
        // Match tool by family
        return Tools.FirstOrDefault(t => t.ToolFamily == mediaToolInfo.ToolFamily);
    }

    public static ToolInfoJsonSchema FromFile(string path)
    {
        return FromJson(File.ReadAllText(path));
    }

    public static void ToFile(string path, ToolInfoJsonSchema json)
    {
        File.WriteAllText(path, ToJson(json));
    }

    private static string ToJson(ToolInfoJsonSchema tools)
    {
        return JsonConvert.SerializeObject(tools, Settings);
    }

    public static ToolInfoJsonSchema FromJson(string json)
    {
        return JsonConvert.DeserializeObject<ToolInfoJsonSchema>(json, Settings);
    }

    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented
    };

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
        Log.Logger.Error("Schema version {SchemaVersion} not supported, run the 'checkfornewtools' command", json.SchemaVersion);

        return false;
    }
}