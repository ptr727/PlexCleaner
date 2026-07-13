using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class VerifyClassifierTests
{
    [Fact]
    public void Classify_EmptyStderr_ReturnsClean()
    {
        _ = VerifyClassifier.Classify(string.Empty).Should().Be(VerifyResult.Clean);
        _ = VerifyClassifier.Classify("   \n  ").Should().Be(VerifyResult.Clean);
    }

    [Fact]
    public void Classify_OnlyDtsMuxerWarnings_ReturnsTimestampOnly()
    {
        string stderr =
            "[null @ 0x1] Application provided invalid, non monotonically increasing dts to muxer in stream 1: 8 >= 8\n"
            + "[null @ 0x1] Application provided invalid, non monotonically increasing dts to muxer in stream 1: 12 >= 12\n";
        _ = VerifyClassifier.Classify(stderr).Should().Be(VerifyResult.TimestampOnly);
    }

    [Theory]
    [InlineData("[matroska @ 0x1] Invalid data found when processing input")]
    [InlineData("[h264 @ 0x1] error while decoding MB 10 20")]
    [InlineData("[NULL @ 0x1] Invalid NAL unit size (-1148261185 > 8772).")]
    [InlineData("[h264 @ 0x1] mmco: unref short failure")]
    [InlineData("[aac @ 0x1] env_facs_q 255 is invalid")]
    [InlineData("[matroska,webm @ 0x1] Length 7 indicated by an EBML number exceeds max length 4.")]
    public void Classify_DecodeSignature_ReturnsDecodeError(string stderr) =>
        VerifyClassifier.Classify(stderr).Should().Be(VerifyResult.DecodeError);

    [Fact]
    public void Classify_DecodeErrorMixedWithDtsWarning_ReturnsDecodeError()
    {
        // A decode error co-occurring with benign DTS warnings must still fail
        string stderr =
            "[null @ 0x1] Application provided invalid, non monotonically increasing dts to muxer in stream 1: 8 >= 8\n"
            + "[h264 @ 0x1] error while decoding MB 3 4\n";
        _ = VerifyClassifier.Classify(stderr).Should().Be(VerifyResult.DecodeError);
    }

    [Fact]
    public void Classify_UnrecognizedStderr_ReturnsDecodeError() =>
        // Fail closed on anything not explicitly benign
        VerifyClassifier
            .Classify("[mystery @ 0x1] some unexpected diagnostic line")
            .Should()
            .Be(VerifyResult.DecodeError);

    [Fact]
    public void Accumulator_StreamedLines_ClassifiesAndKeepsFirstError()
    {
        // The accumulator sees lines one at a time without buffering the whole stderr
        VerifyClassifier.Accumulator accumulator = new();
        accumulator.Add(
            "[null @ 0x1] Application provided invalid, non monotonically increasing dts to muxer in stream 1: 8 >= 8"
        );
        _ = accumulator.Result.Should().Be(VerifyResult.TimestampOnly);

        accumulator.Add("[h264 @ 0x1] error while decoding MB 3 4");
        accumulator.Add("[h264 @ 0x1] Invalid data found");

        _ = accumulator.Result.Should().Be(VerifyResult.DecodeError);
        _ = accumulator.FirstError.Should().Be("[h264 @ 0x1] error while decoding MB 3 4");
    }
}
