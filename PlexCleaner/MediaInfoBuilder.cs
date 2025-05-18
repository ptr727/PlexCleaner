using System;
using CliWrap;
using CliWrap.Builders;

namespace PlexCleaner;

public partial class MediaInfo
{
    public class GlobalOptions(ArgumentsBuilder argumentsBuilder)
    {
        private readonly ArgumentsBuilder _argumentsBuilder = argumentsBuilder;

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

    public class MediaInfoOptions(ArgumentsBuilder argumentsBuilder)
    {
        private readonly ArgumentsBuilder _argumentsBuilder = argumentsBuilder;

        public MediaInfoOptions OutputFormat(string option) => Add($"--Output={option}");

        public MediaInfoOptions OutputFormatXml() => OutputFormat("XML");

        public MediaInfoOptions OutputFormatJson() => OutputFormat("JSON");

        public MediaInfoOptions InputFile(string option) => Add($"\"{option}\"");

        public MediaInfoOptions Add(string option) => Add(option, false);

        public MediaInfoOptions Add(string option, bool escape)
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

        public static Command Version(string targetFilePath) =>
            new MediaInfoBuilder(targetFilePath).WithArguments(args =>
                args.Add("--version").Build()
            );

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
