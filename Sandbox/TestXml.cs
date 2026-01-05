using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Sandbox;

public class TestXml
{
    [Serializable]
    [XmlType(Namespace = "https://mediaarea.net/mediainfo")]
    [XmlRoot("MediaInfo", Namespace = "https://mediaarea.net/mediainfo", IsNullable = false)]
    public class MediaInfo
    {
        [XmlElement("media", IsNullable = false)]
        public Media Media { get; set; } = new();

        public static MediaInfo FromXml(string xml)
        {
            XmlSerializer xmlSerializer = new(typeof(MediaInfo));
            using TextReader textReader = new StringReader(xml);
            XmlReaderSettings xmlSettings = new()
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            };
            using XmlReader xmlReader = XmlReader.Create(textReader, xmlSettings);
            return xmlSerializer.Deserialize(xmlReader) as MediaInfo;
        }

        public static bool StringToBool(string value) =>
            value != null
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
