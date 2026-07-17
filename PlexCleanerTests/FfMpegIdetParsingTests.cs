using System.Diagnostics.CodeAnalysis;
using AwesomeAssertions;
using PlexCleaner;
using PlexCleanerTests;
using Xunit;
using Xunit.Sdk;

[assembly: RegisterXunitSerializer(typeof(FfMpegIdetInfoSerializer))]

namespace PlexCleanerTests;

public class FfMpegIdetParsingTests
{
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
            {
                // idet can emit an early all-zero pass before the final counts; the parser must
                // use the last triple (real ffmpeg 7.1.5 output from a progressive DVD rip)
                new string(
                    """
                    [Parsed_idet_0 @ 0x568fce0a2b00] Repeated Fields: Neither:     0 Top:     0 Bottom:     0
                    [Parsed_idet_0 @ 0x568fce0a2b00] Single frame detection: TFF:     0 BFF:     0 Progressive:     0 Undetermined:     0
                    [Parsed_idet_0 @ 0x568fce0a2b00] Multi frame detection: TFF:     0 BFF:     0 Progressive:     0 Undetermined:     0
                    Stream mapping:
                      Stream #0:0 -> #0:0 (hevc (native) -> wrapped_avframe (native))
                    [Parsed_idet_0 @ 0x719af8004500] Repeated Fields: Neither: 43870 Top:     1 Bottom:     0
                    [Parsed_idet_0 @ 0x719af8004500] Single frame detection: TFF:    21 BFF:    21 Progressive: 25267 Undetermined: 18562
                    [Parsed_idet_0 @ 0x719af8004500] Multi frame detection: TFF:     0 BFF:    11 Progressive: 43831 Undetermined:    29
                    """
                ),
                new FfMpegIdetInfo
                {
                    RepeatedFields = new FfMpegIdetInfo.Repeated
                    {
                        Neither = 43870,
                        Top = 1,
                        Bottom = 0,
                    },
                    SingleFrame = new FfMpegIdetInfo.Frames
                    {
                        Tff = 21,
                        Bff = 21,
                        Progressive = 25267,
                        Undetermined = 18562,
                    },
                    MultiFrame = new FfMpegIdetInfo.Frames
                    {
                        Tff = 0,
                        Bff = 11,
                        Progressive = 43831,
                        Undetermined = 29,
                    },
                }
            },
            {
                // ffmpeg interleaves other stderr lines between the idet stats; on a source with
                // non-monotonic DTS the -f null muxer emits "non monotonically increasing dts" warnings
                // between the three lines (the VC1 regression). The three lines must parse independently
                new string(
                    """
                    [Parsed_idet_0 @ 0x7ec7fc004280] Repeated Fields: Neither:139809 Top:     2 Bottom:     0
                    [null @ 0x5a4c] Application provided invalid, non monotonically increasing dts to muxer in stream 0: 42 >= 42
                    [Parsed_idet_0 @ 0x7ec7fc004280] Single frame detection: TFF:   333 BFF:   259 Progressive:138387 Undetermined:   832
                    [null @ 0x5a4c] Application provided invalid, non monotonically increasing dts to muxer in stream 0: 43 >= 43
                    [Parsed_idet_0 @ 0x7ec7fc004280] Multi frame detection: TFF:     4 BFF:    44 Progressive:139709 Undetermined:    54
                    """
                ),
                new FfMpegIdetInfo
                {
                    RepeatedFields = new FfMpegIdetInfo.Repeated
                    {
                        Neither = 139809,
                        Top = 2,
                        Bottom = 0,
                    },
                    SingleFrame = new FfMpegIdetInfo.Frames
                    {
                        Tff = 333,
                        Bff = 259,
                        Progressive = 138387,
                        Undetermined = 832,
                    },
                    MultiFrame = new FfMpegIdetInfo.Frames
                    {
                        Tff = 4,
                        Bff = 44,
                        Progressive = 139709,
                        Undetermined = 54,
                    },
                }
            },
        };

    [Theory]
    [MemberData(nameof(Data))]
    [SuppressMessage(
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
}
