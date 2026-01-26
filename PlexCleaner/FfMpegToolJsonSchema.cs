using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

// No JSON schema, use XML schema
// https://github.com/FFmpeg/FFmpeg/blob/master/doc/ffprobe.xsd

// Use ffprobe example output:
// ffprobe -loglevel quiet -show_streams -print_format json file.mkv

namespace PlexCleaner;

public class FfMpegToolJsonSchema
{
    public class FfProbe
    {
        [JsonPropertyName("streams")]
        public List<Track> Tracks { get; } = [];

        [JsonPropertyName("format")]
        public FormatInfo Format { get; } = new();

        // Will throw on failure to deserialize
        public static FfProbe FromJson(string json) =>
            JsonSerializer.Deserialize(json, FfMpegToolJsonContext.Default.FfProbe)
            ?? throw new JsonException("Failed to deserialize FfProbe");
    }

    public class FormatInfo
    {
        [JsonPropertyName("format_name")]
        public string FormatName { get; set; } = string.Empty;

        [JsonPropertyName("duration")]
        public double Duration { get; set; }

        [JsonPropertyName("tags")]
        public Dictionary<string, string> Tags { get; } = [];
    }

    public class Track
    {
        [JsonPropertyName("index")]
        public long Index { get; set; }

        [JsonPropertyName("codec_name")]
        public string CodecName { get; set; } = string.Empty;

        [JsonPropertyName("codec_long_name")]
        public string CodecLongName { get; set; } = string.Empty;

        [JsonPropertyName("profile")]
        public string Profile { get; set; } = string.Empty;

        [JsonPropertyName("codec_type")]
        public string CodecType { get; set; } = string.Empty;

        [JsonPropertyName("codec_tag_string")]
        public string CodecTagString { get; set; } = string.Empty;

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("field_order")]
        public string FieldOrder { get; set; } = string.Empty;

        [JsonPropertyName("closed_captions")]
        public int ClosedCaptions { get; set; }

        [JsonPropertyName("disposition")]
        public Disposition Disposition { get; } = new();

        [JsonPropertyName("tags")]
        public Dictionary<string, string> Tags { get; } = [];
    }

    public class Disposition
    {
        [JsonPropertyName("default")]
        public int Default { get; set; }

        [JsonIgnore]
        public bool IsDefault => Default != 0;

        [JsonPropertyName("forced")]
        public int Forced { get; set; }

        [JsonIgnore]
        public bool IsForced => Forced != 0;

        [JsonPropertyName("original")]
        public int Original { get; set; }

        [JsonIgnore]
        public bool IsOriginal => Original != 0;

        [JsonPropertyName("comment")]
        public int Comment { get; set; }

        [JsonIgnore]
        public bool IsCommentary => Comment != 0;

        [JsonPropertyName("hearing_impaired")]
        public int HearingImpaired { get; set; }

        [JsonIgnore]
        public bool IsHearingImpaired => HearingImpaired != 0;

        [JsonPropertyName("visual_impaired")]
        public int VisualImpaired { get; set; }

        [JsonIgnore]
        public bool IsVisualImpaired => VisualImpaired != 0;

        [JsonPropertyName("descriptions")]
        public int Descriptions { get; set; }

        [JsonIgnore]
        public bool IsDescriptions => Descriptions != 0;
    }

    public class PacketInfo
    {
        [JsonPropertyName("packets")]
        public List<Packet> Packets { get; } = [];
    }

    public class Packet
    {
        [JsonPropertyName("codec_type")]
        public string CodecType { get; set; } = string.Empty;

        [JsonPropertyName("stream_index")]
        public long StreamIndex { get; set; } = -1;

        [JsonPropertyName("pts_time")]
        public double PtsTime { get; set; } = double.NaN;

        [JsonPropertyName("dts_time")]
        public double DtsTime { get; set; } = double.NaN;

        [JsonPropertyName("duration_time")]
        public double DurationTime { get; set; } = double.NaN;

        [JsonPropertyName("size")]
        public long Size { get; set; } = -1;
    }
}

[JsonSourceGenerationOptions(
    AllowTrailingCommas = true,
    IncludeFields = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
    ReadCommentHandling = JsonCommentHandling.Skip
)]
[JsonSerializable(typeof(FfMpegToolJsonSchema.FfProbe))]
[JsonSerializable(typeof(FfMpegToolJsonSchema.Packet))]
internal partial class FfMpegToolJsonContext : JsonSerializerContext;
