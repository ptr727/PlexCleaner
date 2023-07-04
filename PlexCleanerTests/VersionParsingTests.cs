using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class VersionParsingTests : IClassFixture<PlexCleanerTests>
{
    [Theory]
    [InlineData("ffmpeg version 4.3.1-2020-11-19-full_build-www.gyan.dev Copyright (c) 2000-2020 the FFmpeg developers", "4.3.1")]
    [InlineData("ffmpeg version 4.3.1-1ubuntu0~20.04.sav1 Copyright (c) 2000-2020 the FFmpeg developers", "4.3.1")]
    [InlineData("ffmpeg version n6.0 Copyright (c) 2000-2023 the FFmpeg developers", "6.0")]
    [InlineData("ffprobe version 4.3.1-2020-11-19-full_build-www.gyan.dev Copyright (c) 2000-2020 the FFmpeg developers", "4.3.1")]
    [InlineData("ffprobe version 4.3.1-1ubuntu0~20.04.sav1 Copyright (c) 2000-2020 the FFmpeg developers", "4.3.1")]
    [InlineData("ffprobe version n6.0 Copyright (c) 2000-2023 the FFmpeg developers", "6.0")]
    [InlineData("ffmpeg version 6.0-0ubuntu1~22.04.sav1.1 Copyright (c) 2000-2023", "6.0")]
    [InlineData("ffmpeg version 5.1.2-3 Copyright (c) 2000-2022", "5.1.2")]
    public void Parse_FfMpeg_Installed_Version(string line, string version)
    {
        var match = FfMpegTool.InstalledVersionRegex().Match(line);
        Assert.True(match.Success);
        Assert.Equal(version, match.Groups["version"].Value);
    }

    [Theory]
    [InlineData("version: 6.0", "6.0")]
    public void Parse_FfMpeg_Latest_Linux_Version(string line, string version)
    {
        var match = FfMpegTool.LinuxVersionRegex().Match(line);
        Assert.True(match.Success);
        Assert.Equal(version, match.Groups["version"].Value);
    }

    // https://johnvansickle.com/ffmpeg/release-readme.txt
    [Theory]
    [InlineData("build: ffmpeg-6.0-amd64-static.tar.xz", "ffmpeg-6.0-amd64-static.tar.xz")]
    public void Parse_FfMpeg_Latest_Linux_Build(string line, string version)
    {
        var match = FfMpegTool.LinuxBuildRegex().Match(line);
        Assert.True(match.Success);
        Assert.Equal(version, match.Groups["build"].Value);
    }

    [Theory]
    [InlineData("HandBrake 1.3.3", "1.3.3")]
    [InlineData("HandBrake 20230223192356-5c2b5d2d0-1.6.x", "20230223192356-5c2b5d2d0-1.6.x")]
    public void Parse_HandBrake_Installed_Version(string line, string version)
    {
        var match = HandBrakeTool.InstalledVersionRegex().Match(line);
        Assert.True(match.Success);
        Assert.Equal(version, match.Groups["version"].Value);
    }

    [Theory]
    [InlineData("MediaInfoLib - v20.09", "20.09")]
    [InlineData("MediaInfo Command line, MediaInfoLib - v23.03", "23.03")]
    public void Parse_MediaInfo_Installed_Version(string line, string version)
    {
        var match = MediaInfoTool.InstalledVersionRegex().Match(line);
        Assert.True(match.Success);
        Assert.Equal(version, match.Groups["version"].Value);
    }

    // https://raw.githubusercontent.com/MediaArea/MediaInfo/master/History_CLI.txt
    [Theory]
    [InlineData("Version 23.03, 2023-03-29", "23.03")]
    public void Parse_MediaInfo_Latest_Windows_Version(string line, string version)
    {
        var match = MediaInfoTool.LatestVersionRegex().Match(line);
        Assert.True(match.Success);
        Assert.Equal(version, match.Groups["version"].Value);
    }

    [Theory]
    [InlineData("mkvmerge v51.0.0 ('I Wish') 64-bit", "51.0.0")]
    public void Parse_MkvMerge_Installed_Version(string line, string version)
    {
        var match = MkvMergeTool.InstalledVersionRegex().Match(line);
        Assert.True(match.Success);
        Assert.Equal(version, match.Groups["version"].Value);
    }

    [Theory]
    [InlineData("7-Zip (a) 19.00 (x64) : Copyright (c) 1999-2018 Igor Pavlov : 2019-02-21", "19.00")]
    [InlineData("7-Zip [64] 16.02 : Copyright (c) 1999-2016 Igor Pavlov : 2016-05-21", "16.02")]
    public void Parse_SevenZip_Installed_Version(string line, string version)
    {
        var match = SevenZipTool.InstalledVersionRegex().Match(line);
        Assert.True(match.Success);
        Assert.Equal(version, match.Groups["version"].Value);
    }

    // https://www.7-zip.org/download.html
    [Theory]
    [InlineData("Download 7-Zip 19.00 (2019-02-21) for Windows:", "19.00")]
    [InlineData("Download 7-Zip 22.01 (2022-07-15):", "22.01")]
    public void Parse_SevenZip_Latest_Windows_Version(string line, string version)
    {
        var match = SevenZipTool.LatestVersionRegex().Match(line);
        Assert.True(match.Success);
        Assert.Equal(version, $"{match.Groups["major"].Value}.{match.Groups["minor"].Value}");
    }
}
