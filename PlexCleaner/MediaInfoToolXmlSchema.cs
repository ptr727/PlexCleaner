using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

// https://github.com/MediaArea/MediaAreaXml/blob/master/mediainfo.xsd
// https://mediaarea.net/en/MediaInfo/Support/Tags

// Convert XML to C# using http://xmltocsharp.azurewebsites.net/
// Do not use XSD, https://mediaarea.net/mediainfo/mediainfo.xsd

// Use mediainfo example output:
// mediainfo --Output=XML file.mkv

// Replace the namespace with Namespace="https://mediaarea.net/mediainfo"
// Add FromXml() method

namespace PlexCleaner;

public class MediaInfoToolXmlSchema
{
    [XmlRoot(ElementName = "track", Namespace = "https://mediaarea.net/mediainfo")]
    public class Track
    {
        [XmlAttribute(AttributeName = "type")]
        public string Type { get; set; } = "";

        [XmlElement(ElementName = "ID", Namespace = "https://mediaarea.net/mediainfo")]
        public string Id { get; set; } = "";

        [XmlElement(ElementName = "Format", Namespace = "https://mediaarea.net/mediainfo")]
        public string Format { get; set; } = "";

        [XmlElement(ElementName = "Format_Profile", Namespace = "https://mediaarea.net/mediainfo")]
        public string FormatProfile { get; set; } = "";

        [XmlElement(ElementName = "Format_Level", Namespace = "https://mediaarea.net/mediainfo")]
        public string FormatLevel { get; set; } = "";

        [XmlElement(ElementName = "HDR_Format", Namespace = "https://mediaarea.net/mediainfo")]
        public string HdrFormat { get; set; } = "";

        [XmlElement(
            ElementName = "HDR_Format_Profile",
            Namespace = "https://mediaarea.net/mediainfo"
        )]
        public string HdrFormatProfile { get; set; } = "";

        [XmlElement(
            ElementName = "HDR_Format_Level",
            Namespace = "https://mediaarea.net/mediainfo"
        )]
        public string HdrFormatLevel { get; set; } = "";

        [XmlElement(
            ElementName = "HDR_Format_Compatibility",
            Namespace = "https://mediaarea.net/mediainfo"
        )]
        public string HdrFormatCompatibility { get; set; } = "";

        [XmlElement(ElementName = "CodecID", Namespace = "https://mediaarea.net/mediainfo")]
        public string CodecId { get; set; } = "";

        [XmlElement(ElementName = "Language", Namespace = "https://mediaarea.net/mediainfo")]
        public string Language { get; set; } = "";

        [XmlElement(ElementName = "Default", Namespace = "https://mediaarea.net/mediainfo")]
        public string DefaultString { get; set; } = "";
        public bool Default => MediaInfo.StringToBool(DefaultString);

        [XmlElement(ElementName = "Forced", Namespace = "https://mediaarea.net/mediainfo")]
        public string ForcedString { get; set; } = "";
        public bool Forced => MediaInfo.StringToBool(ForcedString);

        [XmlElement(ElementName = "MuxingMode", Namespace = "https://mediaarea.net/mediainfo")]
        public string MuxingMode { get; set; } = "";

        [XmlElement(ElementName = "StreamOrder", Namespace = "https://mediaarea.net/mediainfo")]
        public int StreamOrder { get; set; }

        [XmlElement(ElementName = "ScanType", Namespace = "https://mediaarea.net/mediainfo")]
        public string ScanType { get; set; } = "";

        [XmlElement(ElementName = "Title", Namespace = "https://mediaarea.net/mediainfo")]
        public string Title { get; set; } = "";
    }

    [XmlRoot(ElementName = "media", Namespace = "https://mediaarea.net/mediainfo")]
    public class MediaElement
    {
        [XmlElement(ElementName = "track", Namespace = "https://mediaarea.net/mediainfo")]
        public List<Track> Tracks { get; } = [];
    }

    [XmlRoot(ElementName = "MediaInfo", Namespace = "https://mediaarea.net/mediainfo")]
    public class MediaInfo
    {
        [XmlElement(ElementName = "media", Namespace = "https://mediaarea.net/mediainfo")]
        public MediaElement Media { get; set; } = new();

        public static MediaInfo FromXml(string xml)
        {
            XmlSerializer xmlSerializer = new(typeof(MediaInfo));
            using TextReader textReader = new StringReader(xml);
            using XmlReader xmlReader = XmlReader.Create(textReader);
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
}
