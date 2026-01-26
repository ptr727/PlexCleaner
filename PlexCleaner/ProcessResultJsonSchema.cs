using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        JsonSerializer.Serialize(tools, ProcessResultJsonContext.Default.ProcessResultJsonSchema);

    // Will throw on failure to deserialize
    public static ProcessResultJsonSchema FromJson(string json) =>
        JsonSerializer.Deserialize(json, ProcessResultJsonContext.Default.ProcessResultJsonSchema)
        ?? throw new JsonException("Failed to deserialize ProcessResultJsonSchema");

    public class ToolVersion
    {
        [JsonConverter(typeof(JsonStringEnumConverter<MediaTool.ToolFamily>))]
        public MediaTool.ToolFamily Tool { get; set; }

        public string Version { get; set; } = string.Empty;
    }

    public class Version
    {
        public string Application { get; set; } = string.Empty;
        public string Runtime { get; set; } = string.Empty;
        public string OS { get; set; } = string.Empty;
        public List<ToolVersion> Tools { get; } = [];
    }

    public class ProcessResult
    {
        public bool Result { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string NewFileName { get; set; } = string.Empty;
        public bool Modified { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter<SidecarFile.StatesType>))]
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
[JsonSerializable(typeof(ProcessResultJsonSchema))]
internal partial class ProcessResultJsonContext : JsonSerializerContext;
