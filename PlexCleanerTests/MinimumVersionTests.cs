using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class MinimumVersionTests
{
    public static TheoryData<MediaTool.ToolType, string> Minimums =>
        new()
        {
            { MediaTool.ToolType.FfMpeg, "8.0" },
            { MediaTool.ToolType.FfProbe, "8.0" },
            { MediaTool.ToolType.HandBrake, "1.10.0" },
            { MediaTool.ToolType.MediaInfo, "25.07" },
            { MediaTool.ToolType.MkvMerge, "94.0" },
            { MediaTool.ToolType.MkvPropEdit, "94.0" },
            { MediaTool.ToolType.SevenZip, "25.01" },
        };

    public static TheoryData<MediaTool.ToolType, string> OlderVersions =>
        new()
        {
            { MediaTool.ToolType.FfMpeg, "7.1.1" },
            { MediaTool.ToolType.FfProbe, "7.1.1" },
            { MediaTool.ToolType.HandBrake, "1.9.2" },
            { MediaTool.ToolType.MediaInfo, "25.04" },
            { MediaTool.ToolType.MkvMerge, "93.0" },
            { MediaTool.ToolType.MkvPropEdit, "93.0" },
            { MediaTool.ToolType.SevenZip, "25.00" },
        };

    private static MediaTool GetTool(MediaTool.ToolType toolType) =>
        Tools.GetToolList().Single(tool => tool.GetToolType() == toolType);

    [Fact]
    public void MinimumVersion_EveryTool_HasMinimum()
    {
        foreach (MediaTool tool in Tools.GetToolList())
        {
            _ = tool.MinimumVersion.Should().NotBeNull(tool.GetToolType().ToString());
        }
    }

    [Theory]
    [MemberData(nameof(Minimums))]
    public void IsSupportedVersion_Minimum_Accepted(MediaTool.ToolType toolType, string minimum)
    {
        MediaTool tool = GetTool(toolType);
        _ = tool.MinimumVersion.Should().Be(Version.Parse(minimum));
        _ = MediaTool.IsSupportedVersion(minimum, tool.MinimumVersion).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(OlderVersions))]
    public void IsSupportedVersion_Older_Refused(MediaTool.ToolType toolType, string version) =>
        _ = MediaTool
            .IsSupportedVersion(version, GetTool(toolType).MinimumVersion)
            .Should()
            .BeFalse();

    [Theory]
    [InlineData("20230223192356-5c2b5d2d0-1.6.x", MediaTool.VersionGate.Unsupported)]
    [InlineData("20250901123456-5c2b5d2d0-1.10.x", MediaTool.VersionGate.Supported)]
    [InlineData("20250901123456-5c2b5d2d0-master", MediaTool.VersionGate.Supported)]
    [InlineData("20250101000000-5c2b5d2d0-master", MediaTool.VersionGate.Unsupported)]
    [InlineData("1.11.2", MediaTool.VersionGate.Supported)]
    [InlineData("1.9.2-dev", MediaTool.VersionGate.Unsupported)]
    [InlineData("1.10.1-dev", MediaTool.VersionGate.Supported)]
    [InlineData("", MediaTool.VersionGate.Unknown)]
    [InlineData("custom-build", MediaTool.VersionGate.Unknown)]
    public void CheckVersion_HandBrake_DevelopmentBuilds(
        string version,
        MediaTool.VersionGate expected
    )
    {
        HandBrake.Tool tool = new();
        _ = MediaTool
            .CheckVersion(version, tool.MinimumVersion, tool.MinimumDate)
            .Should()
            .Be(expected);
    }

    [Theory]
    [InlineData("2025-08-25-git-abc123-full_build-www.gyan.dev", MediaTool.VersionGate.Supported)]
    [InlineData("2025-08-01-git-abc123-full_build-www.gyan.dev", MediaTool.VersionGate.Unsupported)]
    [InlineData("N-121061-g0123456789", MediaTool.VersionGate.Unknown)]
    public void CheckVersion_FfMpeg_DevelopmentBuilds(
        string version,
        MediaTool.VersionGate expected
    )
    {
        FfMpeg.Tool tool = new();
        _ = MediaTool
            .CheckVersion(version, tool.MinimumVersion, tool.MinimumDate)
            .Should()
            .Be(expected);
    }

    [Theory]
    [InlineData(
        "ffmpeg version N-121061-g0123456789 Copyright (c) 2000-2025",
        "N-121061-g0123456789"
    )]
    [InlineData(
        "ffmpeg version 2025-08-25-git-abc123-full_build-www.gyan.dev Copyright (c) 2000-2025",
        "2025-08-25-git-abc123-full_build-www.gyan.dev"
    )]
    public void GetVersion_FfMpeg_DevelopmentBuild_KeepsBuildString(string line, string version)
    {
        MediaToolInfo mediaToolInfo = new();
        _ = FfMpeg.Tool.GetVersion(line, mediaToolInfo).Should().BeTrue();
        _ = mediaToolInfo.Version.Should().Be(version);
    }

    [Theory]
    [InlineData("HandBrake 1.9.2", false)]
    [InlineData("HandBrake 1.10.0", true)]
    [InlineData("HandBrake 20230223192356-5c2b5d2d0-1.6.x", false)]
    [InlineData("HandBrake 20250901123456-5c2b5d2d0-master", true)]
    [InlineData("HandBrake 20200101000000-5c2b5d2d0-master", false)]
    public void GetVersion_HandBrake_GatesOnMinimum(string line, bool expected)
    {
        MediaToolInfo mediaToolInfo = new();
        _ = HandBrake.Tool.GetVersion(line, mediaToolInfo).Should().BeTrue();
        _ = MediaTool
            .IsSupportedVersion(
                mediaToolInfo.Version,
                new HandBrake.Tool().MinimumVersion,
                new HandBrake.Tool().MinimumDate
            )
            .Should()
            .Be(expected);
    }

    [Theory]
    [InlineData("MediaInfoLib - v25.04", false)]
    [InlineData("MediaInfoLib - v25.07", true)]
    [InlineData("MediaInfoLib - v26.05", true)]
    public void GetVersion_MediaInfo_GatesOnMinimum(string line, bool expected)
    {
        MediaToolInfo mediaToolInfo = new();
        _ = MediaInfo
            .Tool.GetVersion($"MediaInfo Command line,{Environment.NewLine}{line}", mediaToolInfo)
            .Should()
            .BeTrue();
        _ = MediaTool
            .IsSupportedVersion(mediaToolInfo.Version, new MediaInfo.Tool().MinimumVersion)
            .Should()
            .Be(expected);
    }

    [Theory]
    [InlineData("mkvmerge v93.0 ('Goblu') 64-bit", false)]
    [InlineData("mkvmerge v94.0 ('Initiate') 64-bit", true)]
    [InlineData("mkvmerge v100.0 ('Example') 64-bit", true)]
    [InlineData("mkvmerge v93.0-12-g1a2b3c4 ('Goblu') 64-bit", false)]
    public void GetVersion_MkvMerge_GatesOnMinimum(string line, bool expected)
    {
        MediaToolInfo mediaToolInfo = new();
        _ = MkvMerge.Tool.GetVersion(line, mediaToolInfo).Should().BeTrue();
        _ = MediaTool
            .IsSupportedVersion(mediaToolInfo.Version, new MkvMerge.Tool().MinimumVersion)
            .Should()
            .Be(expected);
    }

    [Theory]
    [InlineData("7-Zip [64] 16.02 : Copyright (c) 1999-2016 Igor Pavlov : 2016-05-21", false)]
    [InlineData("7-Zip 25.00 (x64) : Copyright (c) 1999-2025 Igor Pavlov : 2025-07-05", false)]
    [InlineData("7-Zip 25.01 (x64) : Copyright (c) 1999-2025 Igor Pavlov : 2025-08-03", true)]
    [InlineData("7-Zip 26.02 (x64) : Copyright (c) 1999-2026 Igor Pavlov : 2026-06-25", true)]
    public void GetVersion_SevenZip_GatesOnMinimum(string line, bool expected)
    {
        MediaToolInfo mediaToolInfo = new();
        _ = SevenZip.Tool.GetVersion(line, mediaToolInfo).Should().BeTrue();
        _ = MediaTool
            .IsSupportedVersion(mediaToolInfo.Version, new SevenZip.Tool().MinimumVersion)
            .Should()
            .Be(expected);
    }
}
