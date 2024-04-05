using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

// Must contain something to keep the compiler happy
#pragma warning disable CS0219
int foo = 1;
#pragma warning restore CS0219

// Create test object and populate it with some data
/*
var parent1 = new Parent1()
{
    Child = new Child1()
    {
        List = [
            new() { Foo = "foo1", Bar = "bar1" },
            new() { Foo = "foo2", Bar = "bar2" }
            ]
    }
};
*/
const string parent1Json = "{\r\n  \"Child\": {\r\n    \"List\": [\r\n      {\r\n        \"Foo\": \"foo1\",\r\n        \"Bar\": \"bar1\"\r\n      },\r\n      {\r\n        \"Foo\": \"foo2\",\r\n        \"Bar\": \"bar2\"\r\n      }\r\n    ]\r\n  }\r\n}";

// Convert to and from JSON using Newtonsoft
var parentN1 = Newtonsoft.Json.JsonConvert.DeserializeObject<Parent1>(parent1Json, JsonHelper.JsonSerializerSettingsRead);
ArgumentNullException.ThrowIfNull(parentN1);
var parentN2 = new Parent2(parentN1);
var jsonN = Newtonsoft.Json.JsonConvert.SerializeObject(parentN2, JsonHelper.JsonSerializerSettingsWrite);
Console.WriteLine(jsonN);

// Convert to and from JSON using Text.Json
var parentT1 = JsonSerializer.Deserialize<Parent1>(parent1Json, JsonHelper.JsonSerializerOptionsRead);
ArgumentNullException.ThrowIfNull(parentT1);
var parentT2 = new Parent2(parentT1);
var jsonT = JsonSerializer.Serialize(parentT2, JsonHelper.JsonSerializerOptionsWrite);
Console.WriteLine(jsonT);

// Create JSON schema
const string schemaUri = "https://raw.githubusercontent.com/ptr727/PlexCleaner/main/PlexCleaner.schema.json";
var builder = new Json.Schema.JsonSchemaBuilder();
Json.Schema.JsonSchemaBuilderExtensions.Title(builder, "PlexCleaner Configuration Schema");
Json.Schema.JsonSchemaBuilderExtensions.Id(builder, new Uri(schemaUri));
var schema = Json.Schema.Generation.JsonSchemaBuilderExtensions.FromType<Parent2>(builder).Build();
var jsonSchema = JsonSerializer.Serialize(schema);
Console.WriteLine(jsonSchema);


public static class JsonHelper
{
    public static readonly Newtonsoft.Json.JsonSerializerSettings JsonSerializerSettingsRead = new Newtonsoft.Json.JsonSerializerSettings
    {
        ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Reuse
    };

    public static readonly Newtonsoft.Json.JsonSerializerSettings JsonSerializerSettingsWrite = new Newtonsoft.Json.JsonSerializerSettings
    {
        Formatting = Newtonsoft.Json.Formatting.Indented,
        StringEscapeHandling = Newtonsoft.Json.StringEscapeHandling.EscapeNonAscii,
        NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
        ContractResolver = new ExcludeObsoletePropertiesResolver()
    };

    public static readonly JsonSerializerOptions JsonSerializerOptionsRead = new JsonSerializerOptions
    {
        IncludeFields = true,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static readonly JsonSerializerOptions JsonSerializerOptionsWrite = new JsonSerializerOptions
    {
        IncludeFields = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
            .WithAddedModifier(ExcludeObsoleteProperties)
    };

    public static void ExcludeObsoleteProperties(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
            return;
        foreach (var property in typeInfo.Properties)
        {
            if (property.AttributeProvider?.IsDefined(typeof(ObsoleteAttribute), true) == true)
                property.ShouldSerialize = (_, _) => false;
        }
    }
}

public record Parent1
{
    [Obsolete]
    [Json.Schema.Generation.JsonExclude]
    public Child1 Child { get; set; } = new();
}

public record Parent2 : Parent1
{
    public Parent2(Parent1 parent) : base(parent) 
    { 
        Upgrade(); 
    }

    [JsonRequired]
    public new Child2 Child { get; set; } = new();

#pragma warning disable CS0612 // Type or member is obsolete
    protected void Upgrade() 
    { 
        Parent1 parent1 = this;
        Child.NewList.AddRange(parent1.Child.List);
        parent1.Child.List.Clear();
    }
#pragma warning restore CS0612 // Type or member is obsolete
}

public record Sub
{
    public string Foo = "";
    public string Bar = "";
}

public record Child1
{
    public Child1() { }

    [Obsolete]
    [Json.Schema.Generation.JsonExclude]
    public List<Sub> List { get; set; } = [];
}

public record Child2 : Child1
{
    public Child2() { }
    public Child2(Child1 child) : base(child) { }

    [JsonRequired]
    public List<Sub> NewList { get; set; } = [];
}

public class ExcludeObsoletePropertiesResolver : Newtonsoft.Json.Serialization.DefaultContractResolver
{
    protected override List<MemberInfo> GetSerializableMembers(Type objectType)
    {
        var memberList = base.GetSerializableMembers(objectType);
        memberList.RemoveAll(item => item.IsDefined(typeof(ObsoleteAttribute), true));
        return memberList;
    }
}
