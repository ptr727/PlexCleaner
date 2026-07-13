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

    [Fact]
    public void Classify_DtsWarningFollowedByRepeatMarker_ReturnsTimestampOnly()
    {
        // ffmpeg collapses consecutive identical DTS warnings, the repeat marker must not fail the file
        string stderr =
            "[null @ 0x1] Application provided invalid, non monotonically increasing dts to muxer in stream 1: 8 >= 8\n"
            + "    Last message repeated 1 times\n";
        _ = VerifyClassifier.Classify(stderr).Should().Be(VerifyResult.TimestampOnly);
    }

    [Theory]
    [InlineData("[matroska @ 0x1] Invalid data found when processing input")]
    [InlineData("[h264 @ 0x1] error while decoding MB 10 20")]
    [InlineData("[NULL @ 0x1] Invalid NAL unit size (-1148261185 > 8772).")]
    [InlineData("[truehd @ 0x1] quant_step_size larger than huff_lsbs")]
    public void Classify_DecodeError_ReturnsDecodeError(string stderr) =>
        VerifyClassifier.Classify(stderr).Should().Be(VerifyResult.DecodeError);

    [Fact]
    public void Classify_DecodeErrorMixedWithDtsWarning_ReturnsDecodeError()
    {
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
    public void Accumulator_RepeatedErrorType_DedupedToUniqueLines()
    {
        // Lines differing only by pointer address or numbers collapse to one entry, distinct types do not
        VerifyClassifier.Accumulator accumulator = new();
        accumulator.Add("[pgssub @ 0x5f38] Unknown subtitle segment type 0x78, length 55981");
        accumulator.Add("[pgssub @ 0x5f38] Unknown subtitle segment type 0x93, length 2183");
        accumulator.Add("[aac @ 0x62] Prediction is not allowed in AAC-LC");

        _ = accumulator.Result.Should().Be(VerifyResult.DecodeError);
        _ = accumulator.Errors.Should().HaveCount(2);
        _ = accumulator
            .Errors[0]
            .Should()
            .Be("[pgssub @ 0x5f38] Unknown subtitle segment type 0x78, length 55981");
        _ = accumulator.Errors[1].Should().Be("[aac @ 0x62] Prediction is not allowed in AAC-LC");
    }
}
