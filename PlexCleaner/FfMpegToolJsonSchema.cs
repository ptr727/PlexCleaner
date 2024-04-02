using System.Collections.Generic;
using Newtonsoft.Json;

// Convert JSON file to C# using quicktype.io in VSCode https://marketplace.visualstudio.com/items?itemName=typeguard.quicktype-vs
// TODO: Find JSON schema definition
// https://stackoverflow.com/questions/61398647/where-can-i-get-the-ffprobe-json-schema-definition

// Use ffprobe example output:
// ffprobe -loglevel quiet -show_streams -print_format json file.mkv

// Convert array[] to List<>
// Remove per item NullValueHandling = NullValueHandling.Ignore and add to Converter settings

// No JSON schema, but XML schema
// https://github.com/FFmpeg/FFmpeg/blob/master/doc/ffprobe.xsd

// ReSharper disable once CheckNamespace
namespace PlexCleaner.FfMpegToolJsonSchema;

public class FfProbe
{
    [JsonProperty("streams")]
    public List<Stream> Streams { get; } = [];

    [JsonProperty("format")]
    public Format Format { get; } = new();

    public static FfProbe FromJson(string json)
    {
        return JsonConvert.DeserializeObject<FfProbe>(json, ConfigFileJsonSchema.JsonReadSettings);
    }
}

public class Format
{
    [JsonProperty("format_name")]
    public string FormatName { get; set; } = "";

    [JsonProperty("duration")]
    public double Duration { get; set; }

    [JsonProperty("tags")]
    public Dictionary<string, string> Tags { get; } = new();
}

public class Stream
{
    [JsonProperty("index")]
    public int Index { get; set; }

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
    public int Level { get; set; }

    [JsonProperty("field_order")]
    public string FieldOrder { get; set; } = "";

    // XSD says it is a Boolean, examples use an int
    // TODO: Newtonsoft would convert 0 to false all else to true, Text.Json is not so forgiving
    // https://stackoverflow.com/questions/68682450/automatic-conversion-of-numbers-to-bools-migrating-from-newtonsoft-to-system-t
    [JsonProperty("closed_captions")]
    public int ClosedCaptions { get; set; }

    [JsonProperty("disposition")]
    public Disposition Disposition { get; } = new();

    [JsonProperty("tags")]
    public Dictionary<string, string> Tags { get; } = new();
}

public class Disposition
{
    [JsonProperty("default")]
    public int Default { get; set; }

    [JsonProperty("forced")]
    public int Forced { get; set; }

    [JsonProperty("original")]
    public int Original { get; set; }

    [JsonProperty("comment")]
    public int Comment { get; set; }

    [JsonProperty("hearing_impaired")]
    public int HearingImpaired { get; set; }

    [JsonProperty("visual_impaired")]
    public int VisualImpaired { get; set; }

    [JsonProperty("descriptions")]
    public int Descriptions { get; set; }
}

public class PacketInfo
{
    [JsonProperty("packets")]
    public List<Packet> Packets { get; } = [];
}

public class Packet
{
    [JsonProperty("codec_type")]
    public string CodecType { get; set; } = "";

    [JsonProperty("stream_index")]
    public long StreamIndex { get; set; } = -1;

    [JsonProperty("pts_time")]
    public double PtsTime { get; set; } = double.NaN;

    [JsonProperty("dts_time")]
    public double DtsTime { get; set; } = double.NaN;

    [JsonProperty("duration_time")]
    public double DurationTime { get; set; } = double.NaN;

    [JsonProperty("size")]
    public long Size { get; set; } = -1;
}
