using System;
using CliWrap;
using CliWrap.Builders;

namespace PlexCleaner;

// https://github.com/FFmpeg/FFmpeg/blob/master/doc/fftools-common-opts.texi
// https://github.com/FFmpeg/FFmpeg/blob/master/doc/ffprobe.texi

public partial class FfProbe
{
    public class GlobalOptions(ArgumentsBuilder argumentsBuilder)
    {
        private readonly ArgumentsBuilder _argumentsBuilder = argumentsBuilder;

        public GlobalOptions LogLevel(string option) => Add("-loglevel").Add(option);

        public GlobalOptions LogLevelError() => Add("-loglevel").Add("error");

        public GlobalOptions LogLevelQuiet() => Add("-loglevel").Add("quiet");

        public GlobalOptions HideBanner() => Add("-hide_banner");

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

    public class FfProbeOptions(ArgumentsBuilder argumentsBuilder)
    {
        private readonly ArgumentsBuilder _argumentsBuilder = argumentsBuilder;

        public FfProbeOptions OutputFormat(string option) => Add("-output_format").Add(option);

        public FfProbeOptions OutputFormatJson() => Add("-output_format").Add("json");

        public FfProbeOptions ShowStreams() => Add("-show_streams");

        public FfProbeOptions ShowPackets() => Add("-show_packets");

        public FfProbeOptions ShowFrames() => Add("-show_frames");

        public FfProbeOptions ShowFormat() => Add("-show_format");

        public FfProbeOptions AnalyzeFrames() => Add("-analyze_frames");

        public FfProbeOptions SelectStreams(string option) => Add("-select_streams").Add(option);

        public FfProbeOptions Format(string option) => Add("-f").Add(option);

        public FfProbeOptions Input(string option) => Add("-i").Add(option);

        public FfProbeOptions ShowEntries(string option) => Add("-show_entries").Add(option);

        public FfProbeOptions SeekStartStop(TimeSpan timeStart, TimeSpan timeEnd) =>
            timeStart == TimeSpan.Zero || timeEnd == TimeSpan.Zero
                ? this
                : Add("-read_intervals")
                    .Add($"+{(int)timeStart.TotalSeconds}%{(int)timeEnd.TotalSeconds}");

        public FfProbeOptions SeekStart(TimeSpan timeSpan) =>
            timeSpan == TimeSpan.Zero
                ? this
                : Add("-read_intervals").Add($"+{(int)timeSpan.TotalSeconds}");

        public FfProbeOptions SeekStop(TimeSpan timeSpan) =>
            timeSpan == TimeSpan.Zero
                ? this
                : Add("-read_intervals").Add($"%{(int)timeSpan.TotalSeconds}");

        public FfProbeOptions InputFile(string option) => Add($"\"{option}\"");

        public FfProbeOptions Add(string option) => Add(option, false);

        public FfProbeOptions Add(string option, bool escape)
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
        IFfProbeOptions GlobalOptions(Action<GlobalOptions> globalOptions);
    }

    public interface IFfProbeOptions
    {
        IFfProbeBuilder FfProbeOptions(Action<FfProbeOptions> ffprobeOptions);
    }

    public interface IFfProbeBuilder
    {
        Command Build();
    }

    public class FfProbeBuilder(string targetFilePath)
        : Command(targetFilePath),
            IGlobalOptions,
            IFfProbeOptions,
            IFfProbeBuilder
    {
        public static IGlobalOptions Create(string targetFilePath) =>
            new FfProbeBuilder(targetFilePath);

        public static Command Version(string targetFilePath) =>
            new FfProbeBuilder(targetFilePath).WithArguments(args => args.Add("-version").Build());

        public IFfProbeOptions GlobalOptions(Action<GlobalOptions> globalOptions)
        {
            globalOptions(new(_argumentsBuilder));
            return this;
        }

        public IFfProbeBuilder FfProbeOptions(Action<FfProbeOptions> ffprobeOptions)
        {
            ffprobeOptions(new(_argumentsBuilder));
            return this;
        }

        public Command Build() => WithArguments(_argumentsBuilder.Build());

        private readonly ArgumentsBuilder _argumentsBuilder = new();
    }

    public static string EscapeMovieFileName(string fileName) =>
        // Escape the file name, specifically : \ ' characters
        // \ -> /
        // : -> \\:
        // ' -> \\\'
        // , -> \\\,
        // https://superuser.com/questions/1893137/how-to-quote-a-file-name-containing-single-quotes-in-ffmpeg-ffprobe-movie-filena
        fileName
            .Replace(@"\", @"/")
            .Replace(@":", @"\\:")
            .Replace(@"'", @"\\\'")
            .Replace(@",", @"\\\,");
}
