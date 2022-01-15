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
        JsonSerializer serializer = JsonSerializer.Create(Settings);
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

#pragma warning disable CS0612
#pragma warning disable 618

        // Schema must be v2 or later
        // Verify was added in v2
        if (json.SchemaVersion < 2)
        {
            Log.Logger.Error("Sidecar schema not upgradeable : {Schema} < 2", json.SchemaVersion);
            return false;
        }

        // FfMpegToolVersion was replaced with FfProbeToolVersion in v3
        // MkvToolVersion was replaced with MkvMergeToolVersion in v3
        // Migrating the versions is not really useful as the version format changed
        // TODO : Convert the version format from longform to shortform
        if (json.SchemaVersion < 3)
        {
            json.FfProbeToolVersion = json.FfMpegToolVersion;
            json.MkvMergeToolVersion = json.MkvToolVersion;
        }

        // Verified was replaced with State in v4
        if (json.SchemaVersion < 4)
        {
            if (json.Verified)
                json.State |= SidecarFile.States.Verified;
        }

#pragma warning restore CS0612
#pragma warning restore 618

        return true;
    }
}