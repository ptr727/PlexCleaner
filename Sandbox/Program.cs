// See https://aka.ms/new-console-template for more information
using Newtonsoft.Json.Schema.Generation;
using PlexCleaner;

Console.WriteLine("Generating JSON schema");

// Create JSON schema
var generator = new JSchemaGenerator();
// TODO: How can I make the default schema required, and just mark individual items as not required?
generator.DefaultRequired = Newtonsoft.Json.Required.Default;
var schema = generator.Generate(typeof(ConfigFileJsonSchema));
schema.Title = "PlexCleaner Configuration Schema";
schema.SchemaVersion = new Uri("http://json-schema.org/draft-06/schema");
schema.Id = new Uri("https://raw.githubusercontent.com/ptr727/PlexCleaner/main/PlexCleaner.schema.json");
Console.WriteLine(schema);
