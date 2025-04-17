using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlexCleaner;

public record ProcessResultJsonSchema
{
    public class ToolVersion
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MediaTool.ToolFamily Tool { get; set; }
        public string Version { get; set; }
    }

    public class ProcessResult
    {
        public bool Result { get; set; }
        public string OriginalFileName { get; set; }
        public string NewFileName { get; set; }
        public bool Modified { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SidecarFile.StatesType State { get; set; }
    }

    public void SetVersionInfo()
    {
        AppVersion = AssemblyVersion.GetDetailedVersion();
        OSVersion = RuntimeInformation.OSDescription;

        Tools.GetToolFamilyList().ForEach(tool =>
        {
            ToolVersions.Add(new ToolVersion
            {
                Tool = tool.GetToolFamily(),
                Version = tool.Info.Version
            });
        });
    }

    [DefaultValue(0)]
    [JsonPropertyOrder(-2)]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public const int CurrentSchemaVersion = 1;

    public string AppVersion { get; set; }
    public string OSVersion { get; set; }
    public List<ToolVersion> ToolVersions { get; } = [];
    public List<ProcessResult> Results { get; } = [];

    public static ProcessResultJsonSchema FromFile(string path) => FromJson(File.ReadAllText(path));

    public static void ToFile(string path, ProcessResultJsonSchema json) => File.WriteAllText(path, ToJson(json));

    private static string ToJson(ProcessResultJsonSchema tools) => JsonSerializer.Serialize(tools, ConfigFileJsonSchema.JsonWriteOptions);

    public static ProcessResultJsonSchema FromJson(string json) => JsonSerializer.Deserialize<ProcessResultJsonSchema>(json, ConfigFileJsonSchema.JsonReadOptions);
}
