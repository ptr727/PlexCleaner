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

        public GlobalOptions Default() => AnalyzeDuration("2G").ProbeSize("2G");

        public GlobalOptions LogLevel() => Add("-loglevel");

        public GlobalOptions LogLevel(string option) => LogLevel().Add(option);

        public GlobalOptions LogLevelError() => LogLevel("error");

        public GlobalOptions LogLevelQuiet() => LogLevel("quiet");

        public GlobalOptions HideBanner() => Add("-hide_banner");

        public GlobalOptions AnalyzeDuration() => Add("-analyzeduration");

        public GlobalOptions AnalyzeDuration(string option) => AnalyzeDuration().Add(option);

        public GlobalOptions ProbeSize() => Add("-probesize");

        public GlobalOptions ProbeSize(string option) => ProbeSize().Add(option);

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

    // TODO: Rename to input or output options
    public class FfProbeOptions(ArgumentsBuilder argumentsBuilder)
    {
        private readonly ArgumentsBuilder _argumentsBuilder = argumentsBuilder;

        public FfProbeOptions OutputFormat() => Add("-output_format");

        public FfProbeOptions OutputFormat(string option) => OutputFormat().Add(option);

        public FfProbeOptions OutputFormatJson() => OutputFormat("json");

        public FfProbeOptions ShowStreams() => Add("-show_streams");

        public FfProbeOptions ShowPackets() => Add("-show_packets");

        public FfProbeOptions ShowFrames() => Add("-show_frames");

        public FfProbeOptions ShowFormat() => Add("-show_format");

        public FfProbeOptions AnalyzeFrames() => Add("-analyze_frames");

        public FfProbeOptions SelectStreams() => Add("-select_streams");

        public FfProbeOptions SelectStreams(string option) => SelectStreams().Add(option);

        public FfProbeOptions Format() => Add("-f");

        public FfProbeOptions Format(string option) => Format().Add(option);

        public FfProbeOptions Input() => Add("-i");

        public FfProbeOptions Input(string option) => Input().Add(option);

        public FfProbeOptions ShowEntries() => Add("-show_entries");

        public FfProbeOptions ShowEntries(string option) => ShowEntries().Add(option);

        public FfProbeOptions SeekStartStop(TimeSpan timeStart, TimeSpan timeStop) =>
            timeStart == TimeSpan.Zero || timeStop == TimeSpan.Zero
                ? this
                : Add("-read_intervals")
                    .Add($"+{(int)timeStart.TotalSeconds}%{(int)timeStop.TotalSeconds}");

        public FfProbeOptions SeekStart(TimeSpan timeSpan) =>
            timeSpan == TimeSpan.Zero
                ? this
                : Add("-read_intervals").Add($"+{(int)timeSpan.TotalSeconds}");

        public FfProbeOptions SeekStop(TimeSpan timeSpan) =>
            timeSpan == TimeSpan.Zero
                ? this
                : Add("-read_intervals").Add($"%{(int)timeSpan.TotalSeconds}");

        public FfProbeOptions QuickScan() =>
            Program.Options.QuickScan ? SeekStop(Program.QuickScanTimeSpan) : this;

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
        IBuilder FfProbeOptions(Action<FfProbeOptions> ffprobeOptions);
    }

    public interface IBuilder
    {
        Command Build();
    }

    public class Builder(string targetFilePath)
        : Command(targetFilePath),
            IGlobalOptions,
            IFfProbeOptions,
            IBuilder
    {
        public static IGlobalOptions Create(string targetFilePath) => new Builder(targetFilePath);

        public static Command Version(string targetFilePath) =>
            new Builder(targetFilePath).WithArguments(args => args.Add("-version").Build());

        public IFfProbeOptions GlobalOptions(Action<GlobalOptions> globalOptions)
        {
            globalOptions(new(_argumentsBuilder));
            return this;
        }

        public IBuilder FfProbeOptions(Action<FfProbeOptions> ffprobeOptions)
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
