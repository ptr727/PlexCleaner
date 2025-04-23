using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog;

namespace PlexCleaner;

public partial class TrackInfo
{
    public enum StateType
    {
        None,
        Keep,
        Remove,
        ReMux,
        ReEncode,
        DeInterlace,
        SetFlags,
        SetLanguage,
        Unsupported,
    }

    // https://www.ietf.org/archive/id/draft-ietf-cellar-matroska-15.html#name-track-flags
    [Flags]
    public enum FlagsType
    {
        None = 0,
        Default = 1,
        Forced = 1 << 1,
        HearingImpaired = 1 << 2,
        VisualImpaired = 1 << 3,
        Descriptions = 1 << 4,
        Original = 1 << 5,
        Commentary = 1 << 6,
    }

    public TrackInfo() { }

    public TrackInfo(MkvToolJsonSchema.Track track)
    {
        const string parser = "MkvToolJsonSchema";

        // Required
        Format = track.Codec;
        Codec = track.Properties.CodecId;
        if (string.IsNullOrEmpty(Format) || string.IsNullOrEmpty(Codec))
        {
            HasErrors = true;
            State = StateType.Unsupported;
            Log.Error(
                "{Parser} : Track is missing required codec information : State: {State}",
                parser,
                State
            );
            return;
        }

        // Flags
        if (track.Properties.DefaultTrack)
        {
            Flags |= FlagsType.Default;
        }
        if (track.Properties.Original)
        {
            Flags |= FlagsType.Original;
        }
        if (track.Properties.Commentary)
        {
            Flags |= FlagsType.Commentary;
        }
        if (track.Properties.VisualImpaired)
        {
            Flags |= FlagsType.VisualImpaired;
        }
        if (track.Properties.HearingImpaired)
        {
            Flags |= FlagsType.HearingImpaired;
        }
        if (track.Properties.TextDescriptions)
        {
            Flags |= FlagsType.Descriptions;
        }
        if (track.Properties.Forced)
        {
            Flags |= FlagsType.Forced;
        }

        // Title
        Title = track.Properties.TrackName;

        // ISO 639-2B tag
        Language = track.Properties.Language;

        // RFC-5646 / BCP 47 tag
        LanguageIetf = track.Properties.LanguageIetf;

        // If the GetIso639Tag() or GetIetfTag() tag lookup logic is incomplete or buggy no amount of remuxing will help
        // https://gitlab.com/mbunkus/mkvtoolnix/-/wikis/Languages-in-Matroska-and-MKVToolNix
        // https://r12a.github.io/app-subtags/

        // For verification logic save state before modification
        bool languageSet = !string.IsNullOrEmpty(Language);
        bool languageIetfSet = !string.IsNullOrEmpty(LanguageIetf);

        // Language and LanguageIetf are both set, verify they match
        if (languageSet && languageIetfSet)
        {
            // Get the ISO tag from the IETF tag
            string isoLookup = PlexCleaner.Language.Singleton.GetIso639Tag(LanguageIetf, true);

            if (string.IsNullOrEmpty(isoLookup))
            {
                // Set track error and recommend ReMux
                HasErrors = true;
                State = StateType.ReMux;

                // Failed to lookup ISO tag from IETF tag
                Log.Error(
                    "{Parser} : Failed to lookup ISO639 tag from IETF tag : ISO639: {Language}, IETF: {LanguageIetf}, State: {State}",
                    parser,
                    Language,
                    LanguageIetf,
                    State
                );
            }
            else if (!Language.Equals(isoLookup, StringComparison.OrdinalIgnoreCase))
            {
                // Set track error and recommend ReMux
                HasErrors = true;
                State = StateType.ReMux;

                // Lookup ISO from IETF is good, but ISO lookup does not match set ISO language
                Log.Error(
                    "{Parser} : Failed to match ISO639 tag with ISO639 from IETF tag : ISO639: {Language}, IETF: {LanguageIetf}, ISO639 from IETF: {Lookup}, State: {State}",
                    parser,
                    Language,
                    LanguageIetf,
                    isoLookup,
                    State
                );
            }
            // Lookup good and matches
        }

        // Language is set but IETF language is not set
        if (languageSet && !languageIetfSet)
        {
            // Get the IETF tag from the ISO tag
            string ietfLookup = PlexCleaner.Language.Singleton.GetIetfTag(Language, true);

            if (string.IsNullOrEmpty(ietfLookup))
            {
                // Set track error and recommend remux
                HasErrors = true;
                State = StateType.ReMux;

                // Failed to lookup IETF tag from ISO tag
                Log.Error(
                    "{Parser} : Failed to lookup IETF tag from ISO639 tag : ISO639: {Language}, State: {State}",
                    parser,
                    Language,
                    State
                );
            }
            else
            {
                // Set track error and recommend SetLanguage
                // ReMux will conditionally check for SetIetfLanguageTags
                HasErrors = true;
                State = StateType.SetLanguage;

                // Set IETF tag from lookup tag
                LanguageIetf = ietfLookup;
                Log.Information(
                    "{Parser} : Setting IETF tag from ISO639 tag : ISO639: {Language}, IETF: {LanguageIetf}, State: {State}",
                    parser,
                    Language,
                    LanguageIetf,
                    State
                );
            }
        }

        // Language is not set but IETF language is set
        if (!languageSet && languageIetfSet)
        {
            // ISO language should always be set it IETF language is set

            // Set track error and recommend remux
            HasErrors = true;
            State = StateType.ReMux;

            // Get the ISO tag from the IETF tag
            string isoLookup = PlexCleaner.Language.Singleton.GetIso639Tag(LanguageIetf, true);

            if (string.IsNullOrEmpty(isoLookup))
            {
                // Failed to lookup ISO from IETF
                Log.Error(
                    "{Parser} : Failed to lookup ISO639 tag from IETF tag : IETF: {LanguageIetf}, State: {State}",
                    parser,
                    LanguageIetf,
                    State
                );
            }
            else
            {
                // Set ISO from lookup
                Language = isoLookup;
                Log.Warning(
                    "{Parser} : Setting ISO639 tag from IETF tag : ISO639: {Language}, IETF: {LanguageIetf}, State: {State}",
                    parser,
                    Language,
                    LanguageIetf,
                    State
                );
            }
        }

        // If the "language" and "tag_language" fields are set FfProbe uses the tag language instead of the track language
        // https://github.com/MediaArea/MediaAreaXml/issues/34
        if (
            !string.IsNullOrEmpty(track.Properties.TagLanguage)
            && !string.IsNullOrEmpty(track.Properties.Language)
            && !track.Properties.Language.Equals(
                track.Properties.TagLanguage,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            // Set track error and recommend remux
            HasErrors = true;
            State = StateType.ReMux;
            Log.Warning(
                "{Parser} : TagLanguage does not match Language : TagLanguage: {TagLanguage}, Language: {Language}, State: {State}",
                parser,
                track.Properties.TagLanguage,
                track.Properties.Language,
                State
            );
        }

        // Take care to use id and number correctly in MkvMerge and MkvPropEdit
        // https://gitlab.com/mbunkus/mkvtoolnix/-/wikis/About-track-UIDs,-track-numbers-and-track-IDs#track-numbers
        // Id: 0-based track number internally assigned
        Id = track.Id;
        // Number: 1-based track number from Matroska header
        Number = track.Properties.Number;

        // TODO: Anything other than title for tags?
        HasTags = NotTrackTitleFlag();

        // Set flags from title
        SetFlagsFromTitle(parser);
    }

    public TrackInfo(FfMpegToolJsonSchema.Track track)
    {
        const string parser = "FfMpegToolJsonSchema";

        // Required
        Format = track.CodecName;
        Codec = track.CodecLongName;
        if (string.IsNullOrEmpty(Format) || string.IsNullOrEmpty(Codec))
        {
            HasErrors = true;
            State = StateType.Unsupported;
            Log.Error(
                "{Parser} : Track is missing required codec information : State: {State}",
                parser,
                State
            );
            return;
        }

        // Flags
        if (track.Disposition.Default != 0)
        {
            Flags |= FlagsType.Default;
        }
        if (track.Disposition.Forced != 0)
        {
            Flags |= FlagsType.Forced;
        }
        if (track.Disposition.Original != 0)
        {
            Flags |= FlagsType.Original;
        }
        if (track.Disposition.Comment != 0)
        {
            Flags |= FlagsType.Commentary;
        }
        if (track.Disposition.HearingImpaired != 0)
        {
            Flags |= FlagsType.HearingImpaired;
        }
        if (track.Disposition.VisualImpaired != 0)
        {
            Flags |= FlagsType.VisualImpaired;
        }
        if (track.Disposition.Descriptions != 0)
        {
            Flags |= FlagsType.Descriptions;
        }

        // Title
        Title =
            track
                .Tags.FirstOrDefault(item =>
                    item.Key.Equals("title", StringComparison.OrdinalIgnoreCase)
                )
                .Value ?? "";

        // Language
        Language =
            track
                .Tags.FirstOrDefault(item =>
                    item.Key.Equals("language", StringComparison.OrdinalIgnoreCase)
                )
                .Value ?? "";

        // TODO: FfProbe uses the tag language value instead of the track language
        // Some files show MediaInfo and MkvMerge say language is "eng", FfProbe says language is "und"
        // https://github.com/MediaArea/MediaAreaXml/issues/34

        // Some sample files use "???" or "null" for the language
        if (
            Language.Equals("???", StringComparison.OrdinalIgnoreCase)
            || Language.Equals("null", StringComparison.OrdinalIgnoreCase)
        )
        {
            // Set track error and recommend remux
            HasErrors = true;
            State = StateType.ReMux;
            Log.Warning(
                "{Parser} : Invalid Language : {Language} : {State}",
                parser,
                Language,
                State
            );
        }

        // Leave the Language as is, no need to verify

        // Use stream index for id and number
        // TODO: How to get Matroska TrackNumber from ffProbe?
        Id = track.Index;
        Number = track.Index;

        // TODO: Anything other than title for tags?
        HasTags = NotTrackTitleFlag();

        // TODO: Set flags from title
        // Repair uses MkvPropEdit, only set for MkvMergeInfo
        // SetFlagsFromTitle("FfMpegToolJsonSchema");
    }

    public TrackInfo(MediaInfoToolXmlSchema.Track track)
    {
        const string parser = "MediaInfoToolXmlSchema";

        // Required
        Format = track.Format;
        Codec = track.CodecId;
        if (string.IsNullOrEmpty(Format) || string.IsNullOrEmpty(Codec))
        {
            HasErrors = true;
            State = StateType.Unsupported;
            Log.Error(
                "{Parser} : Track is missing required codec information : State: {State}",
                parser,
                State
            );
            return;
        }

        // Title
        Title = track.Title;

        // Language
        Language = track.Language;

        // TODO: Missing flags
        // Original
        // Commentary
        // VisualImpaired
        // HearingImpaired
        // Descriptions

        if (track.Default)
        {
            Flags |= FlagsType.Default;
        }
        if (track.Forced)
        {
            Flags |= FlagsType.Forced;
        }

        // MediaInfo uses ab or abc or ab-cd language tags
        // https://github.com/MediaArea/MediaAreaXml/issues/33

        // FfProbe and MkvMerge use chi not zho
        // https://gitlab.com/mbunkus/mkvtoolnix/-/wikis/Chinese-not-selectable-as-language
        // https://gitlab.com/mbunkus/mkvtoolnix/-/issues/1149

        // Leave the Language as is, no need to verify

        // https://github.com/MediaArea/MediaInfo/issues/201
        // "For Matroska, the first part (before the dash) of the ID field is mapped to TrackNumber Matroska field,
        // and the first part (before the dash) of the UniqueID field is mapped to TrackUID Matroska field"
        // ID can be in a variety of formats:
        // 1
        // 3-CC1
        // 1 / 8876149d-48f0-4148-8225-dc0b53a50b90
        Match match = TrackRegex().Match(track.Id);
        Debug.Assert(match.Success);
        // Use number before dash as Matroska TrackNumber
        Number = int.Parse(match.Groups["id"].Value, CultureInfo.InvariantCulture);

        // Use StreamOrder for Id
        Id = track.StreamOrder;

        // TODO: Anything other than title for tags?
        HasTags = NotTrackTitleFlag();

        // TODO: Set flags from title, flags are incomplete
        // SetFlagsFromTitle("MediaInfoToolXmlSchema");
    }

    public string Format { get; set; } = "";
    public string Codec { get; set; } = "";
    public string Language { get; set; } = "";
    public string LanguageIetf { get; set; } = "";
    public string LanguageAny => !string.IsNullOrEmpty(LanguageIetf) ? LanguageIetf : Language;
    public int Id { get; set; }
    public int Number { get; set; }
    public StateType State { get; set; } = StateType.None;
    public string Title { get; set; } = "";
    public bool HasTags { get; set; }
    public bool HasErrors { get; set; }
    public FlagsType Flags { get; set; } = FlagsType.None;

    public virtual void WriteLine(string prefix) =>
        Log.Information(
            "{Prefix} : Type: {Type}, Format: {Format}, Codec: {Codec}, Language: {Language}, LanguageIetf: {LanguageIetf}, Id: {Id}, Number: {Number}, Title: {Title}, Flags: {Flags}, State: {State}, HasErrors: {HasErrors}, HasTags: {HasTags}",
            prefix,
            GetType().Name,
            Format,
            Codec,
            Language,
            LanguageIetf,
            Id,
            Number,
            Title,
            Flags,
            State,
            HasErrors,
            HasTags
        );

    public bool NotTrackTitleFlag()
    {
        // Not logic, i.e. title is not a flag
        if (string.IsNullOrEmpty(Title))
        {
            // Empty is not a flag
            return false;
        }

        // Not a flag is not a flag
        return !s_titleFlags.Any(tuple =>
            Title.Contains(tuple.Item1, StringComparison.OrdinalIgnoreCase)
        );
    }

    public void SetFlagsFromTitle(string parser)
    {
        // Add flags based on titles
        foreach ((string, FlagsType) tuple in s_titleFlags)
        {
            // Only process if matching flag is not already set
            if (
                Title.Contains(tuple.Item1, StringComparison.OrdinalIgnoreCase)
                && !Flags.HasFlag(tuple.Item2)
            )
            {
                // Set track error state and recommend setting the track flags
                HasErrors = true;
                State = StateType.SetFlags;
                Flags |= tuple.Item2;
                Log.Information(
                    "{Parser} : Setting track Flag from Title : {Title} -> {Flag} : {State}",
                    parser,
                    Title,
                    tuple.Item2,
                    State
                );
            }
        }
    }

    public static IEnumerable<FlagsType> GetFlags(FlagsType flagsType) =>
        Enum.GetValues<FlagsType>()
            .Where(enumValue => flagsType.HasFlag(enumValue) && enumValue != FlagsType.None);

    public static IEnumerable<FlagsType> GetFlags() =>
        Enum.GetValues<FlagsType>().Where(enumValue => enumValue != FlagsType.None);

    [GeneratedRegex(@"(?<id>\d)")]
    public static partial Regex TrackRegex();

    // Track title to flag mapping
    private static readonly ValueTuple<string, FlagsType>[] s_titleFlags =
    [
        new("SDH", FlagsType.HearingImpaired),
        new("CC", FlagsType.HearingImpaired),
        new("Commentary", FlagsType.Commentary),
        new("Forced", FlagsType.Forced),
    ];
}
