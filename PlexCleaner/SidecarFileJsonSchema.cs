using Newtonsoft.Json;
using Serilog;
using System;
using System.ComponentModel;
using System.IO;

namespace PlexCleaner;

public class SidecarFileJsonSchema
{
    [DefaultValue(0)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public const int CurrentSchemaVersion = 4;

    public DateTime MediaLastWriteTimeUtc { get; set; }
    public long MediaLength { get; set; }
    public string MediaHash { get; set; }

    public string FfProbeToolVersion { get; set; }
    public string FfProbeInfoData { get; set; }

    public string MkvMergeToolVersion { get; set; }
    public string MkvMergeInfoData { get; set; }

    public string MediaInfoToolVersion { get; set; }
    public string MediaInfoData { get; set; }

    public SidecarFile.States State { get; set; }

    [Obsolete("Replaced with State in v4", false)]
    private bool Verified { get; set; }
    [Obsolete("Replaced with FfProbeToolVersion in v3", false)]
    private string FfMpegToolVersion { get; set; }
    [Obsolete("Replaced with MkvMergeToolVersion in v3", false)]
    private string MkvToolVersion { get; set; }

    public static SidecarFileJsonSchema FromFile(string path)
    {
        return FromJson(File.ReadAllText(path));
    }

    public static void ToFile(string path, SidecarFileJsonSchema json)
    {
        File.WriteAllText(path, ToJson(json));
    }

    public static string ToJson(SidecarFileJsonSchema json) =>
        JsonConvert.SerializeObject(json, Settings);

    public static SidecarFileJsonSchema FromJson(string json) =>
        JsonConvert.DeserializeObject<SidecarFileJsonSchema>(json, Settings);

    public static SidecarFileJsonSchema FromJson(JsonTextReader reader)
    {
        var serializer = JsonSerializer.Create(Settings);
        return (SidecarFileJsonSchema)serializer.Deserialize(reader, typeof(SidecarFileJsonSchema));
    }

    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented
    };

    public static bool Upgrade(SidecarFileJsonSchema json)
    {
        // Current version
        if (json.SchemaVersion == CurrentSchemaVersion)
            return true;

        // Version 0 in undetermined
        if (json.SchemaVersion == 0)
        {
            Log.Logger.Error("Sidecar schema not upgradeable : {SchemaVersion} < 1", json.SchemaVersion);
            return false;
        }

        // v1 Schema
        // FfIdetInfoData was todo in v1, removed in v2
        // Verify was added in v2, missing in v1
        if (json.SchemaVersion == 1)
            json.State = SidecarFile.States.None;

        // v2 Schema
        // FfMpegToolVersion was replaced with FfProbeToolVersion in v3
        // MkvToolVersion was replaced with MkvMergeToolVersion in v3
        // Migrating the versions is not really useful as the version format changed
        // TODO : Convert the version format from longform to shortform
        if (json.SchemaVersion < 3)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            json.FfProbeToolVersion = json.FfMpegToolVersion;
            json.MkvMergeToolVersion = json.MkvToolVersion;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        // v3 Schema
        // Verified was replaced with State in v4
        if (json.SchemaVersion < 4)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            if (json.Verified)
#pragma warning restore CS0618 // Type or member is obsolete
                json.State |= SidecarFile.States.Verified;
        }

        // v4 Schema
        // MediaHash was added
        // Will always require a recomputation if missing

        return true;
    }
}