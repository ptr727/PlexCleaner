using System;
using FluentAssertions;
using PlexCleaner;
using Xunit;
using Xunit.Sdk;

[assembly: RegisterXunitSerializer(typeof(PlexCleanerTests.FfMpegIdetInfoSerializer))]

namespace PlexCleanerTests;

public class FfMpegIdetParsingTests(PlexCleanerFixture fixture)
{
    private readonly PlexCleanerFixture _fixture = fixture;

    [Theory]
    [MemberData(nameof(Data))]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage",
        "xUnit1045:Avoid using TheoryData type arguments that might not be serializable",
        Justification = "FfMpegIdetInfoSerializer"
    )]
    public void Parse_Idet_Field_Test(string text, FfMpegIdetInfo idetInfo)
    {
        // Follow same pattern as in FfMpegIdetInfo.Parse()
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        System.Text.RegularExpressions.Match match = FfMpegIdetInfo.IdetRegex().Match(text);
        _ = match.Success.Should().BeTrue();

        _ = idetInfo
            .RepeatedFields.Neither.Should()
            .Be(FfMpegIdetInfo.ParseGroupInt(match, "repeated_neither"));
        _ = idetInfo
            .RepeatedFields.Top.Should()
            .Be(FfMpegIdetInfo.ParseGroupInt(match, "repeated_top"));
        _ = idetInfo
            .RepeatedFields.Bottom.Should()
            .Be(FfMpegIdetInfo.ParseGroupInt(match, "repeated_bottom"));

        _ = idetInfo.SingleFrame.Tff.Should().Be(FfMpegIdetInfo.ParseGroupInt(match, "single_tff"));
        _ = idetInfo.SingleFrame.Bff.Should().Be(FfMpegIdetInfo.ParseGroupInt(match, "single_bff"));
        _ = idetInfo
            .SingleFrame.Progressive.Should()
            .Be(FfMpegIdetInfo.ParseGroupInt(match, "single_prog"));
        _ = idetInfo
            .SingleFrame.Undetermined.Should()
            .Be(FfMpegIdetInfo.ParseGroupInt(match, "single_und"));

        _ = idetInfo.MultiFrame.Tff.Should().Be(FfMpegIdetInfo.ParseGroupInt(match, "multi_tff"));
        _ = idetInfo.MultiFrame.Bff.Should().Be(FfMpegIdetInfo.ParseGroupInt(match, "multi_bff"));
        _ = idetInfo
            .MultiFrame.Progressive.Should()
            .Be(FfMpegIdetInfo.ParseGroupInt(match, "multi_prog"));
        _ = idetInfo
            .MultiFrame.Undetermined.Should()
            .Be(FfMpegIdetInfo.ParseGroupInt(match, "multi_und"));
    }

    [Theory]
    [MemberData(nameof(Data))]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage",
        "xUnit1045:Avoid using TheoryData type arguments that might not be serializable",
        Justification = "FfMpegIdetInfoSerializer"
    )]
    public void Parse_Idet_Parse_Test(string text, FfMpegIdetInfo idetInfo)
    {
        FfMpegIdetInfo testIdetInfo = new();
        _ = testIdetInfo.Parse(text).Should().BeTrue();
        _ = testIdetInfo.Should().BeEquivalentTo(idetInfo);
    }

    public static TheoryData<string, FfMpegIdetInfo> Data =>
        new()
        {
            {
                new string(
                    """
                    [Parsed_idet_0 @ 000001c11e8aef00] Repeated Fields: Neither: 76434 Top:     0 Bottom:     0
                    [Parsed_idet_0 @ 000001c11e8aef00] Single frame detection: TFF:   560 BFF:  6353 Progressive: 64750 Undetermined:  4771
                    [Parsed_idet_0 @ 000001c11e8aef00] Multi frame detection: TFF:   610 BFF:  6459 Progressive: 69231 Undetermined:   134
                    """
                ),
                new FfMpegIdetInfo
                {
                    RepeatedFields = new FfMpegIdetInfo.Repeated
                    {
                        Neither = 76434,
                        Top = 0,
                        Bottom = 0,
                    },
                    SingleFrame = new FfMpegIdetInfo.Frames
                    {
                        Tff = 560,
                        Bff = 6353,
                        Progressive = 64750,
                        Undetermined = 4771,
                    },
                    MultiFrame = new FfMpegIdetInfo.Frames
                    {
                        Tff = 610,
                        Bff = 6459,
                        Progressive = 69231,
                        Undetermined = 134,
                    },
                }
            },
        };
}
