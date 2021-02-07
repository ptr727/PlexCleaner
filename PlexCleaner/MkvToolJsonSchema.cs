using System.Collections.Generic;
using Newtonsoft.Json;

// Convert JSON schema to C# using quicktype.io in VSCode https://marketplace.visualstudio.com/items?itemName=typeguard.quicktype-vs
// https://gitlab.com/mbunkus/mkvtoolnix/-/raw/main/doc/json-schema/mkvmerge-identification-output-schema-v13.json

// Convert array[] to List<>
// Change uid long to UInt64
// Remove per item NullValueHandling = NullValueHandling.Ignore and add to Converter settings

// ReSharper disable once CheckNamespace
namespace PlexCleaner.MkvToolJsonSchema
{
    public class MkvMerge
    {
        [JsonProperty("container")]
        public Container Container { get; } = new Container();

        [JsonProperty("global_tags")]
        public List<GlobalTag> GlobalTags { get; } = new List<GlobalTag>();

        [JsonProperty("track_tags")]
        public List<TrackTag> TrackTags { get; } = new List<TrackTag>();

        [JsonProperty("tracks")]
        public List<Track> Tracks { get; } = new List<Track>();

        [JsonProperty("attachments")]
        public List<Attachment> Attachments { get; } = new List<Attachment>();

        [JsonProperty("chapters")]
        public List<Chapter> Chapters { get; } = new List<Chapter>();

        public static MkvMerge FromJson(string json) => 
            JsonConvert.DeserializeObject<MkvMerge>(json, Settings);

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented
        };
    }

    public class Container
    {
        [JsonProperty("properties")]
        public ContainerProperties Properties { get; } = new ContainerProperties();

        [JsonProperty("type")]
        public string Type { get; set; } = "";
    }

    public class ContainerProperties
    {
        [JsonProperty("duration")]
        public long Duration { get; set; }
        [JsonProperty("title")]
        public string Title { get; set; } = "";
    }

    public class GlobalTag
    {
        [JsonProperty("num_entries")]
        public int NumEntries { get; set; }
    }

    public class TrackTag
    {
        [JsonProperty("num_entries")]
        public int NumEntries { get; set; }

        [JsonProperty("track_id")]
        public int TrackId { get; set; }
    }

    public class Track
    {
        [JsonProperty("codec")]
        public string Codec { get; set; } = "";

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("properties")]
        public TrackProperties Properties { get; } = new TrackProperties();

        [JsonProperty("type")]
        public string Type { get; set; } = "";
    }

    public class TrackProperties
    {
        [JsonProperty("codec_id")]
        public string CodecId { get; set; } = "";

        [JsonProperty("codec_name")]
        public string CodecName { get; set; } = "";

        [JsonProperty("language")]
        public string Language { get; set; } = "";

        [JsonProperty("forced_track")]
        public bool Forced { get; set; }

        [JsonProperty("tag_language")]
        public string TagLanguage { get; set; } = "";

        [JsonProperty("number")]
        public int Number { get; set; }

        [JsonProperty("track_name")]
        public string TrackName { get; set; } = "";

        [JsonProperty("default_track")]
        public bool DefaultTrack { get; set; }
    }

    public class Attachment
    {
        [JsonProperty("content_type")]
        public string ContentType { get; set; } = "";

        [JsonProperty("id")]
        public int Id { get; set; }
    }

    public class Chapter
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "";
    }
}
