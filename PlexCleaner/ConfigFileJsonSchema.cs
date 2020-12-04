using Newtonsoft.Json;
using System.IO;

namespace PlexCleaner
{
    public class ConfigFileJsonSchema
    {
        public ToolsOptions ToolsOptions { get; set; } = new ToolsOptions();
        public ConvertOptions ConvertOptions { get; set; } = new ConvertOptions();
        public ProcessOptions ProcessOptions { get; set; } = new ProcessOptions();
        public MonitorOptions MonitorOptions { get; set; } = new MonitorOptions();
        public VerifyOptions VerifyOptions { get; set; } = new VerifyOptions();

        public static void WriteDefaultsToFile(string path)
        {
            ConfigFileJsonSchema config = new ConfigFileJsonSchema();
            ToFile(path, config);
        }

        public static ConfigFileJsonSchema FromFile(string path)
        {
            return FromJson(File.ReadAllText(path));
        }

        public static void ToFile(string path, ConfigFileJsonSchema settings)
        {
            File.WriteAllText(path, ToJson(settings));
        }

        public static string ToJson(ConfigFileJsonSchema settings) =>
            JsonConvert.SerializeObject(settings, Settings);

        public static ConfigFileJsonSchema FromJson(string json) =>
            JsonConvert.DeserializeObject<ConfigFileJsonSchema>(json, Settings);

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented
        };
    }
}
