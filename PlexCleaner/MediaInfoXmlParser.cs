using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml;

// TODO: XML serializer is not AOT compatible
// https://stackoverflow.com/questions/79858800/statically-generated-xml-parsing-code-using-microsoft-xmlserializer-generator
// https://github.com/dotnet/runtime/issues/106580

namespace PlexCleaner;

public static class MediaInfoXmlParser
{
    // Parses known MediaInfo XML elements and converts to MediaInfo object
    public static MediaInfoToolXmlSchema.MediaInfo MediaInfoFromXml(string xml)
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
        MediaInfoToolXmlSchema.Track track = new();
        if (reader.HasAttributes)
        {
            track.Type = reader.GetAttribute("type") ?? string.Empty;
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

    // Parses all MediaInfo XML elements and converts to JSON
    public static string GenericXmlToJson(string xml)
    {
        XmlReaderSettings xmlSettings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreWhitespace = true,
            IgnoreComments = true,
        };
        using StringReader xmlStringReader = new(xml);
        using XmlReader reader = XmlReader.Create(xmlStringReader, xmlSettings);

        JsonWriterOptions jsonOptions = new()
        {
            Indented = true,
            IndentSize = 4,
            // Allow e.g. ' without escaping to \u0027
            // "mkvmerge v93.0 ('Goblu') 64-bit"
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        using MemoryStream jsonStream = new();
        using Utf8JsonWriter jsonWriter = new(jsonStream, jsonOptions);

        // Read until we find the root element
        while (reader.Read() && reader.NodeType != XmlNodeType.Element)
        {
            // Skip until root element
        }

        // Parse the root element's children
        WriteRootElementChildren(reader, jsonWriter);

        // Flush stream and write output
        jsonWriter.Flush();
        return Encoding.UTF8.GetString(jsonStream.ToArray());
    }

    private static void WriteRootElementChildren(XmlReader reader, Utf8JsonWriter writer)
    {
        if (reader.NodeType != XmlNodeType.Element)
        {
            return;
        }

        bool isEmpty = reader.IsEmptyElement;
        int elementDepth = reader.Depth;

        writer.WriteStartObject();

        if (!isEmpty)
        {
            // First pass: collect all children to detect arrays
            Dictionary<string, List<ElementData>> childrenByName = new(
                StringComparer.OrdinalIgnoreCase
            );

            while (reader.Read() && reader.Depth > elementDepth)
            {
                if (reader.Depth == elementDepth + 1 && reader.NodeType == XmlNodeType.Element)
                {
                    ElementData elementData = ReadElementData(reader);
                    if (!childrenByName.TryGetValue(elementData.Name, out List<ElementData>? value))
                    {
                        value = [];
                        childrenByName[elementData.Name] = value;
                    }
                    value.Add(elementData);
                }
            }

            // Second pass: write the JSON
            foreach (KeyValuePair<string, List<ElementData>> kvp in childrenByName)
            {
                writer.WritePropertyName(kvp.Key);

                bool isArray = kvp.Value.Count > 1;
                if (isArray)
                {
                    writer.WriteStartArray();
                }

                foreach (ElementData element in kvp.Value)
                {
                    WriteElementAsJson(element, writer);
                }

                if (isArray)
                {
                    writer.WriteEndArray();
                }
            }
        }

        writer.WriteEndObject();
    }

    private static ElementData ReadElementData(XmlReader reader)
    {
        ElementData data = new() { Name = reader.LocalName };

        bool isEmpty = reader.IsEmptyElement;
        int elementDepth = reader.Depth;

        // Read attributes
        if (reader.HasAttributes)
        {
            for (int i = 0; i < reader.AttributeCount; i++)
            {
                reader.MoveToAttribute(i);
                if (
                    !reader.Name.Equals("xmlns", StringComparison.OrdinalIgnoreCase)
                    && !reader.Name.StartsWith("xmlns:", StringComparison.OrdinalIgnoreCase)
                    && !reader.Name.StartsWith("xsi:", StringComparison.OrdinalIgnoreCase)
                )
                {
                    data.Attributes[reader.LocalName] = reader.Value;
                }
            }
            _ = reader.MoveToElement();
        }

        if (!isEmpty)
        {
            // Read child elements
            while (reader.Read() && reader.Depth > elementDepth)
            {
                if (reader.Depth == elementDepth + 1)
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                        {
                            ElementData childData = ReadElementData(reader);
                            if (
                                !data.Children.TryGetValue(
                                    childData.Name,
                                    out List<ElementData>? value
                                )
                            )
                            {
                                value = [];
                                data.Children[childData.Name] = value;
                            }
                            value.Add(childData);
                            break;
                        }
                        case XmlNodeType.Text or XmlNodeType.CDATA:
                            data.TextContent = reader.Value;
                            break;
                        case XmlNodeType.None:
                        case XmlNodeType.Attribute:
                        case XmlNodeType.EntityReference:
                        case XmlNodeType.Entity:
                        case XmlNodeType.ProcessingInstruction:
                        case XmlNodeType.Comment:
                        case XmlNodeType.Document:
                        case XmlNodeType.DocumentType:
                        case XmlNodeType.DocumentFragment:
                        case XmlNodeType.Notation:
                        case XmlNodeType.Whitespace:
                        case XmlNodeType.SignificantWhitespace:
                        case XmlNodeType.EndElement:
                        case XmlNodeType.EndEntity:
                        case XmlNodeType.XmlDeclaration:
                        default:
                            break;
                    }
                }
            }
        }

