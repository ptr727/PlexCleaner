using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

// No JSON schema. use XML schema
// https://github.com/MediaArea/MediaAreaXml/blob/master/mediainfo.xsd
// https://mediaarea.net/en/MediaInfo/Support/Tags

// Use MediaInfo example output:
// mediainfo --Output=JSON file.mkv

namespace PlexCleaner;

public class MediaInfoToolJsonSchema
{
    public class MediaInfo
    {
        [JsonPropertyName("media")]
        public Media Media { get; } = new();

        public static MediaInfo FromJson(string json) =>
            JsonSerializer.Deserialize(json, MediaInfoToolJsonContext.Default.MediaInfo);
    }

    public class Media
    {
        [JsonPropertyName("track")]
        public List<Track> Tracks { get; } = [];
    }

    public class Track
    {
        [JsonPropertyName("@type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("Format")]
        public string Format { get; set; } = "";

        [JsonPropertyName("Format_Profile")]
        public string FormatProfile { get; set; } = "";

        [JsonPropertyName("Format_Level")]
        public string FormatLevel { get; set; } = "";

        [JsonPropertyName("HDR_Format")]
        public string HdrFormat { get; set; } = "";

        [JsonPropertyName("CodecID")]
        public string CodecId { get; set; } = "";

        [JsonPropertyName("ID")]
        public string Id { get; set; } = "";

        [JsonPropertyName("UniqueID")]
        public string UniqueId { get; set; } = "";

        [JsonPropertyName("Duration")]
        public string Duration { get; set; } = "";

        [JsonPropertyName("Language")]
        public string Language { get; set; } = "";

        [JsonPropertyName("Default")]
        public string Default { get; set; } = "";

        public bool IsDefault => StringToBool(Default);

        [JsonPropertyName("Forced")]
        public string Forced { get; set; } = "";

        public bool IsForced => StringToBool(Forced);

        [JsonPropertyName("MuxingMode")]
        public string MuxingMode { get; set; } = "";

        [JsonPropertyName("StreamOrder")]
        public string StreamOrder { get; set; } = "";

        [JsonPropertyName("ScanType")]
        public string ScanType { get; set; } = "";

        [JsonPropertyName("Title")]
        public string Title { get; set; } = "";

        private static bool StringToBool(string value) =>
            value != null
            && (
                value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("1", StringComparison.OrdinalIgnoreCase)
            );
    }
}

[JsonSourceGenerationOptions(
    AllowTrailingCommas = true,
    IncludeFields = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
    ReadCommentHandling = JsonCommentHandling.Skip
)]
[JsonSerializable(typeof(MediaInfoToolJsonSchema.MediaInfo))]
internal partial class MediaInfoToolJsonContext : JsonSerializerContext;
