using System;
using CliWrap;
using CliWrap.Builders;

namespace PlexCleaner;

public partial class FfMpeg
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

    public class FfMpegOptions(ArgumentsBuilder argumentsBuilder)
    {
        private readonly ArgumentsBuilder _argumentsBuilder = argumentsBuilder;

        public FfMpegOptions OutputFormat(string option)
        {
            _ = _argumentsBuilder.Add($"-output_format {option}");
            return this;
        }

        public FfMpegOptions OutputFormatJson()
        {
            _ = _argumentsBuilder.Add("-output_format json");
            return this;
        }

        public FfMpegOptions ShowStreams()
        {
            _ = _argumentsBuilder.Add("-show_streams");
            return this;
        }

        public FfMpegOptions ShowPackets()
        {
            _ = _argumentsBuilder.Add("-show_packets");
            return this;
        }

        public FfMpegOptions ShowFrames()
        {
            _ = _argumentsBuilder.Add("-show_frames");
            return this;
        }

        public FfMpegOptions ShowFormat()
        {
            _ = _argumentsBuilder.Add("-show_format");
            return this;
        }

        public FfMpegOptions AnalyzeFrames()
        {
            _ = _argumentsBuilder.Add("-analyze_frames");
            return this;
        }

        public FfMpegOptions SelectStreams(string option)
        {
            _ = _argumentsBuilder.Add($"-select_streams {option}");
            return this;
        }

        public FfMpegOptions ShowEntries(string option)
        {
            _ = _argumentsBuilder.Add($"-show_entries {option}");
            return this;
        }

        public FfMpegOptions ReadIntervals(TimeSpan timeStart, TimeSpan timeEnd)
        {
            _ = _argumentsBuilder.Add(
                $"-read_intervals +{(int)timeStart.TotalSeconds}%{(int)timeEnd.TotalSeconds}"
            );
            return this;
        }

        public FfMpegOptions ReadIntervalsStart(TimeSpan timeSpan)
        {
            _ = _argumentsBuilder.Add($"-read_intervals +{(int)timeSpan.TotalSeconds}");
            return this;
        }

        public FfMpegOptions ReadIntervalsStop(TimeSpan timeSpan)
        {
            _ = _argumentsBuilder.Add($"-read_intervals %{(int)timeSpan.TotalSeconds}");
            return this;
        }

        public FfMpegOptions InputFile(string option)
        {
            _ = _argumentsBuilder.Add($"-i {option}");
            return this;
        }

        public FfMpegOptions Add(string option)
        {
            _ = _argumentsBuilder.Add(option);
            return this;
        }

        public FfMpegOptions Add(string option, bool escape)
        {
            _ = _argumentsBuilder.Add(option, escape);
            return this;
        }
    }

    public interface IGlobalOptions
    {
        IFfMpegOptions GlobalOptions(Action<GlobalOptions> globalOptions);
    }

    public interface IFfMpegOptions
    {
        IFfMpegBuilder FfProbeOptions(Action<FfMpegOptions> ffprobeOptions);
    }

    public interface IFfMpegBuilder
    {
        Command Build();
    }

    public class FfMpegBuilder(string targetFilePath)
        : Command(targetFilePath),
            IGlobalOptions,
            IFfMpegOptions,
            IFfMpegBuilder
    {
        public static IGlobalOptions Create(string targetFilePath) =>
            new FfMpegBuilder(targetFilePath);

        public IFfMpegOptions GlobalOptions(Action<GlobalOptions> globalOptions)
        {
            globalOptions(new(_argumentsBuilder));
            return this;
        }

        public IFfMpegBuilder FfProbeOptions(Action<FfMpegOptions> ffprobeOptions)
        {
            ffprobeOptions(new(_argumentsBuilder));
            return this;
        }

        public Command Build() => WithArguments(_argumentsBuilder.Build());

        private readonly ArgumentsBuilder _argumentsBuilder = new();
    }
}
