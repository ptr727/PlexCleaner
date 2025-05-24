using System;
using CliWrap;
using CliWrap.Builders;

namespace PlexCleaner;

public partial class MediaInfo
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

    // TODO: Rename input or output
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
        IBuilder MediaInfoOptions(Action<MediaInfoOptions> ffprobeOptions);
    }

    public interface IBuilder
    {
        Command Build();
    }

    public class Builder(string targetFilePath)
        : Command(targetFilePath),
            IGlobalOptions,
            IMediaInfoOptions,
            IBuilder
    {
        private readonly ArgumentsBuilder _argumentsBuilder = new();

        public Command Build() => WithArguments(_argumentsBuilder.Build());

        public IMediaInfoOptions GlobalOptions(Action<GlobalOptions> globalOptions)
        {
            globalOptions(new(_argumentsBuilder));
            return this;
        }

        public IBuilder MediaInfoOptions(Action<MediaInfoOptions> ffprobeOptions)
        {
            ffprobeOptions(new(_argumentsBuilder));
            return this;
        }

        public static IGlobalOptions Create(string targetFilePath) => new Builder(targetFilePath);

        public static Command Version(string targetFilePath) =>
            new Builder(targetFilePath).WithArguments(args => args.Add("--version").Build());
    }
}
