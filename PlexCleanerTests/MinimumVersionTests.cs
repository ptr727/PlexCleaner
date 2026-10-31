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
    [InlineData("20230223192356-5c2b5d2d0-1.6.x")]
    [InlineData("N-121234-g0123456789")]
    [InlineData("")]
    public void IsSupportedVersion_Unparsable_PassesGate(string version) =>
        _ = MediaTool
            .IsSupportedVersion(version, new HandBrake.Tool().MinimumVersion)
            .Should()
            .BeTrue();

    [Theory]
    [InlineData("HandBrake 1.9.2", false)]
    [InlineData("HandBrake 1.10.0", true)]
    [InlineData("HandBrake 20230223192356-5c2b5d2d0-1.6.x", true)]
    public void GetVersion_HandBrake_GatesOnMinimum(string line, bool expected)
    {
        MediaToolInfo mediaToolInfo = new();
        _ = HandBrake.Tool.GetVersion(line, mediaToolInfo).Should().BeTrue();
        _ = MediaTool
            .IsSupportedVersion(mediaToolInfo.Version, new HandBrake.Tool().MinimumVersion)
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
