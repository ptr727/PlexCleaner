using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class VersionParsingTests(PlexCleanerTests fixture) : IClassFixture<PlexCleanerTests>
{
#pragma warning disable IDE0052 // Remove unread private members
    private readonly PlexCleanerTests _fixture = fixture;
#pragma warning restore IDE0052 // Remove unread private members

    [Theory]
    [InlineData("ffmpeg version 4.3.1-2020-11-19-full_build-www.gyan.dev Copyright (c) 2000-2020 the FFmpeg developers", "4.3.1")]
    [InlineData("ffmpeg version 4.3.1-1ubuntu0~20.04.sav1 Copyright (c) 2000-2020 the FFmpeg developers", "4.3.1")]
    [InlineData("ffmpeg version n6.0 Copyright (c) 2000-2023 the FFmpeg developers", "6.0")]
    [InlineData("ffprobe version 4.3.1-2020-11-19-full_build-www.gyan.dev Copyright (c) 2000-2020 the FFmpeg developers", "4.3.1")]
    [InlineData("ffprobe version 4.3.1-1ubuntu0~20.04.sav1 Copyright (c) 2000-2020 the FFmpeg developers", "4.3.1")]
    [InlineData("ffprobe version n6.0 Copyright (c) 2000-2023 the FFmpeg developers", "6.0")]
    [InlineData("ffmpeg version 6.0-0ubuntu1~22.04.sav1.1 Copyright (c) 2000-2023", "6.0")]
    [InlineData("ffmpeg version 5.1.2-3 Copyright (c) 2000-2022", "5.1.2")]
    public void ParseFfMpegInstalledVersion(string line, string version)
    {
        System.Text.RegularExpressions.Match match = FfMpegTool.InstalledVersionRegex().Match(line);
        Assert.True(match.Success);
        Assert.Equal(version, match.Groups["version"].Value);
    }

    [Theory]
    [InlineData("HandBrake 1.3.3", "1.3.3")]
    [InlineData("HandBrake 20230223192356-5c2b5d2d0-1.6.x", "20230223192356-5c2b5d2d0-1.6.x")]
    public void ParseHandBrakeInstalledVersion(string line, string version)
    {
        System.Text.RegularExpressions.Match match = HandBrakeTool.InstalledVersionRegex().Match(line);
        Assert.True(match.Success);
        Assert.Equal(version, match.Groups["version"].Value);
    }

    [Theory]
    [InlineData("MediaInfoLib - v20.09", "20.09")]
    [InlineData("MediaInfo Command line, MediaInfoLib - v23.03", "23.03")]
    public void ParseMediaInfoInstalledVersion(string line, string version)
    {
        System.Text.RegularExpressions.Match match = MediaInfoTool.InstalledVersionRegex().Match(line);
        Assert.True(match.Success);
        Assert.Equal(version, match.Groups["version"].Value);
    }

    [Theory]
    [InlineData("mkvmerge v51.0.0 ('I Wish') 64-bit", "51.0.0")]
    public void ParseMkvMergeInstalledVersion(string line, string version)
    {
        System.Text.RegularExpressions.Match match = MkvMergeTool.InstalledVersionRegex().Match(line);
        Assert.True(match.Success);
        Assert.Equal(version, match.Groups["version"].Value);
    }

    [Theory]
    [InlineData("7-Zip (a) 19.00 (x64) : Copyright (c) 1999-2018 Igor Pavlov : 2019-02-21", "19.00")]
    [InlineData("7-Zip [64] 16.02 : Copyright (c) 1999-2016 Igor Pavlov : 2016-05-21", "16.02")]
    public void ParseSevenZipInstalledVersion(string line, string version)
    {
        System.Text.RegularExpressions.Match match = SevenZipTool.InstalledVersionRegex().Match(line);
        Assert.True(match.Success);
        Assert.Equal(version, match.Groups["version"].Value);
    }
}