        return data;
    }

    private static void WriteElementAsJson(ElementData element, Utf8JsonWriter writer)
    {
        writer.WriteStartObject();

        // Write attributes
        // If element has children, use @ prefix for attributes
        // If element has only text content and attributes, don't use @ prefix
        bool hasChildren = element.Children.Count > 0;
        foreach (KeyValuePair<string, string> attr in element.Attributes)
        {
            writer.WriteString(hasChildren ? "@" + attr.Key : attr.Key, attr.Value);
        }

        // Write text content if present and no children
        // The text becomes the "name" property for elements like <creatingLibrary>
        if (!string.IsNullOrEmpty(element.TextContent) && !hasChildren)
        {
            writer.WriteString("name", element.TextContent);
        }

        // Write child elements as properties
        foreach ((string propertyName, List<ElementData> children) in element.Children)
        {
            bool isArray = children.Count > 1;

            // Check if all children have only text content (no attributes, no nested children)
            bool allSimpleText = children.All(c =>
                c.Attributes.Count == 0 && c.Children.Count == 0
            );

            if (isArray)
            {
                writer.WritePropertyName(propertyName);
                writer.WriteStartArray();

                foreach (ElementData child in children)
                {
                    if (allSimpleText && !string.IsNullOrEmpty(child.TextContent))
                    {
                        writer.WriteStringValue(child.TextContent);
                    }
                    else
                    {
                        WriteElementAsJson(child, writer);
                    }
                }

                writer.WriteEndArray();
            }
            else
            {
                ElementData child = children[0];
                if (allSimpleText && !string.IsNullOrEmpty(child.TextContent))
                {
                    // Simple text element
                    writer.WriteString(propertyName, child.TextContent);
                }
                else
                {
                    // Complex element with attributes or children
                    writer.WritePropertyName(propertyName);
                    WriteElementAsJson(child, writer);
                }
            }
        }

        writer.WriteEndObject();
    }

    private class ElementData
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, string> Attributes { get; } = [];
        public Dictionary<string, List<ElementData>> Children { get; } = [];
        public string TextContent { get; set; } = string.Empty;
    }

    // Parses known MediaInfo XML elements and converts to JSON
    public static string MediaInfoXmlToJson(string mediaInfoXml)
    {
        // Serialize from XML
        MediaInfoToolXmlSchema.MediaInfo xmlMediaInfo = MediaInfoToolXmlSchema.MediaInfo.FromXml(
            mediaInfoXml
        );

        // Copy to JSON schema
        MediaInfoToolJsonSchema.MediaInfo jsonMediaInfo = new();
        foreach (MediaInfoToolXmlSchema.Track xmlTrack in xmlMediaInfo.Media.Tracks)
        {
            MediaInfoToolJsonSchema.Track jsonTrack = new()
            {
                Type = xmlTrack.Type,
                Id = xmlTrack.Id,
                UniqueId = xmlTrack.UniqueId,
                Duration = xmlTrack.Duration,
                Format = xmlTrack.Format,
                FormatProfile = xmlTrack.FormatProfile,
                FormatLevel = xmlTrack.FormatLevel,
                HdrFormat = xmlTrack.HdrFormat,
                CodecId = xmlTrack.CodecId,
                Language = xmlTrack.Language,
                Default = xmlTrack.Default,
                Forced = xmlTrack.Forced,
                MuxingMode = xmlTrack.MuxingMode,
                StreamOrder = xmlTrack.StreamOrder,
                ScanType = xmlTrack.ScanType,
                Title = xmlTrack.Title,
            };
            jsonMediaInfo.Media.Tracks.Add(jsonTrack);
        }

        // Serialize to JSON
        return JsonSerializer.Serialize(jsonMediaInfo, MediaInfoToolJsonContext.Default.MediaInfo);
    }
}
