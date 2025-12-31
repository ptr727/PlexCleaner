using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

// Convert JSON file to C# using app.quicktype.io
// Set language, framework, namespace, list

// No JSON schema, use XML schema
// https://github.com/FFmpeg/FFmpeg/blob/master/doc/ffprobe.xsd

// Convert array[] to List<>
// Remove per item NullValueHandling = NullValueHandling.Ignore and add to Converter settings

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

        public static FfProbe FromJson(string json) =>
            JsonSerializer.Deserialize(json, FfMpegToolJsonContext.Default.FfProbe);
    }

    public class FormatInfo
    {
        [JsonPropertyName("format_name")]
        public string FormatName { get; set; } = "";

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
        public string CodecName { get; set; } = "";

        [JsonPropertyName("codec_long_name")]
        public string CodecLongName { get; set; } = "";

        [JsonPropertyName("profile")]
        public string Profile { get; set; } = "";

        [JsonPropertyName("codec_type")]
        public string CodecType { get; set; } = "";

        [JsonPropertyName("codec_tag_string")]
        public string CodecTagString { get; set; } = "";

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("field_order")]
        public string FieldOrder { get; set; } = "";

        // XSD says it is a Boolean, examples use an int
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

        [JsonPropertyName("forced")]
        public int Forced { get; set; }

        [JsonPropertyName("original")]
        public int Original { get; set; }

        [JsonPropertyName("comment")]
        public int Comment { get; set; }

        [JsonPropertyName("hearing_impaired")]
        public int HearingImpaired { get; set; }

        [JsonPropertyName("visual_impaired")]
        public int VisualImpaired { get; set; }

        [JsonPropertyName("descriptions")]
        public int Descriptions { get; set; }
    }

    public class PacketInfo
    {
        [JsonPropertyName("packets")]
        public List<Packet> Packets { get; } = [];
    }

    public class Packet
    {
        [JsonPropertyName("codec_type")]
        public string CodecType { get; set; } = "";

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
internal partial class FfMpegToolJsonContext : JsonSerializerContext;
