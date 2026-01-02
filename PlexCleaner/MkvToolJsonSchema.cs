using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

// JSON Schema: https://gitlab.com/mbunkus/mkvtoolnix/-/blob/main/doc/json-schema/mkvmerge-identification-output-schema-v17.json

// Use mkvmerge example output:
// mkvmerge --identify file.mkv --identification-format json

// Convert array[] to List<>
// Change uid long to UInt64
// Remove per item NullValueHandling = NullValueHandling.Ignore and add to Converter settings

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

        public static MkvMerge FromJson(string json) =>
            JsonSerializer.Deserialize(json, MkvToolJsonContext.Default.MkvMerge);
    }

    public class Container
    {
        [JsonPropertyName("properties")]
        public ContainerProperties Properties { get; } = new();

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

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
        public string Title { get; set; } = "";
    }

    public class GlobalTag
    {
        [JsonPropertyName("num_entries")]
        public int NumEntries { get; set; }
    }

    // TODO: Only used to for presence, do we need contents?
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
        public string Codec { get; set; } = "";

        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("properties")]
        public TrackProperties Properties { get; } = new();

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";
    }

    public class TrackProperties
    {
        [JsonPropertyName("codec_id")]
        public string CodecId { get; set; } = "";

        [JsonPropertyName("language")]
        public string Language { get; set; } = "";

        [JsonPropertyName("language_ietf")]
        public string LanguageIetf { get; set; }

        [JsonPropertyName("forced_track")]
        public bool Forced { get; set; }

        [JsonPropertyName("tag_language")]
        public string TagLanguage { get; set; } = "";

        [JsonPropertyName("uid")]
        public ulong Uid { get; set; }

        [JsonPropertyName("number")]
        public long Number { get; set; }

        [JsonPropertyName("track_name")]
        public string TrackName { get; set; } = "";

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

    // TODO: Only used to for presence, do we need contents?
    public class Attachment
    {
        [JsonPropertyName("content_type")]
        public string ContentType { get; set; } = "";

        [JsonPropertyName("id")]
        public int Id { get; set; }
    }

    // TODO: Only used to for presence, do we need contents?
    public class Chapter
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";
    }

    // https://mkvtoolnix.download/latest-release.json
    public class LatestRelease
    {
        [JsonPropertyName("mkvtoolnix-releases")]
        public MkvtoolnixReleases MkvToolnixReleases { get; } = new();

        public static LatestRelease FromJson(string json) =>
            JsonSerializer.Deserialize(json, MkvToolJsonContext.Default.LatestRelease);
    }

    public class MkvtoolnixReleases
    {
        [JsonPropertyName("latest-source")]
        public LatestSource LatestSource { get; } = new();
    }

    public class LatestSource
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "";
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
