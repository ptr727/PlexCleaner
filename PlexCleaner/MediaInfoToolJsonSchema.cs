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

        // Will throw on failure to deserialize
        public static MediaInfo FromJson(string json) =>
            JsonSerializer.Deserialize(json, MediaInfoToolJsonContext.Default.MediaInfo)
            ?? throw new JsonException("Failed to deserialize MediaInfo");
    }

    public class Media
    {
        [JsonPropertyName("track")]
        public List<Track> Tracks { get; } = [];
    }

    public class Track
    {
        [JsonPropertyName("@type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("Format")]
        public string Format { get; set; } = string.Empty;

        [JsonPropertyName("Format_Profile")]
        public string FormatProfile { get; set; } = string.Empty;

        [JsonPropertyName("Format_Level")]
        public string FormatLevel { get; set; } = string.Empty;

        [JsonPropertyName("HDR_Format")]
        public string HdrFormat { get; set; } = string.Empty;

        [JsonPropertyName("CodecID")]
        public string CodecId { get; set; } = string.Empty;

        [JsonPropertyName("ID")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("UniqueID")]
        public string UniqueId { get; set; } = string.Empty;

        [JsonPropertyName("Duration")]
        public string Duration { get; set; } = string.Empty;

        [JsonPropertyName("Language")]
        public string Language { get; set; } = string.Empty;

        [JsonPropertyName("Default")]
        public string Default { get; set; } = string.Empty;

        [JsonIgnore]
        public bool IsDefault => StringToBool(Default);

        [JsonPropertyName("Forced")]
        public string Forced { get; set; } = string.Empty;

        [JsonIgnore]
        public bool IsForced => StringToBool(Forced);

        [JsonPropertyName("MuxingMode")]
        public string MuxingMode { get; set; } = string.Empty;

        [JsonPropertyName("StreamOrder")]
        public string StreamOrder { get; set; } = string.Empty;

        [JsonPropertyName("ScanType")]
        public string ScanType { get; set; } = string.Empty;

        [JsonPropertyName("Title")]
        public string Title { get; set; } = string.Empty;

        private static bool StringToBool(string value) =>
            !string.IsNullOrEmpty(value)
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
