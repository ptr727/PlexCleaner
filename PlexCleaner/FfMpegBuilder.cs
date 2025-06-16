using System;
using CliWrap;
using CliWrap.Builders;

namespace PlexCleaner;

// https://github.com/FFmpeg/FFmpeg/blob/master/doc/fftools-common-opts.texi
// https://github.com/FFmpeg/FFmpeg/blob/master/doc/formats.texi
// https://github.com/FFmpeg/FFmpeg/blob/master/doc/ffmpeg.texi

// https://github.com/livingbio/typed-ffmpeg
// https://github.com/rosenbjerg/FFMpegCore
// https://github.com/kkroening/ffmpeg-python
// https://github.com/fluent-ffmpeg/node-fluent-ffmpeg

public partial class FfMpeg
{
    public class GlobalOptions(ArgumentsBuilder argumentsBuilder)
    {
        private readonly ArgumentsBuilder _argumentsBuilder = argumentsBuilder;

        public GlobalOptions Default() =>
            LogLevelError().HideBanner().NoStats().AbortOnEmptyOutput();

        public GlobalOptions LogLevel() => Add("-loglevel");

        public GlobalOptions LogLevel(string option) => LogLevel().Add(option);

        public GlobalOptions LogLevelError() => LogLevel("error");

        public GlobalOptions HideBanner() => Add("-hide_banner");

        public GlobalOptions NoStats() => Add("-nostats");

        public GlobalOptions ExitOnError() => Add("-xerror");

        public GlobalOptions AbortOn() => Add("-abort_on");

        public GlobalOptions AbortOn(string option) => AbortOn().Add(option);

        public GlobalOptions AbortOnEmptyOutput() => AbortOn("empty_output");

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

        // https://trac.ffmpeg.org/ticket/2622
        // Error with some PGS subtitles
        // [matroska,webm @ 000001d77fb61ca0] Could not find codec parameters for stream 2 (Subtitle: hdmv_pgs_subtitle): unspecified size
        // Consider increasing the value for the 'analyzeduration' and 'probesize' options
        // TODO: Issue is reported fixed, to be verified

        // Add -fflags +genpts to generate missing timestamps
        // [mpegts @ 0x5713ff02ab40] first pts and dts value must be set
        // av_interleaved_write_frame(): Invalid data found when processing input
        // [matroska @ 0x604976cd9dc0] Can't write packet with unknown timestamp
        // av_interleaved_write_frame(): Invalid argument
        public InputOptions Default() => Flags("+genpts").AnalyzeDuration("2G").ProbeSize("2G");

        public InputOptions AnalyzeDuration() => Add("-analyzeduration");

        public InputOptions AnalyzeDuration(string option) => AnalyzeDuration().Add(option);

        public InputOptions ProbeSize() => Add("-probesize");

        public InputOptions ProbeSize(string option) => ProbeSize().Add(option);

        public InputOptions SeekStartStop(TimeSpan timeStart, TimeSpan timeStop) =>
            timeStart == TimeSpan.Zero || timeStop == TimeSpan.Zero
                ? this
                : SeekStart(timeStart).SeekStop(timeStop);

        public InputOptions SeekStart() => Add("-ss");

        public InputOptions SeekStart(TimeSpan timeSpan) =>
            timeSpan == TimeSpan.Zero ? this : SeekStart().Add($"{(int)timeSpan.TotalSeconds}");

        public InputOptions SeekStop() => Add("-t");

        public InputOptions SeekStop(TimeSpan timeSpan) =>
            timeSpan == TimeSpan.Zero ? this : SeekStop().Add($"{(int)timeSpan.TotalSeconds}");

        public InputOptions TestSnippets() =>
            Program.Options.TestSnippets ? SeekStop(Program.SnippetTimeSpan) : this;

        public InputOptions QuickScan() =>
            Program.Options.QuickScan ? SeekStop(Program.QuickScanTimeSpan) : this;

        public InputOptions Flags() => Add("-fflags");

        public InputOptions Flags(string option) => Flags().Add(option);

        public InputOptions Input() => Add("-i");

        public InputOptions InputFile(string option) => Input().Add($"\"{option}\"");

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

        // https://trac.ffmpeg.org/ticket/6375
        // Too many packets buffered for output stream 0:1
        // Set max_muxing_queue_size to large value to work around issue
        // TODO: Issue is reported fixed, to be verified
        public OutputOptions Default() => MaxMuxingQueueSize("1024");

        public OutputOptions MaxMuxingQueueSize() => Add("-max_muxing_queue_size");

        public OutputOptions MaxMuxingQueueSize(string option) => MaxMuxingQueueSize().Add(option);

        public OutputOptions NoAudio() => Add("-an");

        public OutputOptions NoVideo() => Add("-vn");

        public OutputOptions NoSubtitles() => Add("-sn");

        public OutputOptions NoData() => Add("-dn");

        public OutputOptions Map() => Add("-map");

        public OutputOptions Map(string option) => Map().Add(option);

        public OutputOptions MapMetadata() => Add("-map_metadata");

        public OutputOptions MapMetadata(string option) => MapMetadata().Add(option);

        public OutputOptions MapAll() => Map("0");

        public OutputOptions Codec() => Add("-c");

        public OutputOptions Codec(string option) => Codec().Add(option);

        public OutputOptions CodecCopy() => Codec("copy");

        public OutputOptions MapAllCodecCopy() => MapAll().CodecCopy();

        public OutputOptions CodecVideo() => Add("-c:v");

        public OutputOptions CodecVideo(string option) => CodecVideo().Add(option);

        public OutputOptions CodecAudio() => Add("-c:a");

        public OutputOptions CodecAudio(string option) => CodecAudio().Add(option);

        public OutputOptions CodecSubtitle() => Add("-c:s");

        public OutputOptions CodecSubtitle(string option) => CodecSubtitle().Add(option);

        public OutputOptions Format() => Add("-f");

        public OutputOptions Format(string option) => Format().Add(option);

        public OutputOptions FormatMatroska() => Format("matroska");

        public OutputOptions OutputFile(string option) => Add($"\"{option}\"");

        public OutputOptions VideoFilter() => Add("-vf");

        public OutputOptions VideoFilter(string option) => VideoFilter().Add(option);

        public OutputOptions BitstreamFilterVideo() => Add("-bsf:v");

        public OutputOptions BitstreamFilterVideo(string option) =>
            BitstreamFilterVideo().Add(option);

        public OutputOptions SeekStartStop(TimeSpan timeStart, TimeSpan timeStop) =>
            timeStart == TimeSpan.Zero || timeStop == TimeSpan.Zero
                ? this
                : SeekStart(timeStart).SeekStop(timeStop);

        public OutputOptions SeekStart() => Add("-ss");

        public OutputOptions SeekStart(TimeSpan timeSpan) =>
            timeSpan == TimeSpan.Zero ? this : SeekStart().Add($"{(int)timeSpan.TotalSeconds}");

        public OutputOptions SeekStop() => Add("-t");

        public OutputOptions SeekStop(TimeSpan timeSpan) =>
            timeSpan == TimeSpan.Zero ? this : SeekStop().Add($"{(int)timeSpan.TotalSeconds}");

        public OutputOptions NullOutput() => Format().Add("null").Add("-");

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
        private readonly ArgumentsBuilder _argumentsBuilder = new();

        public Command Build() => WithArguments(_argumentsBuilder.Build());

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

        public static IGlobalOptions Create(string targetFilePath) => new Builder(targetFilePath);

        public static Command Version(string targetFilePath) =>
            new Builder(targetFilePath).WithArguments(args => args.Add("-version").Build());
    }
}
