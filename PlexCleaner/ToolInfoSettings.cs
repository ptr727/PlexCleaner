using Newtonsoft.Json;
using System.Collections.Generic;

namespace PlexCleaner
{
    public class ToolInfoSettings
    {
        public string LastCheck { get; set; }

        public List<ToolInfo> Tools { get; set; }

        public static string ToJson(ToolInfoSettings tools) =>
            JsonConvert.SerializeObject(tools, Settings);

        public static ToolInfoSettings FromJson(string json) =>
            JsonConvert.DeserializeObject<ToolInfoSettings>(json, Settings);

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Formatting = Formatting.Indented
        };
    }
}
