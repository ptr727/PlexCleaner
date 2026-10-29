using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class FfMpegVerifyCommandTests
{
    [Theory]
    [InlineData("9.0", true)]
    [InlineData("8.1.2", true)]
    [InlineData("7.1.1", true)]
    [InlineData("6.1", true)]
    [InlineData("6.1.1", true)]
    [InlineData("6.0", false)]
    [InlineData("5.1.6", false)]
    [InlineData("4.4.2", false)]
    [InlineData("", true)]
    [InlineData("N-121234-g0123456789", true)]
    public void IsSupportedVersion_FfMpegMinimum(string version, bool expected) =>
        _ = MediaTool
            .IsSupportedVersion(version, new FfMpeg.Tool().MinimumVersion)
            .Should()
            .Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData("1.0")]
    public void IsSupportedVersion_NoMinimum_True(string version) =>
        _ = MediaTool.IsSupportedVersion(version, null).Should().BeTrue();

    [Theory]
    [InlineData("ffmpeg version 9.0-1ubuntu1 Copyright (c) 2000-2026", "9.0", true)]
    [InlineData("ffmpeg version n9.0-1-g0123456789-20261001 Copyright (c)", "9.0", true)]
    [InlineData("ffmpeg version 8.1.2-2ubuntu2 Copyright (c) 2000-2026", "8.1.2", true)]
    [InlineData("ffmpeg version 7.1.1-full_build-www.gyan.dev Copyright", "7.1.1", true)]
    [InlineData("ffmpeg version 6.1.1-3ubuntu5 Copyright (c) 2000-2023", "6.1.1", true)]
    [InlineData("ffmpeg version 5.1.6-0+deb12u1 Copyright (c) 2000-2024", "5.1.6", false)]
    [InlineData("ffmpeg version 4.4.2-0ubuntu0.22.04.1 Copyright (c) 2000", "4.4.2", false)]
    public void GetVersion_DistroVersionLine_GatesOnMinimum(
        string versionLine,
        string expectedVersion,
        bool expectedSupported
    )
    {
        MediaToolInfo mediaToolInfo = new();
        _ = FfMpeg.Tool.GetVersion(versionLine, mediaToolInfo).Should().BeTrue();

        _ = mediaToolInfo.Version.Should().Be(expectedVersion);
        _ = MediaTool
            .IsSupportedVersion(mediaToolInfo.Version, new FfMpeg.Tool().MinimumVersion)
            .Should()
            .Be(expectedSupported);
    }

    [Fact]
    public void GetVerifyMediaCommand_EncodesVideoInDemuxTimeBase()
    {
        string arguments = FfMpeg.Tool.GetVerifyMediaCommand("ffmpeg", "media.mkv").Arguments;

        _ = arguments.Should().Contain("-enc_time_base:v demux -f null -");
        _ = arguments.Should().NotContain("-enc_time_base:a");
        _ = arguments
            .IndexOf("-enc_time_base:v", StringComparison.Ordinal)
            .Should()
            .BeGreaterThan(arguments.IndexOf("-i ", StringComparison.Ordinal));
    }

    [Fact]
    public void GetVerifyMediaCommand_ExitsOnError()
    {
        string arguments = FfMpeg.Tool.GetVerifyMediaCommand("ffmpeg", "media.mkv").Arguments;

        _ = arguments.Should().Contain("-xerror");
        _ = arguments.Should().Contain("-loglevel error");
    }
}
