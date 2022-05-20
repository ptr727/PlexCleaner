using Newtonsoft.Json.Schema.Generation;
using PlexCleaner;

namespace Sandbox;

internal class SchemaGenerator
{
    public static void GenerateSchema()
    {
        Console.WriteLine("Generating ConfigFileJsonSchema schema");

        // Create JSON schema
        var generator = new JSchemaGenerator
        {
            // TODO: How can I make the default schema required, and just mark individual items as not required?
            DefaultRequired = Newtonsoft.Json.Required.Default
        };
        var schema = generator.Generate(typeof(ConfigFileJsonSchema));
        schema.Title = "PlexCleaner Configuration Schema";
        schema.SchemaVersion = new Uri("http://json-schema.org/draft-06/schema");
        schema.Id = new Uri("https://raw.githubusercontent.com/ptr727/PlexCleaner/main/PlexCleaner.schema.json");
        Console.WriteLine(schema);
    }
}
