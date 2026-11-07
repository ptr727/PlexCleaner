using AwesomeAssertions;
using PlexCleaner;
using Serilog.Events;
using Xunit;

namespace PlexCleanerTests;

[Collection("Sequential")]
public class TrackPropsCreateTests
{
    private const string MkvContainer = "matroska";
    private const string Mp4Container = "mov,mp4,m4a,3gp,3g2,mj2";

    private static SubtitleProps NewTrack(MediaTool.ToolType parser, string container) =>
        new(new MediaProps(parser, "test.mkv") { Container = container });

    private static MkvToolJsonSchema.Track NewMkvTrack(string codec, string codecId)
    {
        MkvToolJsonSchema.Track track = new() { Codec = codec, Id = 2 };
        track.Properties.CodecId = codecId;
        track.Properties.Number = 3;
        track.Properties.Uid = 4;
        return track;
    }

    [Fact]
    public void MkvMerge_Non_Mkv_Container_Missing_Codec_Is_Unknown()
    {
        SubtitleProps props = NewTrack(MediaTool.ToolType.MkvMerge, Mp4Container);

        _ = props.Create(NewMkvTrack(string.Empty, string.Empty)).Should().BeTrue();

        _ = props.Format.Should().Be("unknown");
        _ = props.Codec.Should().Be("unknown");
        _ = props.Id.Should().Be(2);
        _ = props.Number.Should().Be(3);
        _ = props.Uid.Should().Be(4UL);
    }

    [Fact]
    public void MkvMerge_Mkv_Container_Missing_Codec_Is_Unsupported()
    {
        SubtitleProps props = NewTrack(MediaTool.ToolType.MkvMerge, MkvContainer);

        _ = props.Create(NewMkvTrack("SubRip/SRT", string.Empty)).Should().BeFalse();

        _ = props.State.Should().Be(TrackProps.StateType.Unsupported);
        _ = props.HasErrors.Should().BeTrue();
    }

    [Fact]
    public void MkvMerge_Sets_Every_Flag()
    {
        MkvToolJsonSchema.Track track = NewMkvTrack("SubRip/SRT", "S_TEXT/UTF8");
        track.Properties.DefaultTrack = true;
        track.Properties.Original = true;
        track.Properties.Commentary = true;
        track.Properties.VisualImpaired = true;
        track.Properties.HearingImpaired = true;
        track.Properties.TextDescriptions = true;
        track.Properties.Forced = true;
        SubtitleProps props = NewTrack(MediaTool.ToolType.MkvMerge, MkvContainer);

        _ = props.Create(track).Should().BeTrue();

        _ = props
            .Flags.Should()
            .Be(
                TrackProps.FlagsType.Default
                    | TrackProps.FlagsType.Original
                    | TrackProps.FlagsType.Commentary
                    | TrackProps.FlagsType.VisualImpaired
                    | TrackProps.FlagsType.HearingImpaired
                    | TrackProps.FlagsType.Descriptions
                    | TrackProps.FlagsType.Forced
            );
    }

    [Fact]
    public void MkvMerge_Ietf_Language_Without_Iso_Sets_Iso_And_Needs_ReMux()
    {
        MkvToolJsonSchema.Track track = NewMkvTrack("SubRip/SRT", "S_TEXT/UTF8");
        track.Properties.LanguageIetf = "en";
        SubtitleProps props = NewTrack(MediaTool.ToolType.MkvMerge, MkvContainer);

        _ = props.Create(track).Should().BeTrue();

        _ = props.Language.Should().Be("eng");
        _ = props.HasErrors.Should().BeTrue();
        _ = props.Actions.Should().Be(TrackProps.ActionsType.ReMux);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("und", "und")]
    public void MkvMerge_Ietf_Language_Without_An_Iso_Equivalent_Needs_ReMux(
        string language,
        string expectedLanguage
    )
    {
        MkvToolJsonSchema.Track track = NewMkvTrack("SubRip/SRT", "S_TEXT/UTF8");
        track.Properties.Language = language;
        track.Properties.LanguageIetf = "pcm";
        SubtitleProps props = NewTrack(MediaTool.ToolType.MkvMerge, MkvContainer);
        bool created = false;

        IReadOnlyList<LogEvent> events = LogCapture.Capture(() => created = props.Create(track));

        _ = created.Should().BeTrue();
        _ = props.Language.Should().Be(expectedLanguage);
        _ = props.HasErrors.Should().BeTrue();
        _ = props.Actions.Should().Be(TrackProps.ActionsType.ReMux);
        _ = LogCapture
            .Messages(events, LogEventLevel.Error)
            .Should()
            .ContainSingle(message =>
                message.Contains("Failed to lookup ISO639 tag from IETF tag")
            );
    }

