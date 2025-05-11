using System;
using CliWrap;
using CliWrap.Builders;

namespace PlexCleaner;

public partial class MediaInfo
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

    public class MediaInfoOptions(ArgumentsBuilder argumentsBuilder)
    {
        private readonly ArgumentsBuilder _argumentsBuilder = argumentsBuilder;

        public MediaInfoOptions OutputFormat(string option)
        {
            _ = _argumentsBuilder.Add($"-output_format {option}");
            return this;
        }

        public MediaInfoOptions OutputFormatJson()
        {
            _ = _argumentsBuilder.Add("-output_format json");
            return this;
        }

        public MediaInfoOptions ShowStreams()
        {
            _ = _argumentsBuilder.Add("-show_streams");
            return this;
        }

        public MediaInfoOptions ShowPackets()
        {
            _ = _argumentsBuilder.Add("-show_packets");
            return this;
        }

        public MediaInfoOptions ShowFrames()
        {
            _ = _argumentsBuilder.Add("-show_frames");
            return this;
        }

        public MediaInfoOptions ShowFormat()
        {
            _ = _argumentsBuilder.Add("-show_format");
            return this;
        }

        public MediaInfoOptions AnalyzeFrames()
        {
            _ = _argumentsBuilder.Add("-analyze_frames");
            return this;
        }

        public MediaInfoOptions SelectStreams(string option)
        {
            _ = _argumentsBuilder.Add($"-select_streams {option}");
            return this;
        }

        public MediaInfoOptions ShowEntries(string option)
        {
            _ = _argumentsBuilder.Add($"-show_entries {option}");
            return this;
        }

        public MediaInfoOptions ReadIntervals(TimeSpan timeStart, TimeSpan timeEnd)
        {
            _ = _argumentsBuilder.Add(
                $"-read_intervals +{(int)timeStart.TotalSeconds}%{(int)timeEnd.TotalSeconds}"
            );
            return this;
        }

        public MediaInfoOptions ReadIntervalsStart(TimeSpan timeSpan)
        {
            _ = _argumentsBuilder.Add($"-read_intervals +{(int)timeSpan.TotalSeconds}");
            return this;
        }

        public MediaInfoOptions ReadIntervalsStop(TimeSpan timeSpan)
        {
            _ = _argumentsBuilder.Add($"-read_intervals %{(int)timeSpan.TotalSeconds}");
            return this;
        }

        public MediaInfoOptions InputFile(string option)
        {
            _ = _argumentsBuilder.Add($"-i {option}");
            return this;
        }

        public MediaInfoOptions Add(string option)
        {
            _ = _argumentsBuilder.Add(option);
            return this;
        }

        public MediaInfoOptions Add(string option, bool escape)
        {
            _ = _argumentsBuilder.Add(option, escape);
            return this;
        }
    }

    public interface IGlobalOptions
    {
        IMediaInfoOptions GlobalOptions(Action<GlobalOptions> globalOptions);
    }

    public interface IMediaInfoOptions
    {
        IMediaInfoBuilder MediaInfoOptions(Action<MediaInfoOptions> ffprobeOptions);
    }

    public interface IMediaInfoBuilder
    {
        Command Build();
    }

    public class MediaInfoBuilder(string targetFilePath)
        : Command(targetFilePath),
            IGlobalOptions,
            IMediaInfoOptions,
            IMediaInfoBuilder
    {
        public static IGlobalOptions Create(string targetFilePath) =>
            new MediaInfoBuilder(targetFilePath);

        public IMediaInfoOptions GlobalOptions(Action<GlobalOptions> globalOptions)
        {
            globalOptions(new(_argumentsBuilder));
            return this;
        }

        public IMediaInfoBuilder MediaInfoOptions(Action<MediaInfoOptions> ffprobeOptions)
        {
            ffprobeOptions(new(_argumentsBuilder));
            return this;
        }

        public Command Build() => WithArguments(_argumentsBuilder.Build());

        private readonly ArgumentsBuilder _argumentsBuilder = new();
    }
}
