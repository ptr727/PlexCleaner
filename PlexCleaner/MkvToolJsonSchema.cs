using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

// https://gitlab.com/mbunkus/mkvtoolnix/-/blob/main/doc/json-schema/mkvmerge-identification-output-schema-v17.json
// https://mkvtoolnix.download/latest-release.json

// Use mkvmerge example output:
// mkvmerge --identify file.mkv --identification-format json

namespace PlexCleaner;

public class MkvToolJsonSchema
{
    public class MkvMerge
    {
        [JsonPropertyName("container")]
        public Container Container { get; } = new();

        [JsonPropertyName("global_tags")]
        public List<GlobalTag> GlobalTags { get; } = [];

        [JsonPropertyName("track_tags")]
        public List<TrackTag> TrackTags { get; } = [];

        [JsonPropertyName("tracks")]
        public List<Track> Tracks { get; } = [];

        [JsonPropertyName("attachments")]
        public List<Attachment> Attachments { get; } = [];

        [JsonPropertyName("chapters")]
        public List<Chapter> Chapters { get; } = [];

        // Will throw on failure to deserialize
        public static MkvMerge FromJson(string json) =>
            JsonSerializer.Deserialize(json, MkvToolJsonContext.Default.MkvMerge)
            ?? throw new JsonException("Failed to deserialize MkvMerge");
    }

    public class Container
    {
        [JsonPropertyName("properties")]
        public ContainerProperties Properties { get; } = new();

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("recognized")]
        public bool Recognized { get; set; }

        [JsonPropertyName("supported")]
        public bool Supported { get; set; }
    }

    public class ContainerProperties
    {
        [JsonPropertyName("duration")]
        public long Duration { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;
    }

    public class GlobalTag
    {
        [JsonPropertyName("num_entries")]
        public int NumEntries { get; set; }
    }

    public class TrackTag
    {
        [JsonPropertyName("num_entries")]
        public int NumEntries { get; set; }

        [JsonPropertyName("track_id")]
        public int TrackId { get; set; }
    }

    public class Track
    {
        [JsonPropertyName("codec")]
        public string Codec { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("properties")]
        public TrackProperties Properties { get; } = new();

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
    }

    public class TrackProperties
    {
        [JsonPropertyName("codec_id")]
        public string CodecId { get; set; } = string.Empty;

        [JsonPropertyName("language")]
        public string Language { get; set; } = string.Empty;

        [JsonPropertyName("language_ietf")]
        public string LanguageIetf { get; set; } = string.Empty;

        [JsonPropertyName("forced_track")]
        public bool Forced { get; set; }

        [JsonPropertyName("tag_language")]
        public string TagLanguage { get; set; } = string.Empty;

        [JsonPropertyName("uid")]
        public ulong Uid { get; set; }

        [JsonPropertyName("number")]
        public long Number { get; set; }

        [JsonPropertyName("track_name")]
        public string TrackName { get; set; } = string.Empty;

        [JsonPropertyName("default_track")]
        public bool DefaultTrack { get; set; }

        [JsonPropertyName("flag_original")]
        public bool Original { get; set; }

        [JsonPropertyName("flag_commentary")]
        public bool Commentary { get; set; }

        [JsonPropertyName("flag_visual_impaired")]
        public bool VisualImpaired { get; set; }

        [JsonPropertyName("flag_hearing_impaired")]
        public bool HearingImpaired { get; set; }

        [JsonPropertyName("flag_text_descriptions")]
        public bool TextDescriptions { get; set; }
    }

    public class Attachment
    {
        [JsonPropertyName("content_type")]
        public string ContentType { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public int Id { get; set; }
    }

    public class Chapter
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
    }

    public class LatestRelease
    {
        [JsonPropertyName("mkvtoolnix-releases")]
        public MkvtoolnixReleases MkvToolnixReleases { get; } = new();

        // Will throw on failure to deserialize
        public static LatestRelease FromJson(string json) =>
            JsonSerializer.Deserialize(json, MkvToolJsonContext.Default.LatestRelease)
            ?? throw new JsonException("Failed to deserialize LatestRelease");
    }

    public class MkvtoolnixReleases
    {
        [JsonPropertyName("latest-source")]
        public LatestSource LatestSource { get; } = new();
    }

    public class LatestSource
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
    }
}

[JsonSourceGenerationOptions(
    AllowTrailingCommas = true,
    IncludeFields = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
    ReadCommentHandling = JsonCommentHandling.Skip
)]
[JsonSerializable(typeof(MkvToolJsonSchema.MkvMerge))]
[JsonSerializable(typeof(MkvToolJsonSchema.LatestRelease))]
internal partial class MkvToolJsonContext : JsonSerializerContext;
