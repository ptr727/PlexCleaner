using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

// Convert JSON file to C# using quicktype.io in VSCode https://marketplace.visualstudio.com/items?itemName=typeguard.quicktype-vs
// Use ffproble example output
// TODO: Find JSON schema definition
// https://stackoverflow.com/questions/61398647/where-can-i-get-the-ffprobe-json-schema-definition

// Convert array[] to List<>
// Remove per item NullValueHandling = NullValueHandling.Ignore and add to Converter settings

namespace PlexCleaner.FfMpegToolJsonSchema
{
    public class FfProbe
    {
        [JsonProperty("streams")]
        public List<Stream> Streams { get; } = new List<Stream>();

        public static FfProbe FromJson(string json) => 
            JsonConvert.DeserializeObject<FfProbe>(json, Settings);

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented
        };
    }

    public class Stream
    {
        [JsonProperty("index")]
        public int Index { get; set; } = 0;

        [JsonProperty("codec_name")]
        public string CodecName { get; set; } = "";

        [JsonProperty("codec_long_name")]
        public string CodecLongName { get; set; } = "";

        [JsonProperty("profile")]
        public string Profile { get; set; } = "";

        [JsonProperty("codec_type")]
        public string CodecType { get; set; } = "";

        [JsonProperty("codec_tag_string")]
        public string CodecTagString { get; set; } = "";

        [JsonProperty("level")]
        public string Level { get; set; } = "";

        [JsonProperty("field_order")]
        public string FieldOrder { get; set; } = "";

        [JsonProperty("tags")]
        public Tags Tags { get; } = new Tags();

        [JsonProperty("disposition")]
        public Disposition Disposition { get; } = new Disposition();
    }

    public class Tags
    {
        [JsonProperty("language")]
        public string Language { get; set; } = "";
        [JsonProperty("title")]
        public string Title { get; set; } = "";
    }

    public class Disposition
    {
        [JsonProperty("default")]
        public bool Default { get; set; } = false;
        [JsonProperty("forced")]
        public bool Forced { get; set; } = false;
    }
}
