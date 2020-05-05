using Newtonsoft.Json;
using System;

namespace PlexCleaner
{
    public class SidecarFileJsonSchema
    {
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public const int CurrentSchemaVersion = 2;

        public DateTime MediaLastWriteTimeUtc { get; set; }
        public long MediaLength { get; set; }

        public string FfMpegToolVersion { get; set; }
        public string FfProbeInfoData { get; set; }

        public string MkvToolVersion { get; set; }
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
