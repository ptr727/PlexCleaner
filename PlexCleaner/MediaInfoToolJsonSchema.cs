using System.Collections.Generic;
using System.Text.Json.Serialization;

// Convert JSON file to C# using app.quicktype.io
// Set language, framework, namespace, list

// No JSON schema. use XML schema
// https://github.com/MediaArea/MediaAreaXml/blob/master/mediainfo.xsd
// https://mediaarea.net/en/MediaInfo/Support/Tags

// Use MediaInfo example output:
// mediainfo --Output=JSON file.mkv

// TODO: Evaluate support on all version of MediaInfo and switch from XML to JSON
// TODO: Reduce to minimal set of used values
// TODO: VideoCount, AudioCount, TextCount indicates sub-tracks that can be used for exclusion or e.g. CC inclusion

namespace PlexCleaner;

public class MediaInfoToolJsonSchema
{
    [JsonPropertyName("media")]
    public Media Media { get; } = new();
}

public partial class Media
{
    [JsonPropertyName("track")]
    public List<Track> Tracks { get; } = [];
}

public partial class Track
{
    [JsonPropertyName("@type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("Format")]
    public string Format { get; set; } = "";

    [JsonPropertyName("Format_Profile")]
    public string FormatProfile { get; set; } = "";

    [JsonPropertyName("CodecID")]
    public string CodecId { get; set; } = "";

    [JsonPropertyName("ID")]
    public string Id { get; set; } = "";

    [JsonPropertyName("Format_Level")]
    public string FormatLevel { get; set; } = "";

    [JsonPropertyName("MuxingMode")]
    public string MuxingMode { get; set; } = "";
}
