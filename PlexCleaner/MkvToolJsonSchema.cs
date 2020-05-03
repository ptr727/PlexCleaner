using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

// Convert JSON schema to C# using quicktype.io in VSCode https://marketplace.visualstudio.com/items?itemName=typeguard.quicktype-vs
// https://gitlab.com/mbunkus/mkvtoolnix/-/blob/master/doc/json-schema/mkvmerge-identification-output-schema-v12.json

// Convert array[] to List<>
// Change uid long to UInt64
// Remove per item NullValueHandling = NullValueHandling.Ignore and add to Converter settings

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

        public static MkvMerge FromJson(string json) => 
            JsonConvert.DeserializeObject<MkvMerge>(json, Converter.Settings);
    }

    public class Container
    {
        [JsonProperty("properties")]
        public ContainerProperties Properties { get; } = new ContainerProperties();
    }

    public class ContainerProperties
    {
        [JsonProperty("duration")]
        public long Duration { get; set; } = 0;
    }

    public class GlobalTag
    {
        [JsonProperty("num_entries")]
        public int NumEntries { get; set; } = 0;
    }

    public class TrackTag
    {
        [JsonProperty("num_entries")]
        public int NumEntries { get; set; } = 0;

        [JsonProperty("track_id")]
        public int TrackId { get; set; } = 0;
    }

    public class Track
    {
        [JsonProperty("codec")]
        public string Codec { get; set; } = "";

        [JsonProperty("id")]
        public int Id { get; set; } = 0;

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
        public bool Forced { get; set; } = false;

        [JsonProperty("tag_language")]
        public string TagLanguage { get; set; } = "";

        [JsonProperty("number")]
        public int Number { get; set; } = 0;

        [JsonProperty("track_name")]
        public string TrackName { get; set; } = "";

        [JsonProperty("default_track")]
        public bool DefaultTrack { get; set; } = false;
    }

    internal static class Converter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            }
        };
    }
}
