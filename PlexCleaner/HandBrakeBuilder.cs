using System;
using CliWrap;
using CliWrap.Builders;

namespace PlexCleaner;

public partial class HandBrake
{
    public class GlobalOptions(ArgumentsBuilder argumentsBuilder)
    {
        private readonly ArgumentsBuilder _argumentsBuilder = argumentsBuilder;

        // TODO: Consolidate
        public GlobalOptions Default() => this;

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

        public InputOptions Input() => Add("--input");

        public InputOptions InputFile(string option) => Input().Add($"\"{option}\"");

        public InputOptions SeekStartStop(TimeSpan timeStart, TimeSpan timeStop) =>
            timeStart == TimeSpan.Zero || timeStop == TimeSpan.Zero
                ? this
                : SeekStart(timeStart).SeekStop(timeStop);

        public InputOptions SeekStart(TimeSpan timeSpan) =>
            timeSpan == TimeSpan.Zero
                ? this
                : Add("--start-at").Add($"seconds:{(int)timeSpan.TotalSeconds}");

        public InputOptions SeekStop(TimeSpan timeSpan) =>
            timeSpan == TimeSpan.Zero
                ? this
                : Add("--stop-at").Add($"seconds:{(int)timeSpan.TotalSeconds}");

        public InputOptions TestSnippets() =>
            Program.Options.TestSnippets ? SeekStop(Program.SnippetTimeSpan) : this;

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

        public OutputOptions Output() => Add("--output");

        public OutputOptions OutputFile(string option) => Output().Add($"\"{option}\"");

        public OutputOptions Format() => Add("--format");

        public OutputOptions FormatMatroska() => Format().Add("av_mkv");

        public OutputOptions VideoEncoder() => Add("--encoder");

        public OutputOptions VideoEncoder(string option) => VideoEncoder().Add(option);

        public OutputOptions CombDetect() => Add("--comb-detect");

        public OutputOptions Decomb() => Add("--decomb");

        public OutputOptions AllAudio() => Add("--all-audio");

        public OutputOptions AudioEncoder() => Add("--aencoder");

        public OutputOptions AudioEncoder(string option) => AudioEncoder().Add(option);

        public OutputOptions AllSubtitles() => Add("--all-subtitles");

        public OutputOptions Subtitle() => Add("--subtitle");

        public OutputOptions Subtitle(string option) => Subtitle().Add(option);

        public OutputOptions NoSubtitles() => Subtitle("none");

        public OutputOptions Add(bool enable, Func<OutputOptions, OutputOptions> func) =>
            enable ? func(this) : this;

        public OutputOptions Add(
            bool condition,
            Func<OutputOptions, OutputOptions> func1,
            Func<OutputOptions, OutputOptions> func2
        ) => condition ? func1(this) : func2(this);

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
