using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PlexCleaner;
using JsonSerializer = System.Text.Json.JsonSerializer;

// Must contain something to keep the compiler happy
#pragma warning disable CS0219
int foo = 1;
#pragma warning restore CS0219

// Text.Json settings
var jsonSerializerSettingsRead = new JsonSerializerSettings
{
    ObjectCreationHandling = ObjectCreationHandling.Reuse
};
var jsonSerializerSettingsWrite = new JsonSerializerSettings
{
    Formatting = Formatting.Indented,
    StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
    NullValueHandling = NullValueHandling.Ignore,
    ContractResolver = new ExcludeObsoletePropertiesResolver()
};

// Newtonsoft.Json options
var jsonSerializerOptionsRead = new JsonSerializerOptions
{
    PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true
};
var jsonSerializerOptionsWrite = new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        .WithAddedModifier(JsonExtensions.ExcludeObsoleteProperties)
};

// Schema
var schema = new Schema 
{ 
    Required = "Required",
    Obsolete = "Obsolete",
    HashSet = "foo, bar"
};
var schemaDerived = new SchemaDerived
{
    Required = "Required",
    Obsolete = "Obsolete",
    HashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "foo", "bar" }
};
var schemaJson1= JsonSerializer.Serialize<Schema>(schema, jsonSerializerOptionsWrite);
var schemaJson2 = JsonConvert.SerializeObject(schema, jsonSerializerSettingsWrite);
var schemaJson3 = JsonSerializer.Serialize<Schema>(schemaDerived, jsonSerializerOptionsWrite);
var schemaJson4 = JsonConvert.SerializeObject(schemaDerived, jsonSerializerSettingsWrite);

// JSON
const string json = "{\"Required\":\"Required\",\"Obsolete\":\"Obsolete\",\"HashSet\":[\"foo\",\"bar\"]}";

// Newtonsoft
// Error, Obsolete is not read nor written
var schemaNewtonJson = JsonConvert.DeserializeObject<SchemaDerived>(json, jsonSerializerSettingsRead);
var jsonNewtonJson = JsonConvert.SerializeObject(schemaNewtonJson, jsonSerializerSettingsWrite);

// Text.Json
// Seems to work, Obsolete is read but not written
var schemaTextJson = JsonSerializer.Deserialize<SchemaDerived>(json, jsonSerializerOptionsRead);
var jsonTextJson = JsonSerializer.Serialize<SchemaDerived>(schemaTextJson, jsonSerializerOptionsWrite);


var jsonText = File.ReadAllText(PlexCleanerTests.PlexCleanerTests.GetSampleFilePath("PlexCleaner.v2.json"));
var jsonSchema1 = JsonSerializer.Deserialize<ConfigFileJsonSchema2>(jsonText, jsonSerializerOptionsRead);
var jsonSchema2= JsonSerializer.Deserialize<ConfigFileJsonSchema2>(jsonText);
var jsonSchema3 = JsonConvert.DeserializeObject<ConfigFileJsonSchema2>(jsonText, jsonSerializerSettingsRead);
var jsonSchema4 = JsonConvert.DeserializeObject<ConfigFileJsonSchema2>(jsonText);

// var configFileJsonSchema = PlexCleaner.ConfigFileJsonSchema4.FromFile(PlexCleanerTests.PlexCleanerTests.GetSampleFilePath("PlexCleaner.v2.json"));

var parent1 = new ParentSchema1()
{
    Child = new ChildSchema1()
    {
        CSV = "foo, bar"
    }
};

var parent2 = new ParentSchema2()
{
    Child = new ChildSchema2()
    {
        List = [
            new() { Foo = "foo1", Bar = "bar1" },
            new() { Foo = "foo2", Bar = "bar2" }
            ]
    }
};

var json1 = JsonConvert.SerializeObject(parent2, jsonSerializerSettingsWrite);
var json2 = JsonSerializer.Serialize(parent2, jsonSerializerOptionsWrite);
var txt = "{\r\n  \"Child\": {\r\n    \"List\": [\r\n      {\r\n        \"Foo\": \"foo\",\r\n        \"Bar\": \"bar\"\r\n      }\r\n    ]\r\n  }\r\n}";
var parent3 = JsonConvert.DeserializeObject<ParentSchema2>(json1, jsonSerializerSettingsRead);
var parent4 = JsonSerializer.Deserialize<ParentSchema2>(json2, jsonSerializerOptionsRead);

foo = 1;

public record Schema
{
    // Always read and write
    [Required]
    public string Required { get; set; } = "";

    // Read but do not write
    [Obsolete]
    public string Obsolete { get; set; } = "";

    [Obsolete]
    public string HashSet { internal get; set; } = "";
}

public record SchemaDerived : Schema
{
    [Required]
    public new HashSet<string> HashSet { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public record ParentSchema1
{
    [Obsolete]
    public ChildSchema1 Child { internal get; set; } = new();
}

public record ParentSchema2 : ParentSchema1
{
    public ParentSchema2() { }
    public ParentSchema2(ParentSchema1 parent) : base(parent) { }

    [Required]
    public new ChildSchema2 Child { get; set; } = new();
}


public record SubSchema
{
    public string Foo;
    public string Bar;
}

public record ChildSchema1
{
    [Obsolete]
    public string CSV { internal get; set; } = "";
}

public record ChildSchema2 : ChildSchema1
{
    public ChildSchema2() { }
    public ChildSchema2(ChildSchema1 child) : base(child) { }

    [Required]
    public List<SubSchema> List { get; set; } = [];
}

public static class JsonExtensions
{
    public static void ExcludeObsoleteProperties(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
            return;
        foreach (var property in typeInfo.Properties)
        {
            // Do not serialize Obsolete items
            if (property.AttributeProvider?.IsDefined(typeof(ObsoleteAttribute), true) == true)
                property.ShouldSerialize = (_, _) => false;
        }
    }
}

public class ExcludeObsoletePropertiesResolver : DefaultContractResolver
{
    protected override List<MemberInfo> GetSerializableMembers(Type objectType)
    {
        var memberList = base.GetSerializableMembers(objectType);
        // Remove all Obsolete items
        memberList.RemoveAll(item => item.IsDefined(typeof(ObsoleteAttribute), true));
        return memberList;
    }
}
