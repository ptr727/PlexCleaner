using Newtonsoft.Json;
using System.IO;
using System.ComponentModel;

namespace PlexCleaner
{
    public class ConfigFileJsonSchema
    {
        [DefaultValue(0)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public const int CurrentSchemaVersion = 1;

        public ToolsOptions ToolsOptions { get; set; } = new();
        public ConvertOptions ConvertOptions { get; set; } = new();
        public ProcessOptions ProcessOptions { get; set; } = new();
        public MonitorOptions MonitorOptions { get; set; } = new();
        public VerifyOptions VerifyOptions { get; set; } = new();

        public static void WriteDefaultsToFile(string path)
        {
            ConfigFileJsonSchema config = new();
            ToFile(path, config);
        }

        public static ConfigFileJsonSchema FromFile(string path)
        {
            return FromJson(File.ReadAllText(path));
        }

        public static void ToFile(string path, ConfigFileJsonSchema json)
        {
            File.WriteAllText(path, ToJson(json));
        }

        public static string ToJson(ConfigFileJsonSchema settings) =>
            JsonConvert.SerializeObject(settings, Settings);

        public static ConfigFileJsonSchema FromJson(string json) =>
            JsonConvert.DeserializeObject<ConfigFileJsonSchema>(json, Settings);

        private static readonly JsonSerializerSettings Settings = new()
        {
            Formatting = Formatting.Indented,
            StringEscapeHandling = StringEscapeHandling.EscapeNonAscii
        };

        public static bool Upgrade(ConfigFileJsonSchema json)
        {
            // Current version
            if (json.SchemaVersion == CurrentSchemaVersion)
                return true;

            // Schema version was added in v1, and remains backwards compatible

            return true;
        }
    }
}
