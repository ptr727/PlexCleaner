using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class ClosedCaptionsProbeTests
{
    [Fact]
    public void FromJson_ClosedCaptionsPresent_Parsed()
    {
        // lang=json
        const string json = """
            { "programs": [], "stream_groups": [], "streams": [ { "closed_captions": 1 } ] }
            """;
        FfMpegToolJsonSchema.ClosedCaptionsProbe probe =
            FfMpegToolJsonSchema.ClosedCaptionsProbe.FromJson(json);

        _ = probe.Streams.Should().ContainSingle();
        _ = probe.Streams[0].ClosedCaptions.Should().Be(1);
    }

    [Fact]
    public void FromJson_NoClosedCaptions_Parsed()
    {
        // lang=json
        const string json = """
            { "streams": [ { "closed_captions": 0 } ] }
            """;
        FfMpegToolJsonSchema.ClosedCaptionsProbe probe =
            FfMpegToolJsonSchema.ClosedCaptionsProbe.FromJson(json);

        _ = probe.Streams.Should().ContainSingle();
        _ = probe.Streams[0].ClosedCaptions.Should().Be(0);
    }

    [Fact]
    public void FromJson_NoStreams_Empty()
    {
        // lang=json
        const string json = """
            { "streams": [] }
            """;
        FfMpegToolJsonSchema.ClosedCaptionsProbe probe =
            FfMpegToolJsonSchema.ClosedCaptionsProbe.FromJson(json);

        _ = probe.Streams.Should().BeEmpty();
    }
}
