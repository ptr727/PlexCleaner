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
        public string Type { get; set; } = "";

        [XmlElement("ID")]
        public string Id { get; set; } = "";

        [XmlElement("UniqueID")]
        public string UniqueId { get; set; } = "";

        [XmlElement("Duration")]
        public string Duration { get; set; } = "";

        [XmlElement("Format")]
        public string Format { get; set; } = "";

        [XmlElement("Format_Profile")]
        public string FormatProfile { get; set; } = "";

        [XmlElement("Format_Level")]
        public string FormatLevel { get; set; } = "";

        [XmlElement("HDR_Format")]
        public string HdrFormat { get; set; } = "";

        [XmlElement("CodecID")]
        public string CodecId { get; set; } = "";

        [XmlElement("Language")]
        public string Language { get; set; } = "";

        [XmlElement("Default")]
        public string Default { get; set; } = "";

        [XmlIgnore]
        public bool IsDefault => MediaInfo.StringToBool(Default);

        [XmlElement("Forced")]
        public string Forced { get; set; } = "";

        [XmlIgnore]
        public bool IsForced => MediaInfo.StringToBool(Forced);

        [XmlElement("MuxingMode")]
        public string MuxingMode { get; set; } = "";

        [XmlElement("StreamOrder")]
        public string StreamOrder { get; set; } = "";

        [XmlElement("ScanType")]
        public string ScanType { get; set; } = "";

        [XmlElement("Title")]
        public string Title { get; set; } = "";
    }
}
