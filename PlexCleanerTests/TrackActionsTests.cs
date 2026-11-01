using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

public class TrackActionsTests
{
    [Fact]
    public void ActionsType_EveryAction_IsInExactlyOneGroup()
    {
        foreach (
            TrackProps.ActionsType action in Enum.GetValues<TrackProps.ActionsType>()
                .Where(item => item != TrackProps.ActionsType.None)
        )
        {
            bool attribute = TrackProps.AttributeActions.HasFlag(action);
            bool stream = TrackProps.StreamActions.HasFlag(action);
            _ = (attribute ^ stream).Should().BeTrue($"{action} belongs to one group");
        }
    }

    [Fact]
    public void SetActions_AddsToSelectedAndClearsNotSelected()
    {
        MediaProps props = new(MediaTool.ToolType.FfProbe, "test.mkv");
        VideoProps video = new(props) { Actions = TrackProps.ActionsType.ReEncode };
        AudioProps audio = new(props) { Actions = TrackProps.ActionsType.SetFlags };
        props.Video.Add(video);
        props.Audio.Add(audio);

        SelectMediaProps selectMediaProps = new(props, item => item is AudioProps);
        selectMediaProps.SetActions(TrackProps.ActionsType.ReEncode);

        _ = audio
            .Actions.Should()
            .Be(TrackProps.ActionsType.SetFlags | TrackProps.ActionsType.ReEncode);
        _ = video.Actions.Should().Be(TrackProps.ActionsType.None);
    }

    [Fact]
    public void CreateTrackArgs_EncodesOnlyTracksWithReEncode()
    {
        MediaProps props = new(MediaTool.ToolType.FfProbe, "test.mkv");
        props.Video.Add(new VideoProps(props) { Id = 0 });
        props.Audio.Add(new AudioProps(props) { Id = 1 });
        props.Audio.Add(new AudioProps(props) { Id = 2 });

        SelectMediaProps selectMediaProps = new(props, item => item.Id == 1);
        selectMediaProps.SetState(TrackProps.StateType.Keep, TrackProps.StateType.Keep);
        selectMediaProps.SetActions(TrackProps.ActionsType.ReEncode);

        FfMpeg.Tool.CreateTrackArgs(selectMediaProps, out _, out string outputMap);

        _ = outputMap
            .Should()
            .Be(
                $"-c:v:0 copy -c:a:0 {Program.Config.ConvertOptions.FfMpegOptions.Audio} -c:a:1 copy"
            );
    }
}
