using Newtonsoft.Json;
using System;
using System.ComponentModel;

namespace PlexCleaner
{
    public class SidecarFileJsonSchema
    {
        [DefaultValue(0)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public const int CurrentSchemaVersion = 3;

        public DateTime MediaLastWriteTimeUtc { get; set; }
        public long MediaLength { get; set; }

        public string FfProbeToolVersion { get; set; }
        public string FfProbeInfoData { get; set; }

        public string MkvMergeToolVersion { get; set; }
        public string MkvMergeInfoData { get; set; }

        public string MediaInfoToolVersion { get; set; }
        public string MediaInfoData { get; set; }

        public bool Verified { get; set; }

        public static string ToJson(SidecarFileJsonSchema json) =>
            JsonConvert.SerializeObject(json, Settings);

        public static SidecarFileJsonSchema FromJson(string json) =>
            JsonConvert.DeserializeObject<SidecarFileJsonSchema>(json, Settings);

        public static SidecarFileJsonSchema FromJson(JsonTextReader reader)
        {
            JsonSerializer serializer = JsonSerializer.Create(Settings);
            return (SidecarFileJsonSchema)serializer.Deserialize(reader, typeof(SidecarFileJsonSchema));
        }

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented
        };
    }
}
