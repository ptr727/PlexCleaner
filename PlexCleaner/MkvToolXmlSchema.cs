using System.IO;
using System.Xml;
using System.Xml.Serialization;

// Convert XML to C# using http://xmltocsharp.azurewebsites.net/
// https://mkvtoolnix.download/latest-release.xml.gz

// ReSharper disable once CheckNamespace
namespace PlexCleaner.MkvToolXmlSchema;

[XmlRoot(ElementName = "latest-source")]
public class LatestSource
{
    [XmlElement(ElementName = "version")]
    public string Version { get; set; }
}

[XmlRoot(ElementName = "mkvtoolnix-releases")]
public class MkvToolnixReleases
{
    [XmlElement(ElementName = "latest-source")]
    public LatestSource LatestSource { get; set; }

    public static MkvToolnixReleases FromXml(string xml)
    {
        XmlSerializer xmlSerializer = new(typeof(MkvToolnixReleases));
        using TextReader textReader = new StringReader(xml);
        using var xmlReader = XmlReader.Create(textReader);
        return xmlSerializer.Deserialize(xmlReader) as MkvToolnixReleases;
    }
}
