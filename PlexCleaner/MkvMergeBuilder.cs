using System;
using CliWrap;
using CliWrap.Builders;

namespace PlexCleaner;

public partial class MkvMerge
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

    public class MkvMergeOptions(ArgumentsBuilder argumentsBuilder)
    {
        private readonly ArgumentsBuilder _argumentsBuilder = argumentsBuilder;

        public MkvMergeOptions OutputFormat(string option)
        {
            _ = _argumentsBuilder.Add($"-output_format {option}");
            return this;
        }

        public MkvMergeOptions OutputFormatJson()
        {
            _ = _argumentsBuilder.Add("-output_format json");
            return this;
        }

        public MkvMergeOptions ShowStreams()
        {
            _ = _argumentsBuilder.Add("-show_streams");
            return this;
        }

        public MkvMergeOptions ShowPackets()
        {
            _ = _argumentsBuilder.Add("-show_packets");
            return this;
        }

        public MkvMergeOptions ShowFrames()
        {
            _ = _argumentsBuilder.Add("-show_frames");
            return this;
        }

        public MkvMergeOptions ShowFormat()
        {
            _ = _argumentsBuilder.Add("-show_format");
            return this;
        }

        public MkvMergeOptions AnalyzeFrames()
        {
            _ = _argumentsBuilder.Add("-analyze_frames");
            return this;
        }

        public MkvMergeOptions SelectStreams(string option)
        {
            _ = _argumentsBuilder.Add($"-select_streams {option}");
            return this;
        }

        public MkvMergeOptions ShowEntries(string option)
        {
            _ = _argumentsBuilder.Add($"-show_entries {option}");
            return this;
        }

        public MkvMergeOptions ReadIntervals(TimeSpan timeStart, TimeSpan timeEnd)
        {
            _ = _argumentsBuilder.Add(
                $"-read_intervals +{(int)timeStart.TotalSeconds}%{(int)timeEnd.TotalSeconds}"
            );
            return this;
        }

        public MkvMergeOptions ReadIntervalsStart(TimeSpan timeSpan)
        {
            _ = _argumentsBuilder.Add($"-read_intervals +{(int)timeSpan.TotalSeconds}");
            return this;
        }

        public MkvMergeOptions ReadIntervalsStop(TimeSpan timeSpan)
        {
            _ = _argumentsBuilder.Add($"-read_intervals %{(int)timeSpan.TotalSeconds}");
            return this;
        }

        public MkvMergeOptions InputFile(string option)
        {
            _ = _argumentsBuilder.Add($"-i {option}");
            return this;
        }

        public MkvMergeOptions Add(string option)
        {
            _ = _argumentsBuilder.Add(option);
            return this;
        }

        public MkvMergeOptions Add(string option, bool escape)
        {
            _ = _argumentsBuilder.Add(option, escape);
            return this;
        }
    }

    public interface IGlobalOptions
    {
        IMkvMergeOptions GlobalOptions(Action<GlobalOptions> globalOptions);
    }

    public interface IMkvMergeOptions
    {
        IMkvMergeBuilder MkvMergeOptions(Action<MkvMergeOptions> ffprobeOptions);
    }

    public interface IMkvMergeBuilder
    {
        Command Build();
    }

    public class MkvMergeBuilder(string targetFilePath)
        : Command(targetFilePath),
            IGlobalOptions,
            IMkvMergeOptions,
            IMkvMergeBuilder
    {
        public static IGlobalOptions Create(string targetFilePath) =>
            new MkvMergeBuilder(targetFilePath);

        public IMkvMergeOptions GlobalOptions(Action<GlobalOptions> globalOptions)
        {
            globalOptions(new(_argumentsBuilder));
            return this;
        }

        public IMkvMergeBuilder MkvMergeOptions(Action<MkvMergeOptions> ffprobeOptions)
        {
            ffprobeOptions(new(_argumentsBuilder));
            return this;
        }

        public Command Build() => WithArguments(_argumentsBuilder.Build());

        private readonly ArgumentsBuilder _argumentsBuilder = new();
    }
}
