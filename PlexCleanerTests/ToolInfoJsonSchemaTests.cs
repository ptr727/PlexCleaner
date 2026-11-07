using System.Text.Json;
using AwesomeAssertions;
using PlexCleaner;
using Serilog.Events;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public class ToolInfoJsonSchemaTests
{
    private static ToolInfoJsonSchema NewSchema()
    {
        ToolInfoJsonSchema schema = new()
        {
            LastCheck = new DateTime(2024, 8, 11, 12, 0, 0, DateTimeKind.Utc),
        };
        schema.Tools.Add(MediaToolInfoTests.NewToolInfo());
        return schema;
    }

    [Fact]
    public void ToFile_Then_FromFile_Round_Trips()
    {
        string path = Path.GetTempFileName();
        try
        {
            ToolInfoJsonSchema schema = NewSchema();

            ToolInfoJsonSchema.ToFile(path, schema);
            ToolInfoJsonSchema read = ToolInfoJsonSchema.FromFile(path);

            _ = read.Should().BeEquivalentTo(schema);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FromJson_Null_Document_Throws()
    {
        Action action = () => ToolInfoJsonSchema.FromJson("null");

        _ = action.Should().Throw<JsonException>();
    }

    [Fact]
    public void GetToolInfo_Finds_Tool_By_Family()
    {
        ToolInfoJsonSchema schema = NewSchema();

        _ = schema
            .GetToolInfo(new SevenZip.Tool())
            .Should()
            .NotBeNull()
            .And.BeEquivalentTo(MediaToolInfoTests.NewToolInfo());
        _ = schema.GetToolInfo(new FfMpeg.Tool()).Should().BeNull();
    }

    [Fact]
    public void Upgrade_Current_Version_Succeeds() =>
        _ = ToolInfoJsonSchema.Upgrade(NewSchema()).Should().BeTrue();

    [Fact]
    public void Upgrade_Older_Version_Fails_And_Logs()
    {
        ToolInfoJsonSchema schema = NewSchema();
        schema.SchemaVersion = 1;
        bool upgraded = true;

        IReadOnlyList<LogEvent> events = LogCapture.Capture(() =>
            upgraded = ToolInfoJsonSchema.Upgrade(schema)
        );

        _ = upgraded.Should().BeFalse();
        _ = LogCapture.Messages(events, LogEventLevel.Error).Should().ContainSingle();
    }
}
