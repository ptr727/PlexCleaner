using Newtonsoft.Json;
using System;

namespace PlexCleaner
{
    public class SidecarFileJsonSchema
    {
        public string ToolVersion { get; set; }
        public DateTime MediaLastWriteTimeUtc { get; set; }
        public long MediaLength { get; set; }

        public static string ToJson(SidecarFileJsonSchema header) =>
            JsonConvert.SerializeObject(header, Settings);

        public static SidecarFileJsonSchema FromJson(string json) =>
            JsonConvert.DeserializeObject<SidecarFileJsonSchema>(json, Settings);

        public static SidecarFileJsonSchema FromJson(JsonTextReader reader)
        {
            JsonSerializer serializer = JsonSerializer.Create(Settings);
            return (SidecarFileJsonSchema)serializer.Deserialize(reader, typeof(SidecarFileJsonSchema));
        }

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            // The JSON stream reader is greedy and does not allow us to read mixed content from the text reader
            // Write the entire serialized string in a single line
            Formatting = Formatting.None
        };
    }
}
