using Newtonsoft.Json;
using System.IO;

namespace PlexCleaner
{
    public class Config
    {
        public ToolsOptions ToolsOptions { get; set; } = new ToolsOptions();
        public ConvertOptions ConvertOptions { get; set; } = new ConvertOptions();
        public ProcessOptions ProcessOptions { get; set; } = new ProcessOptions();
        public MonitorOptions MonitorOptions { get; set; } = new MonitorOptions();

        public static Config FromFile(string path)
        {
            return FromJson(File.ReadAllText(path));
        }

        public static void ToFile(string path, Config settings)
        {
            File.WriteAllText(path, ToJson(settings));
        }

        public static string ToJson(Config settings) =>
            JsonConvert.SerializeObject(settings, JsonSettings);

        public static Config FromJson(string json) =>
            JsonConvert.DeserializeObject<Config>(json, JsonSettings);

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Formatting = Formatting.Indented
        };
    }
}