    [Fact]
    public void FfProbe_Non_Mkv_Container_Unknown_Codec_Uses_Codec_Tag()
    {
        FfMpegToolJsonSchema.Track track = new() { CodecTagString = "avc1" };
        SubtitleProps props = NewTrack(MediaTool.ToolType.FfProbe, Mp4Container);

        _ = props.Create(track).Should().BeTrue();

        _ = props.Format.Should().Be("avc1");
        _ = props.Codec.Should().Be("avc1");
    }

    [Fact]
    public void FfProbe_Mkv_Container_Missing_Codec_Is_Unsupported()
    {
        SubtitleProps props = NewTrack(MediaTool.ToolType.FfProbe, MkvContainer);

        _ = props.Create(new FfMpegToolJsonSchema.Track()).Should().BeFalse();

        _ = props.State.Should().Be(TrackProps.StateType.Unsupported);
        _ = props.HasErrors.Should().BeTrue();
    }

    [Fact]
    public void FfProbe_Sets_Impaired_And_Descriptions_Flags()
    {
        FfMpegToolJsonSchema.Track track = new()
        {
            CodecName = "subrip",
            CodecLongName = "SubRip subtitle",
        };
        track.Disposition.VisualImpaired = 1;
        track.Disposition.Descriptions = 1;
        SubtitleProps props = NewTrack(MediaTool.ToolType.FfProbe, MkvContainer);

        _ = props.Create(track).Should().BeTrue();

        _ = props
            .Flags.Should()
            .Be(TrackProps.FlagsType.VisualImpaired | TrackProps.FlagsType.Descriptions);
    }

    [Theory]
    [InlineData("", "V_MPEG4/ISO/AVC", "", "V_MPEG4/ISO/AVC", "V_MPEG4/ISO/AVC")]
    [InlineData("", "", "Header stripping", "Header stripping", "Header stripping")]
    [InlineData("AVC", "", "", "AVC", "AVC")]
    public void MediaInfo_Non_Mkv_Container_Fills_Missing_Codec(
        string trackFormat,
        string codecId,
        string muxingMode,
        string format,
        string codec
    )
    {
        MediaInfoToolJsonSchema.Track track = new()
        {
            Format = trackFormat,
            CodecId = codecId,
            MuxingMode = muxingMode,
        };
        AudioProps props = new(
            new MediaProps(MediaTool.ToolType.MediaInfo, "test.mp4") { Container = "MPEG-4" }
        );

        _ = props.Create(track).Should().BeTrue();

        _ = props.Format.Should().Be(format);
        _ = props.Codec.Should().Be(codec);
    }

    [Fact]
    public void MediaInfo_Mkv_Container_Missing_Codec_Is_Unsupported()
    {
        AudioProps props = new(
            new MediaProps(MediaTool.ToolType.MediaInfo, "test.mkv") { Container = "Matroska" }
        );

        _ = props.Create(new MediaInfoToolJsonSchema.Track()).Should().BeFalse();

        _ = props.State.Should().Be(TrackProps.StateType.Unsupported);
        _ = props.HasErrors.Should().BeTrue();
    }

    [Fact]
    public void MediaInfo_Missing_Ids_Default_To_Zero()
    {
        MediaInfoToolJsonSchema.Track track = new() { Format = "AAC", CodecId = "A_AAC" };
        AudioProps props = new(
            new MediaProps(MediaTool.ToolType.MediaInfo, "test.mkv") { Container = "Matroska" }
        );

        _ = props.Create(track).Should().BeTrue();

        _ = props.Id.Should().Be(0);
        _ = props.Number.Should().Be(0);
        _ = props.Uid.Should().Be(0UL);
    }

    [Theory]
    [InlineData("1-0", true, 1)]
    [InlineData("first", false, 0)]
    public void MediaInfo_Sub_Track_StreamOrder_Is_Sanitized_Or_Rejected(
        string streamOrder,
        bool created,
        long id
    )
    {
        MediaInfoToolJsonSchema.Track track = new()
        {
            Format = "AAC",
            CodecId = "A_AAC",
            StreamOrder = streamOrder,
        };
        AudioProps props = new(
            new MediaProps(MediaTool.ToolType.MediaInfo, "test.mkv") { Container = "Matroska" }
        );

        _ = props.Create(track).Should().Be(created);

        _ = props.Id.Should().Be(id);
    }
}
