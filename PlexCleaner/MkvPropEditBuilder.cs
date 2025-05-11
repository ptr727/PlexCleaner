using System;
using CliWrap;
using CliWrap.Builders;

namespace PlexCleaner;

public partial class MkvPropEdit
{
    public class GlobalOptions(ArgumentsBuilder argumentsBuilder)
    {
        private readonly ArgumentsBuilder _argumentsBuilder = argumentsBuilder;

        public GlobalOptions LogLevel(string option)
        {
            _ = _argumentsBuilder.Add($"-loglevel {option}");
            return this;
        }

        public GlobalOptions LogLevelError()
        {
            _ = _argumentsBuilder.Add("-loglevel error");
            return this;
        }

        public GlobalOptions LogLevelQuiet()
        {
            _ = _argumentsBuilder.Add("-loglevel quiet");
            return this;
        }

        public GlobalOptions HideBanner()
        {
            _ = _argumentsBuilder.Add("-hide_banner");
            return this;
        }

        public GlobalOptions Add(string option)
        {
            _ = _argumentsBuilder.Add(option);
            return this;
        }

        public GlobalOptions Add(string option, bool escape)
        {
            _ = _argumentsBuilder.Add(option, escape);
            return this;
        }
    }

    public class MkvPropEditOptions(ArgumentsBuilder argumentsBuilder)
    {
        private readonly ArgumentsBuilder _argumentsBuilder = argumentsBuilder;

        public MkvPropEditOptions OutputFormat(string option)
        {
            _ = _argumentsBuilder.Add($"-output_format {option}");
            return this;
        }

        public MkvPropEditOptions OutputFormatJson()
        {
            _ = _argumentsBuilder.Add("-output_format json");
            return this;
        }

        public MkvPropEditOptions ShowStreams()
        {
            _ = _argumentsBuilder.Add("-show_streams");
            return this;
        }

        public MkvPropEditOptions ShowPackets()
        {
            _ = _argumentsBuilder.Add("-show_packets");
            return this;
        }

        public MkvPropEditOptions ShowFrames()
        {
            _ = _argumentsBuilder.Add("-show_frames");
            return this;
        }

        public MkvPropEditOptions ShowFormat()
        {
            _ = _argumentsBuilder.Add("-show_format");
            return this;
        }

        public MkvPropEditOptions AnalyzeFrames()
        {
            _ = _argumentsBuilder.Add("-analyze_frames");
            return this;
        }

        public MkvPropEditOptions SelectStreams(string option)
        {
            _ = _argumentsBuilder.Add($"-select_streams {option}");
            return this;
        }

        public MkvPropEditOptions ShowEntries(string option)
        {
            _ = _argumentsBuilder.Add($"-show_entries {option}");
            return this;
        }

        public MkvPropEditOptions ReadIntervals(TimeSpan timeStart, TimeSpan timeEnd)
        {
            _ = _argumentsBuilder.Add(
                $"-read_intervals +{(int)timeStart.TotalSeconds}%{(int)timeEnd.TotalSeconds}"
            );
            return this;
        }

        public MkvPropEditOptions ReadIntervalsStart(TimeSpan timeSpan)
        {
            _ = _argumentsBuilder.Add($"-read_intervals +{(int)timeSpan.TotalSeconds}");
            return this;
        }

        public MkvPropEditOptions ReadIntervalsStop(TimeSpan timeSpan)
        {
            _ = _argumentsBuilder.Add($"-read_intervals %{(int)timeSpan.TotalSeconds}");
            return this;
        }

        public MkvPropEditOptions InputFile(string option)
        {
            _ = _argumentsBuilder.Add($"-i {option}");
            return this;
        }

        public MkvPropEditOptions Add(string option)
        {
            _ = _argumentsBuilder.Add(option);
            return this;
        }

        public MkvPropEditOptions Add(string option, bool escape)
        {
            _ = _argumentsBuilder.Add(option, escape);
            return this;
        }
    }

    public interface IGlobalOptions
    {
        IMkvPropEditOptions GlobalOptions(Action<GlobalOptions> globalOptions);
    }

    public interface IMkvPropEditOptions
    {
        IMkvPropEditBuilder MkvPropEditOptions(Action<MkvPropEditOptions> ffprobeOptions);
    }

    public interface IMkvPropEditBuilder
    {
        Command Build();
    }

    public class MkvPropEditBuilder(string targetFilePath)
        : Command(targetFilePath),
            IGlobalOptions,
            IMkvPropEditOptions,
            IMkvPropEditBuilder
    {
        public static IGlobalOptions Create(string targetFilePath) =>
            new MkvPropEditBuilder(targetFilePath);

        public IMkvPropEditOptions GlobalOptions(Action<GlobalOptions> globalOptions)
        {
            globalOptions(new(_argumentsBuilder));
            return this;
        }

        public IMkvPropEditBuilder MkvPropEditOptions(Action<MkvPropEditOptions> ffprobeOptions)
        {
            ffprobeOptions(new(_argumentsBuilder));
            return this;
        }

        public Command Build() => WithArguments(_argumentsBuilder.Build());

        private readonly ArgumentsBuilder _argumentsBuilder = new();
    }
}
