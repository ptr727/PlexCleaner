using System;
using System.Collections.Generic;
using System.Xml.Serialization;

// https://github.com/MediaArea/MediaAreaXml/blob/master/mediainfo.xsd
// https://mediaarea.net/en/MediaInfo/Support/Tags

// Use mediainfo example output:
// mediainfo --Output=XML file.mkv

namespace PlexCleaner;

public class MediaInfoToolXmlSchema
{
    [XmlRoot("MediaInfo", Namespace = "https://mediaarea.net/mediainfo", IsNullable = false)]
    public class MediaInfo
    {
        [XmlElement("media", IsNullable = false)]
        public Media Media { get; set; } = new();

        public static MediaInfo FromXml(string xml) => MediaInfoXmlParser.MediaInfoFromXml(xml);

        public static bool StringToBool(string value) =>
            !string.IsNullOrEmpty(value)
            && (
                value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("1", StringComparison.OrdinalIgnoreCase)
            );
    }

    [XmlRoot("media")]
    public class Media
    {
        [XmlElement("track")]
        public List<Track> Tracks { get; set; } = [];
    }

    [XmlRoot("track")]
    public class Track
    {
        [XmlAttribute("type")]
        public string Type { get; set; } = string.Empty;

        [XmlElement("ID")]
        public string Id { get; set; } = string.Empty;

        [XmlElement("UniqueID")]
        public string UniqueId { get; set; } = string.Empty;

        [XmlElement("Duration")]
        public string Duration { get; set; } = string.Empty;

        [XmlElement("Format")]
        public string Format { get; set; } = string.Empty;

        [XmlElement("Format_Profile")]
        public string FormatProfile { get; set; } = string.Empty;

        [XmlElement("Format_Level")]
        public string FormatLevel { get; set; } = string.Empty;

        [XmlElement("HDR_Format")]
        public string HdrFormat { get; set; } = string.Empty;

        [XmlElement("CodecID")]
        public string CodecId { get; set; } = string.Empty;

        [XmlElement("Language")]
        public string Language { get; set; } = string.Empty;

        [XmlElement("Default")]
        public string Default { get; set; } = string.Empty;

        [XmlIgnore]
        public bool IsDefault => MediaInfo.StringToBool(Default);

        [XmlElement("Forced")]
        public string Forced { get; set; } = string.Empty;

        [XmlIgnore]
        public bool IsForced => MediaInfo.StringToBool(Forced);

        [XmlElement("MuxingMode")]
        public string MuxingMode { get; set; } = string.Empty;

        [XmlElement("StreamOrder")]
        public string StreamOrder { get; set; } = string.Empty;

        [XmlElement("ScanType")]
        public string ScanType { get; set; } = string.Empty;

        [XmlElement("Title")]
        public string Title { get; set; } = string.Empty;
    }
}
