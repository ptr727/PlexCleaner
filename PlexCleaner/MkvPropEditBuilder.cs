using System;
using System.Linq;
using CliWrap;
using CliWrap.Builders;

namespace PlexCleaner;

public partial class MkvPropEdit
{
    public static string GetTrackFlag(TrackProps.FlagsType flagType) =>
        // mkvpropedit --list-property-names
        // Enums must be single flag values, not combined flags
        flagType switch
        {
            TrackProps.FlagsType.Default => "flag-default",
            TrackProps.FlagsType.Forced => "flag-forced",
            TrackProps.FlagsType.HearingImpaired => "flag-hearing-impaired",
            TrackProps.FlagsType.VisualImpaired => "flag-visual-impaired",
            TrackProps.FlagsType.Descriptions => "flag-text-descriptions",
            TrackProps.FlagsType.Original => "flag-original",
            TrackProps.FlagsType.Commentary => "flag-commentary",
            // flag-enabled
            TrackProps.FlagsType.None => throw new NotImplementedException(),
            _ => throw new NotImplementedException(),
        };

    public class GlobalOptions(ArgumentsBuilder argumentsBuilder)
    {
        public GlobalOptions Default() => NormalizeLanguageIetfExtended();

        public GlobalOptions NormalizeLanguageIetf(string option) =>
            Add("--normalize-language-ietf").Add(option);

        public GlobalOptions NormalizeLanguageIetfExtended() => NormalizeLanguageIetf("extlang");

        public GlobalOptions Add(string option) => Add(option, false);

        public GlobalOptions Add(string option, bool escape)
        {
            if (string.IsNullOrWhiteSpace(option))
            {
                return this;
            }
            _ = argumentsBuilder.Add(option, escape);
            return this;
        }
    }

    public class InputOptions(ArgumentsBuilder argumentsBuilder)
    {
        public InputOptions Default() => DeleteTrackStatisticsTags();

        public InputOptions InputFile(string option) => Add($"\"{option}\"");

        public InputOptions DeleteTrackStatisticsTags() => Add("--delete-track-statistics-tags");

        public InputOptions Edit() => Add("--edit");

        public InputOptions Track(long option) => Add($"track:@{option}");

        public InputOptions EditTrack(long option) => Edit().Track(option);

        public InputOptions Set() => Add("--set");

        public InputOptions Tags() => Add("--tags");

        public InputOptions Delete() => Add("--delete");

        public InputOptions DeleteAttachment() => Add("--delete-attachment");

        public InputOptions DeleteAttachment(int option) => DeleteAttachment().Add($"{option + 1}");

        // Set the language property not the language-ietf property
        // https://codeberg.org/mbunkus/mkvtoolnix/wiki/Languages-in-Matroska-and-MKVToolNix#mkvpropedit
        public InputOptions Language(string option) => Add($"language={option}");

        public InputOptions SetLanguage(string option) => Set().Language(option);

        public InputOptions SetFlags(TrackProps.FlagsType option)
        {
            TrackProps
                .GetFlags(option)
                .ToList()
                .ForEach(item => Set().Add($"{GetTrackFlag(item)}=1"));
            return this;
        }

        public InputOptions Add(Func<InputOptions, InputOptions> func) => func(this);

        public InputOptions Add(string option) => Add(option, false);

        public InputOptions Add(string option, bool escape)
        {
            if (string.IsNullOrWhiteSpace(option))
            {
                return this;
            }
            _ = argumentsBuilder.Add(option, escape);
            return this;
        }
    }

    public interface IGlobalOptions
    {
        IInputOptions GlobalOptions(Action<GlobalOptions> globalOptions);
    }

    public interface IInputOptions
    {
        IBuilder InputOptions(Action<InputOptions> inputOptions);
    }

    public interface IBuilder
    {
        Command Build();
    }

    public class Builder(string targetFilePath)
        : Command(targetFilePath),
            IGlobalOptions,
            IInputOptions,
            IBuilder
    {
        private readonly ArgumentsBuilder _argumentsBuilder = new();

        public Command Build() => WithArguments(_argumentsBuilder.Build());

        public IInputOptions GlobalOptions(Action<GlobalOptions> globalOptions)
        {
            globalOptions(new(_argumentsBuilder));
            return this;
        }

        public IBuilder InputOptions(Action<InputOptions> inputOptions)
        {
            inputOptions(new(_argumentsBuilder));
            return this;
        }

        public static IGlobalOptions Create(string targetFilePath) => new Builder(targetFilePath);

        public static Command Version(string targetFilePath) =>
            new Builder(targetFilePath).WithArguments(args => args.Add("--version").Build());
    }
}
