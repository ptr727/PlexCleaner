using System;
using System.IO;
using System.Xml;

namespace PlexCleaner;

// AOT-safe XML parser for MediaInfo XML output
// Only parses the elements used in MediaInfoToolXmlSchema
public static class MediaInfoXmlParser
{
    public static MediaInfoToolXmlSchema.MediaInfo ParseXml(string xml)
    {
        MediaInfoToolXmlSchema.MediaInfo mediaInfo = new();
        using StringReader stringReader = new(xml);
        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreWhitespace = true,
            IgnoreComments = true,
        };

        using XmlReader reader = XmlReader.Create(stringReader, settings);
        while (reader.Read())
        {
            if (
                reader.NodeType == XmlNodeType.Element
                && reader.LocalName.Equals("mediainfo", StringComparison.OrdinalIgnoreCase)
            )
            {
                ParseMediaInfo(reader, mediaInfo);
                break;
            }
        }

        return mediaInfo;
    }

    private static void ParseMediaInfo(XmlReader reader, MediaInfoToolXmlSchema.MediaInfo mediaInfo)
    {
        int mediaInfoDepth = reader.Depth;
        while (reader.Read() && reader.Depth > mediaInfoDepth)
        {
            if (
                reader.NodeType == XmlNodeType.Element
                && reader.LocalName.Equals("media", StringComparison.OrdinalIgnoreCase)
            )
            {
                ParseMedia(reader, mediaInfo.Media);
            }
        }
    }

    private static void ParseMedia(XmlReader reader, MediaInfoToolXmlSchema.Media media)
    {
        int mediaDepth = reader.Depth;
        while (reader.Read() && reader.Depth > mediaDepth)
        {
            if (
                reader.NodeType == XmlNodeType.Element
                && reader.LocalName.Equals("track", StringComparison.OrdinalIgnoreCase)
            )
            {
                MediaInfoToolXmlSchema.Track track = ParseTrack(reader);
                media.Tracks.Add(track);
            }
        }
    }

    private static MediaInfoToolXmlSchema.Track ParseTrack(XmlReader reader)
    {
        // Get the type attribute
        MediaInfoToolXmlSchema.Track track = new();
        if (reader.HasAttributes)
        {
            track.Type = reader.GetAttribute("type") ?? "";
        }

        int trackDepth = reader.Depth;
        while (reader.Read() && reader.Depth > trackDepth)
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                string elementName = reader.LocalName;
                if (reader.Read() && reader.NodeType == XmlNodeType.Text)
                {
                    switch (elementName.ToLowerInvariant())
                    {
                        case "id":
                            track.Id = reader.Value;
                            break;
                        case "uniqueid":
                            track.UniqueId = reader.Value;
                            break;
                        case "duration":
                            track.Duration = reader.Value;
                            break;
                        case "format":
                            track.Format = reader.Value;
                            break;
                        case "format_profile":
                            track.FormatProfile = reader.Value;
                            break;
                        case "format_level":
                            track.FormatLevel = reader.Value;
                            break;
                        case "hdr_format":
                            track.HdrFormat = reader.Value;
                            break;
                        case "codecid":
                            track.CodecId = reader.Value;
                            break;
                        case "language":
                            track.Language = reader.Value;
                            break;
                        case "default":
                            track.Default = reader.Value;
                            break;
                        case "forced":
                            track.Forced = reader.Value;
                            break;
                        case "muxingmode":
                            track.MuxingMode = reader.Value;
                            break;
                        case "streamorder":
                            track.StreamOrder = reader.Value;
                            break;
                        case "scantype":
                            track.ScanType = reader.Value;
                            break;
                        case "title":
                            track.Title = reader.Value;
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        return track;
    }
}
