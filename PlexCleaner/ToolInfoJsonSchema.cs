using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace PlexCleaner
{
    public class ToolInfoJsonSchema
    {
        [DefaultValue(0)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public const int CurrentSchemaVersion = 2;

        public DateTime LastCheck { get; set; }

        public List<MediaToolInfo> Tools { get; } = new List<MediaToolInfo>();

        public MediaToolInfo GetToolInfo(MediaTool mediaTool)
        {
            // Match tool by family
            return Tools.FirstOrDefault(t => t.ToolFamily == mediaTool.GetToolFamily());
        }

        public MediaToolInfo GetToolInfo(MediaToolInfo mediaToolInfo)
        {
            // Match tool by family
            return Tools.FirstOrDefault(t => t.ToolFamily == mediaToolInfo.ToolFamily);
        }

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
