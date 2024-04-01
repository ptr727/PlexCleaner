using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.ComponentModel.DataAnnotations;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Schema;

// Must contain something to keep the compiler happy
#pragma warning disable CS0219
int foo = 1;
#pragma warning restore CS0219

// Text.Json settings
var jsonSerializerSettings = new JsonSerializerSettings()
{
    Formatting = Formatting.Indented,
    StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
    NullValueHandling = NullValueHandling.Ignore,
    ObjectCreationHandling = ObjectCreationHandling.Reuse,
    ContractResolver = new ExcludeObsoletePropertiesResolver()
};

// Newtonsoft.Json options
var jsonSerializerOptions = new JsonSerializerOptions()
{
    WriteIndented = true, // Formatting = Formatting.Indented,
    Encoder = null, // StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, // NullValueHandling = NullValueHandling.Ignore,
    PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate, // ObjectCreationHandling = ObjectCreationHandling.Reuse,
    TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        .WithAddedModifier(JsonExtensions.ExcludeObsoleteProperties),
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true
};

// JSON string
var jsonString = "{\"Required\":\"Required\",\"Obsolete\":\"Obsolete\"}";

// Newtonsoft
// Error, Obsolete is not read nor written
var schemaNewtonJson = Newtonsoft.Json.JsonConvert.DeserializeObject<Schema>(jsonString, jsonSerializerSettings);
var jsonNewtonJson = Newtonsoft.Json.JsonConvert.SerializeObject(schemaNewtonJson, jsonSerializerSettings);

// Text.Json
// Seems to work, Obsolete is read but not written
var schemaTextJson = System.Text.Json.JsonSerializer.Deserialize<Schema>(jsonString, jsonSerializerOptions);
var jsonTextJson = System.Text.Json.JsonSerializer.Serialize<Schema>(schemaTextJson, jsonSerializerOptions);


foo = 1;

public record Schema
{
    // Always read and write
    [Required]
    public string Required { get; set; } = "";

    // Read but do not write
    [Obsolete]
    public string Obsolete { get; set; } = "";
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
                property.ShouldSerialize = (object _, object? _) => { return false; };
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
