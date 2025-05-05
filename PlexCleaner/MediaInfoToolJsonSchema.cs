using System.Collections.Generic;
using System.Text.Json.Serialization;

// Convert JSON file to C# using app.quicktype.io
// Set language, framework, namespace, list

// No JSON schema. use XML schema
// https://github.com/MediaArea/MediaAreaXml/blob/master/mediainfo.xsd
// https://mediaarea.net/en/MediaInfo/Support/Tags

// Use MediaInfo example output:
// mediainfo --Output=JSON file.mkv

// TODO: Evaluate JSON support on old versions of MediaInfo and switch from XML to JSON

namespace PlexCleaner;

public class MediaInfoToolJsonSchema
{
    [JsonPropertyName("media")]
    public Media Media { get; } = new();
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

    [JsonPropertyName("CodecID")]
    public string CodecId { get; set; } = "";

    [JsonPropertyName("ID")]
    public string Id { get; set; } = "";

    [JsonPropertyName("Format_Level")]
    public string FormatLevel { get; set; } = "";

    [JsonPropertyName("MuxingMode")]
    public string MuxingMode { get; set; } = "";
}
