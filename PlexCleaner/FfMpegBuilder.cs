using System;
using CliWrap;
using CliWrap.Builders;

namespace PlexCleaner;

// https://github.com/FFmpeg/FFmpeg/blob/master/doc/fftools-common-opts.texi
// https://github.com/FFmpeg/FFmpeg/blob/master/doc/formats.texi
// https://github.com/FFmpeg/FFmpeg/blob/master/doc/ffmpeg.texi

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

        public GlobalOptions LogLevel(string option) => Add("-loglevel").Add(option);

        public GlobalOptions LogLevelError() => LogLevel("error");

        public GlobalOptions LogLevelQuiet() => LogLevel("quiet");

        public GlobalOptions HideBanner() => Add("-hide_banner");

        public GlobalOptions NoStats() => Add("-nostats");

        public GlobalOptions ExitOnError() => Add("-xerror");

        public GlobalOptions AbortOn(string option) => Add("-abort_on").Add(option);

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

        public InputOptions AnalyzeDuration(string option) => Add("-analyzeduration").Add(option);

        public InputOptions ProbeSize(string option) => Add("-probesize").Add(option);

        public InputOptions SeekStartStop(TimeSpan timeStart, TimeSpan timeEnd) =>
            timeStart == TimeSpan.Zero || timeEnd == TimeSpan.Zero
                ? this
                : Add("-ss")
                    .Add($"{(int)timeStart.TotalSeconds}")
                    .Add("-t")
                    .Add($"{(int)timeEnd.TotalSeconds}");

        public InputOptions SeekStart(TimeSpan timeSpan) =>
            timeSpan == TimeSpan.Zero ? this : Add("-ss").Add($"{(int)timeSpan.TotalSeconds}");

        public InputOptions SeekStop(TimeSpan timeSpan) =>
            timeSpan == TimeSpan.Zero ? this : Add("-t").Add($"{(int)timeSpan.TotalSeconds}");

        public InputOptions Flags(string option) => Add("-fflags").Add(option);

        public InputOptions InputFile(string option) => Add("-i").Add($"\"{option}\"");

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

        public OutputOptions MaxMuxingQueueSize(string option) =>
            Add("-max_muxing_queue_size").Add(option);

        public OutputOptions NoAudio() => Add("-an");

        public OutputOptions NoVideo() => Add("-an");

        public OutputOptions NoSubtitles() => Add("-sn");

        public OutputOptions NoData() => Add("-dn");

        public OutputOptions Map(string option) => Add("-map").Add(option);

        public OutputOptions MapMetadata(string option) => Add("-map_metadata").Add(option);

        public OutputOptions MapAll() => Map("0");

        public OutputOptions Codec(string option) => Add("-c").Add(option);

        public OutputOptions CodecCopy() => Codec("copy");

        public OutputOptions MapAllCodecCopy() => MapAll().CodecCopy();

        public OutputOptions CodecVideo(string option) => Add("-c:v").Add(option);

        public OutputOptions CodecAudio(string option) => Add("-c:a").Add(option);

        public OutputOptions CodecSubtitle(string option) => Add("-c:s").Add(option);

        public OutputOptions Format(string option) => Add("-f").Add(option);

        public OutputOptions FormatMatroska() => Format("matroska");

        public OutputOptions OutputFile(string option) => Add($"\"{option}\"");

        public OutputOptions VideoFilter(string option) => Add("-vf").Add(option);

        public OutputOptions BitstreamFilterVideo(string option) => Add("-bsf:v").Add(option);

        public OutputOptions SeekStartStop(TimeSpan timeStart, TimeSpan timeEnd) =>
            timeStart == TimeSpan.Zero || timeEnd == TimeSpan.Zero
                ? this
                : SeekStart(timeStart).SeekStop(timeEnd);

        public OutputOptions SeekStart(TimeSpan timeSpan) =>
            timeSpan == TimeSpan.Zero ? this : Add("-ss").Add($"{(int)timeSpan.TotalSeconds}");

        public OutputOptions SeekStop(TimeSpan timeSpan) =>
            timeSpan == TimeSpan.Zero ? this : Add("-t").Add($"{(int)timeSpan.TotalSeconds}");

        public OutputOptions NullOutput() => Add("-f").Add("null").Add("-");

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
        IFfMpegBuilder OutputOptions(Action<OutputOptions> outputOptions);
    }

    public interface IFfMpegBuilder
    {
        Command Build();
    }

    public class FfMpegBuilder(string targetFilePath)
        : Command(targetFilePath),
            IGlobalOptions,
            IInputOptions,
            IOutputOptions,
            IFfMpegBuilder
    {
        public static IGlobalOptions Create(string targetFilePath) =>
            new FfMpegBuilder(targetFilePath);

        public static Command Version(string targetFilePath) =>
            new FfMpegBuilder(targetFilePath).WithArguments(args => args.Add("-version").Build());

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

        public IFfMpegBuilder OutputOptions(Action<OutputOptions> outputOptions)
        {
            outputOptions(new(_argumentsBuilder));
            return this;
        }

        public Command Build() => WithArguments(_argumentsBuilder.Build());

        private readonly ArgumentsBuilder _argumentsBuilder = new();
    }
}
