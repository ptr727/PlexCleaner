using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Newtonsoft.Json;

namespace Sandbox;

internal class TestSomething(Dictionary<string, JsonElement> settings) : Program(settings)
{
    protected override Task<int> Sandbox(string[] args)
    {
        TestXsdParser();

        TestXmlParser();

        TestXmlToJsonParser();

        TestJsonParser();

        return Task.FromResult(0);
    }

    protected void TestJsonParser()
    {
        using FileStream fsJson = new(@"D:\MediaInfo.json", FileMode.Open);
        TestJson.MediaInfo rootObject = System.Text.Json.JsonSerializer.Deserialize(
            fsJson,
            TestJsonContext.Default.TestJsonRootobject
        );
        Debug.Assert(rootObject != null);
    }

    protected void TestXsdParser()
    {
        // XmlSerializer serializer = new XmlSerializerFactory().CreateSerializer(typeof(mediainfoType));
        // XmlSerializer serializer = new mediainfoTypeSerializer();
        XmlSerializer serializer = new(typeof(mediainfoType));

        using FileStream fsXml = new(@"D:\MediaInfo.xml", FileMode.Open);
        using StreamReader srXml = new(fsXml);
        using XmlReader xmlReader = XmlReader.Create(
            fsXml,
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }
        );
        mediainfoType mediaInfo = (mediainfoType)serializer.Deserialize(xmlReader);
        Debug.Assert(mediaInfo != null);
    }

    protected void TestXmlToJsonParser()
    {
        using FileStream fsXml = new(@"D:\MediaInfo.xml", FileMode.Open);
        using StreamReader srXml = new(fsXml);
        string xmlString = srXml.ReadToEnd();
        XmlDocument xmlDoc = new();
        xmlDoc.LoadXml(xmlString);
        string jsonDoc = JsonConvert.SerializeXmlNode(xmlDoc);
        Debug.Assert(!string.IsNullOrEmpty(jsonDoc));
    }

    protected void TestXmlParser()
    {
        using FileStream fsXml = new(@"D:\MediaInfo.xml", FileMode.Open);
        using StreamReader srXml = new(fsXml);
        string xmlString = srXml.ReadToEnd();
        TestXml.MediaInfo mediaInfo = TestXml.MediaInfo.FromXml(xmlString);
        Debug.Assert(mediaInfo != null);
    }
}
