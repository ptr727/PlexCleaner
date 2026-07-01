using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class DefaultTrackFlagsTests
{
    private static MediaProps NewProps() => new(MediaTool.ToolType.MkvMerge, "test.mkv");

    private static AudioProps AddAudio(MediaProps props, long number, string format, bool isDefault)
    {
        AudioProps track = new(props)
        {
            Number = number,
            Format = format,
            Flags = isDefault ? TrackProps.FlagsType.Default : TrackProps.FlagsType.None,
        };
        props.Audio.Add(track);
        return track;
    }

    private static VideoProps AddVideo(MediaProps props, long number, bool isDefault)
    {
        VideoProps track = new(props)
        {
            Number = number,
            Flags = isDefault ? TrackProps.FlagsType.Default : TrackProps.FlagsType.None,
        };
        props.Video.Add(track);
        return track;
    }

    private static SubtitleProps AddSubtitle(MediaProps props, long number, bool isDefault)
    {
        SubtitleProps track = new(props)
        {
            Number = number,
            Flags = isDefault ? TrackProps.FlagsType.Default : TrackProps.FlagsType.None,
        };
        props.Subtitle.Add(track);
        return track;
    }

    [Fact]
    public void LoneDefaultTrack_IsCleared()
    {
        MediaProps props = NewProps();
        _ = AddVideo(props, 1, isDefault: true);
        AudioProps audio = AddAudio(props, 2, "ac-3", isDefault: true);

        // Single track of a type with a default flag is redundant
        List<TrackProps> clear = ProcessFile.FindRedundantDefaultTracks(props);

        _ = clear.Should().HaveCount(2);
        _ = clear.Should().Contain(audio);
    }

    [Fact]
    public void MultipleAudioDefaults_KeepsOne_ClearsRest()
    {
        MediaProps props = NewProps();
        AudioProps first = AddAudio(props, 1, "fmt-a", isDefault: true);
        AudioProps second = AddAudio(props, 2, "fmt-b", isDefault: true);

        // Two audio defaults, non-preferred formats, keeper is the first
        List<TrackProps> clear = ProcessFile.FindRedundantDefaultTracks(props);

        _ = clear.Should().ContainSingle().Which.Should().Be(second);
        _ = clear.Should().NotContain(first);
    }

    [Fact]
    public void MultipleSubtitleDefaults_ClearsAll()
    {
        MediaProps props = NewProps();
        SubtitleProps first = AddSubtitle(props, 1, isDefault: true);
        SubtitleProps second = AddSubtitle(props, 2, isDefault: true);

        // Subtitles never keep a default
        List<TrackProps> clear = ProcessFile.FindRedundantDefaultTracks(props);

        _ = clear.Should().HaveCount(2);
        _ = clear.Should().Contain([first, second]);
    }

    [Fact]
    public void SingleDefaultAmongMany_IsUnchanged()
    {
        MediaProps props = NewProps();
        _ = AddAudio(props, 1, "ac-3", isDefault: true);
        _ = AddAudio(props, 2, "aac", isDefault: false);
        _ = AddSubtitle(props, 3, isDefault: false);
        _ = AddSubtitle(props, 4, isDefault: false);

        // Exactly one audio default and zero subtitle defaults are already correct
        List<TrackProps> clear = ProcessFile.FindRedundantDefaultTracks(props);

        _ = clear.Should().BeEmpty();
    }

    [Fact]
    public void NoDefaults_IsUnchanged()
    {
        MediaProps props = NewProps();
        _ = AddVideo(props, 1, isDefault: false);
        _ = AddAudio(props, 2, "ac-3", isDefault: false);
        _ = AddSubtitle(props, 3, isDefault: false);

        List<TrackProps> clear = ProcessFile.FindRedundantDefaultTracks(props);

        _ = clear.Should().BeEmpty();
    }
}
