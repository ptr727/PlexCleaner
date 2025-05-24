#region

using System;
using CliWrap;
using CliWrap.Builders;

#endregion

namespace PlexCleaner;

public partial class SevenZip
{
    public class GlobalOptions(ArgumentsBuilder argumentsBuilder)
    {
        private readonly ArgumentsBuilder _argumentsBuilder = argumentsBuilder;

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

        public InputOptions InputFile(string option) => Add($"\"{option}\"");

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

        public OutputOptions OutputFolder(string option) => Add($"-o\"{option}\"");

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

        public static Command Version(string targetFilePath) => new Builder(targetFilePath).Build();
    }
}
