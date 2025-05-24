#region

using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

#endregion

namespace PlexCleaner;

public record ProcessResultJsonSchema
{
    public const int CurrentSchemaVersion = 1;

    [DefaultValue(0)]
    [JsonPropertyOrder(-2)]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public Version Versions { get; } = new();
    public Result Results { get; } = new();

    public void SetVersionInfo()
    {
        Versions.Application = AssemblyVersion.GetAppVersion();
        Versions.Runtime = AssemblyVersion.GetRuntimeVersion();
        Versions.OS = RuntimeInformation.OSDescription;

        Tools
            .GetToolFamilyList()
            .ForEach(tool =>
            {
                Versions.Tools.Add(
                    new ToolVersion { Tool = tool.GetToolFamily(), Version = tool.Info.Version }
                );
            });
    }

    public static ProcessResultJsonSchema FromFile(string path) => FromJson(File.ReadAllText(path));

    public static void ToFile(string path, ProcessResultJsonSchema json) =>
        File.WriteAllText(path, ToJson(json));

    private static string ToJson(ProcessResultJsonSchema tools) =>
        JsonSerializer.Serialize(tools, ConfigFileJsonSchema.JsonWriteOptions);

    public static ProcessResultJsonSchema FromJson(string json) =>
        JsonSerializer.Deserialize<ProcessResultJsonSchema>(
            json,
            ConfigFileJsonSchema.JsonReadOptions
        );

    public class ToolVersion
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MediaTool.ToolFamily Tool { get; set; }

        public string Version { get; set; }
    }

    public class Version
    {
        public string Application { get; set; }
        public string Runtime { get; set; }
        public string OS { get; set; }
        public List<ToolVersion> Tools { get; } = [];
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

    public class ProcessSummary
    {
        public int Total { get; set; }
        public List<string> Files { get; } = [];
    }

    public class Result
    {
        public ProcessSummary Errors { get; } = new();
        public ProcessSummary VerifyFailed { get; } = new();
        public ProcessSummary Modified { get; } = new();
        public List<ProcessResult> Results { get; } = [];
    }
}
