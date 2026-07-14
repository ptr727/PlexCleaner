using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class StreamTimingsProbeTests
{
    [Fact]
    public void FromJson_StartAndDuration_Parsed()
    {
        // ffprobe emits start_time and duration as strings, parsed to double
        // lang=json
        const string json = """
            { "streams": [ { "index": 0, "start_time": "0.000000", "duration": "1234.567000" } ] }
            """;
        FfMpegToolJsonSchema.StreamTimingsProbe probe =
            FfMpegToolJsonSchema.StreamTimingsProbe.FromJson(json);

        _ = probe.Streams.Should().ContainSingle();
        _ = probe.Streams[0].Index.Should().Be(0);
        _ = probe.Streams[0].StartTime.Should().Be(0.0);
        _ = probe.Streams[0].Duration.Should().Be(1234.567);
    }

    [Fact]
    public void FromJson_MissingTiming_DefaultsToNaN()
    {
        // A stream without start_time or duration keeps the NaN sentinel, treated as unchanged by the gate
        // lang=json
        const string json = """
            { "streams": [ { "index": 1 } ] }
            """;
        FfMpegToolJsonSchema.StreamTimingsProbe probe =
            FfMpegToolJsonSchema.StreamTimingsProbe.FromJson(json);

        _ = double.IsNaN(probe.Streams[0].StartTime).Should().BeTrue();
        _ = double.IsNaN(probe.Streams[0].Duration).Should().BeTrue();
    }
}
