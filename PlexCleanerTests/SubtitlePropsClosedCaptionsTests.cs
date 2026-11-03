using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class SubtitlePropsClosedCaptionsTests
{
    private static MediaProps NewMediaProps(params long[] videoNumbers)
    {
        MediaProps mediaProps = new(MediaTool.ToolType.MediaInfo, "test.mkv")
        {
            Container = "Matroska",
        };
        foreach (long number in videoNumbers)
        {
            mediaProps.Video.Add(new VideoProps(mediaProps) { Number = number });
        }
        return mediaProps;
    }

    private static MediaInfoToolJsonSchema.Track NewCaptionTrack(string id, string muxingMode) =>
        new()
        {
            Format = "EIA-608",
            Id = id,
            MuxingMode = muxingMode,
        };

    [Fact]
    public void Embedded_Captions_Flag_The_Matching_Video_Track()
    {
        MediaProps mediaProps = NewMediaProps(256);
        SubtitleProps props = new(mediaProps);

        _ = props.Create(NewCaptionTrack("256-CC1", "A/53 / DTVCC Transport")).Should().BeFalse();

        _ = mediaProps.Video[0].ClosedCaptions.Should().BeTrue();
    }

    [Fact]
    public void Embedded_Captions_Without_A_Matching_Video_Track_Are_Dropped()
    {
        MediaProps mediaProps = NewMediaProps(100);
        SubtitleProps props = new(mediaProps);

        _ = props.Create(NewCaptionTrack("256-CC1", "A/53 / DTVCC Transport")).Should().BeFalse();

        _ = mediaProps.Video[0].ClosedCaptions.Should().BeFalse();
    }

    [Fact]
    public void Unparsable_Caption_Track_Id_Is_Dropped()
    {
        MediaProps mediaProps = NewMediaProps(256);
        SubtitleProps props = new(mediaProps);

        _ = props.Create(NewCaptionTrack("CC", "A/53 / DTVCC Transport")).Should().BeFalse();

        _ = mediaProps.Video[0].ClosedCaptions.Should().BeFalse();
    }

    [Theory]
    [InlineData("SCTE 128 / DTVCC Transport")]
    [InlineData("Final Cut")]
    public void Separate_Or_Unknown_Captions_Are_Subtitle_Tracks(string muxingMode)
    {
        MediaProps mediaProps = NewMediaProps(256);
        SubtitleProps props = new(mediaProps);

        _ = props.Create(NewCaptionTrack("256-CC1", muxingMode)).Should().BeTrue();

        _ = props.Format.Should().Be("EIA-608");
        _ = props.Codec.Should().Be(muxingMode);
        _ = props.Number.Should().Be(256);
        _ = mediaProps.Video[0].ClosedCaptions.Should().BeFalse();
    }

    [Theory]
    [InlineData("", TrackProps.StateType.Remove)]
    [InlineData("zlib", TrackProps.StateType.None)]
    public void VobSub_Without_MuxingMode_Is_Removed(string muxingMode, TrackProps.StateType state)
    {
        MediaInfoToolJsonSchema.Track track = new()
        {
            Format = "VobSub",
            CodecId = "S_VOBSUB",
            MuxingMode = muxingMode,
        };
        SubtitleProps props = new(NewMediaProps());

        _ = props.Create(track).Should().BeTrue();

        _ = props.State.Should().Be(state);
        _ = props.HasErrors.Should().Be(state == TrackProps.StateType.Remove);
    }
}
