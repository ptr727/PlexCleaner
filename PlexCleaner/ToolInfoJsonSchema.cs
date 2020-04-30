using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace PlexCleaner
{
    public class ToolInfoJsonSchema
    {
        public DateTime LastCheck { get; set; }

        public List<ToolInfo> Tools { get; } = new List<ToolInfo>();

        public static string ToJson(ToolInfoJsonSchema tools) =>
            JsonConvert.SerializeObject(tools, Settings);

        public static ToolInfoJsonSchema FromJson(string json) =>
            JsonConvert.DeserializeObject<ToolInfoJsonSchema>(json, Settings);

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented
        };
    }
}
