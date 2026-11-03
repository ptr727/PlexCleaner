using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class MediaPropsTests
{
    private static MediaProps NewMediaProps(params string[] videoFormats)
    {
        MediaProps mediaProps = new(MediaTool.ToolType.MkvMerge, "test.mkv");
        foreach (string format in videoFormats)
        {
            mediaProps.Video.Add(new VideoProps(mediaProps) { Format = format, Codec = format });
        }
        return mediaProps;
    }

    [Fact]
    public void RemoveCoverArt_Removes_Only_Cover_Art_Tracks()
    {
        MediaProps mediaProps = NewMediaProps("AVC", "PNG", "JPEG");

        mediaProps.RemoveCoverArt();

        _ = mediaProps.Video.Should().ContainSingle().Which.Format.Should().Be("AVC");
    }

    [Fact]
    public void RemoveCoverArt_Removes_Every_Track_When_All_Are_Cover_Art()
    {
        MediaProps mediaProps = NewMediaProps("PNG");

        mediaProps.RemoveCoverArt();

        _ = mediaProps.Video.Should().BeEmpty();
    }

    [Fact]
    public void RemoveCoverArt_Without_Video_Does_Nothing()
    {
        MediaProps mediaProps = NewMediaProps();

        mediaProps.RemoveCoverArt();

        _ = mediaProps.Video.Should().BeEmpty();
    }
}
