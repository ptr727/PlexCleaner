using System;
using System.Diagnostics;
using System.Linq;
using CliWrap;
using CliWrap.Builders;

namespace PlexCleaner;

public partial class MkvMerge
{
    public class GlobalOptions(ArgumentsBuilder argumentsBuilder)
    {
        private readonly ArgumentsBuilder _argumentsBuilder = argumentsBuilder;

        public GlobalOptions Default() =>
            DisableTrackStatisticsTags().NormalizeLanguageIetfExtended();

        public GlobalOptions Quiet() => Add("--quiet");

        public GlobalOptions NormalizeLanguageIetf(string option) =>
            Add("--normalize-language-ietf").Add(option);

        // Normalize IETF tags to extended format, cmn-Hant -> zh-cmn-Hant
        public GlobalOptions NormalizeLanguageIetfExtended() => NormalizeLanguageIetf("extlang");

        public GlobalOptions DisableTrackStatisticsTags() => Add("--disable-track-statistics-tags");

        public GlobalOptions Add(string option) => Add(option, false);

        public GlobalOptions Add(string option, bool escape)
        {
            if (string.IsNullOrWhiteSpace(option))
            {
                return this;
            }
            _ = _argumentsBuilder.Add(option, escape);
            return this;
        }
    }

    public class InputOptions(ArgumentsBuilder argumentsBuilder)
    {
        private readonly ArgumentsBuilder _argumentsBuilder = argumentsBuilder;

        public InputOptions Default() => NoGlobalTags().NoTrackTags().NoAttachments().NoButtons();

        public InputOptions Identify(string option) => Add("--identify").Add($"\"{option}\"");

        public InputOptions NoGlobalTags() => Add("--no-global-tags");

        public InputOptions NoTrackTags() => Add("--no-track-tags");

        public InputOptions NoAttachments() => Add("--no-attachments");

        public InputOptions NoButtons() => Add("--no-buttons");

        public InputOptions VideoTracks(string option) => Add("--video-tracks").Add(option);

        public InputOptions AudioTracks(string option) => Add("--audio-tracks").Add(option);

        public InputOptions SubtitleTracks(string option) => Add("--subtitle-tracks").Add(option);

        public InputOptions NoVideo() => Add("--no-video");

        public InputOptions NoAudio() => Add("--no-audio");

        public InputOptions NoSubtitles() => Add("--no-subtitles");

        public InputOptions SelectTracks(MediaProps mediaProps)
        {
            // Verify correct media type
            Debug.Assert(mediaProps.Parser == MediaTool.ToolType.MkvMerge);

            // Create the track number filters
            // The track numbers are reported by MkvMerge --identify, use the track.id values
            _ =
                mediaProps.Video.Count > 0
                    ? VideoTracks(string.Join(",", mediaProps.Video.Select(item => $"{item.Id}")))
                    : NoVideo();
            _ =
                mediaProps.Audio.Count > 0
                    ? AudioTracks(string.Join(",", mediaProps.Audio.Select(item => $"{item.Id}")))
                    : NoAudio();
            _ =
                mediaProps.Subtitle.Count > 0
                    ? SubtitleTracks(
                        string.Join(",", mediaProps.Subtitle.Select(item => $"{item.Id}"))
                    )
                    : NoSubtitles();
            return this;
        }

        public InputOptions InputFile(string option) => Add($"\"{option}\"");

        public InputOptions Add(string option) => Add(option, false);

        public InputOptions Add(string option, bool escape)
        {
            if (string.IsNullOrWhiteSpace(option))
            {
                return this;
            }
            _ = _argumentsBuilder.Add(option, escape);
            return this;
        }
    }

    public class OutputOptions(ArgumentsBuilder argumentsBuilder)
    {
        private readonly ArgumentsBuilder _argumentsBuilder = argumentsBuilder;

        public OutputOptions IdentificationFormat(string option) =>
            Add("--identification-format").Add(option);

        public OutputOptions IdentificationFormatJson() => IdentificationFormat("json");

        public OutputOptions OutputFile(string option) => Add("--output").Add($"\"{option}\"");

        public OutputOptions SeekStartStop(TimeSpan timeStart, TimeSpan timeStop) =>
            timeStart == TimeSpan.Zero || timeStop == TimeSpan.Zero
                ? this
                : Add("--split")
                    .Add($"parts:{(int)timeStart.TotalSeconds}s-{(int)timeStop.TotalSeconds}s");

        public OutputOptions SeekStart(TimeSpan timeSpan) =>
            timeSpan == TimeSpan.Zero
                ? this
                : Add("--split").Add($"parts:{(int)timeSpan.TotalSeconds}s-");

        public OutputOptions SeekStop(TimeSpan timeSpan) =>
            timeSpan == TimeSpan.Zero
                ? this
                : Add("--split").Add($"parts:-{(int)timeSpan.TotalSeconds}s");

        public OutputOptions TestSnippets() =>
            Program.Options.TestSnippets ? SeekStop(Program.SnippetTimeSpan) : this;

        public OutputOptions Add(string option) => Add(option, false);

        public OutputOptions Add(string option, bool escape)
        {
            if (string.IsNullOrWhiteSpace(option))
            {
                return this;
            }
            _ = _argumentsBuilder.Add(option, escape);
            return this;
        }
    }

    public interface IGlobalOptions
    {
        IInputOptions GlobalOptions(Action<GlobalOptions> globalOptions);
    }

    public interface IInputOptions
    {
        IOutputOptions InputOptions(Action<InputOptions> inputOptions);
    }

    public interface IOutputOptions
    {
        IBuilder OutputOptions(Action<OutputOptions> outputOptions);
    }

    public interface IBuilder
    {
        Command Build();
    }

    public class Builder(string targetFilePath)
        : Command(targetFilePath),
            IGlobalOptions,
            IInputOptions,
            IOutputOptions,
            IBuilder
    {
        public static IGlobalOptions Create(string targetFilePath) => new Builder(targetFilePath);

        public static Command Version(string targetFilePath) =>
            new Builder(targetFilePath).WithArguments(args => args.Add("--version").Build());

        public IInputOptions GlobalOptions(Action<GlobalOptions> globalOptions)
        {
            globalOptions(new(_argumentsBuilder));
            return this;
        }

        public IOutputOptions InputOptions(Action<InputOptions> inputOptions)
        {
            inputOptions(new(_argumentsBuilder));
            return this;
        }

        public IBuilder OutputOptions(Action<OutputOptions> outputOptions)
        {
            outputOptions(new(_argumentsBuilder));
            return this;
        }

        public Command Build() => WithArguments(_argumentsBuilder.Build());

        private readonly ArgumentsBuilder _argumentsBuilder = new();
    }
}
