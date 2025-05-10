using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog;

namespace PlexCleaner;

public class TrackProps
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

    public MediaTool.ToolType Parser { get; } = MediaTool.ToolType.None;
    public string Format { get; set; } = string.Empty;
    public string Codec { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string LanguageIetf { get; set; } = string.Empty;
    public string LanguageAny => !string.IsNullOrEmpty(LanguageIetf) ? LanguageIetf : Language;
    public int Id { get; set; }
    public int Number { get; set; }
    public StateType State { get; set; } = StateType.None;
    public string Title { get; set; } = string.Empty;
    public bool HasTags { get; set; }
    public bool HasErrors { get; set; }
    public FlagsType Flags { get; set; } = FlagsType.None;

    protected string FileName { get; } = string.Empty;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor")]
    public TrackProps(MediaTool.ToolType parser, string fileName)
    {
        Parser = parser;
        FileName = fileName;
    }

    public virtual bool Create(MkvToolJsonSchema.Track track)
    {
        Debug.Assert(Parser == MediaTool.ToolType.MkvMerge);

        // Required
        Format = track.Codec;
        Codec = track.Properties.CodecId;
        if (string.IsNullOrEmpty(Format) || string.IsNullOrEmpty(Codec))
        {
            HasErrors = true;
            State = StateType.Unsupported;
            Log.Error(
                "MkvToolJsonSchema : Track is missing required codec information : Format: {Format}, Codec: {Codec}, State: {State} : {FileName}",
                Format,
                Codec,
                State,
                FileName
            );
            return false;
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

        // Set language
        if (!SetLanguage(track))
        {
            return false;
        }

        // Use id and number correctly in MkvMerge and MkvPropEdit
        // https://gitlab.com/mbunkus/mkvtoolnix/-/wikis/About-track-UIDs,-track-numbers-and-track-IDs#track-numbers

        // Id: 0-based track number internally assigned
        Id = track.Id;

        // Number: 1-based track number from Matroska header
        Number = track.Properties.Number;

        // Check title for tags
        HasTags = TitleIsTag();

        // Set flags from title
        SetFlagsFromTitle();

        return true;
    }

    private bool SetLanguage(MkvToolJsonSchema.Track track)
    {
        // ISO 639-2B tag
        Language = track.Properties.Language;

        // RFC-5646 / BCP 47 tag
        LanguageIetf = track.Properties.LanguageIetf;

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
                    "MkvToolJsonSchema : Failed to lookup ISO639 tag from IETF tag : ISO639: {Language}, IETF: {LanguageIetf}, State: {State} : {FileName}",
                    Language,
                    LanguageIetf,
                    State,
                    FileName
                );
            }
            else if (!Language.Equals(isoLookup, StringComparison.OrdinalIgnoreCase))
            {
                // Set track error and recommend ReMux
                HasErrors = true;
                State = StateType.ReMux;

                // Lookup ISO from IETF is good, but ISO lookup does not match set ISO language
                Log.Error(
                    "MkvToolJsonSchema : Failed to match ISO639 tag with ISO639 from IETF tag : ISO639: {Language}, IETF: {LanguageIetf}, ISO639 from IETF: {Lookup}, State: {State} : {FileName}",
                    Language,
                    LanguageIetf,
                    isoLookup,
                    State,
                    FileName
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
                    "MkvToolJsonSchema : Failed to lookup IETF tag from ISO639 tag : ISO639: {Language}, State: {State} : {FileName}",
                    Language,
                    State,
                    FileName
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
                    "MkvToolJsonSchema : Setting IETF tag from ISO639 tag : ISO639: {Language}, IETF: {LanguageIetf}, State: {State} : {FileName}",
                    Language,
                    LanguageIetf,
                    State,
                    FileName
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
                    "MkvToolJsonSchema : Failed to lookup ISO639 tag from IETF tag : IETF: {LanguageIetf}, State: {State} : {FileName}",
                    LanguageIetf,
                    State,
                    FileName
                );
            }
            else
            {
                // Set ISO from lookup
                Language = isoLookup;
                Log.Warning(
                    "MkvToolJsonSchema : Setting ISO639 tag from IETF tag : ISO639: {Language}, IETF: {LanguageIetf}, State: {State} : {FileName}",
                    Language,
                    LanguageIetf,
                    State,
                    FileName
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
                "MkvToolJsonSchema : TagLanguage does not match Language : TagLanguage: {TagLanguage}, Language: {Language}, State: {State} : {FileName}",
                track.Properties.TagLanguage,
                track.Properties.Language,
                State,
                FileName
            );
        }

        return true;
    }

    public virtual bool Create(FfMpegToolJsonSchema.Track track)
    {
        Debug.Assert(Parser == MediaTool.ToolType.FfProbe);

        // Required
        Format = track.CodecName;
        Codec = track.CodecLongName;
        if (string.IsNullOrEmpty(Format) || string.IsNullOrEmpty(Codec))
        {
            HasErrors = true;
            State = StateType.Unsupported;
            Log.Error(
                "FfMpegToolJsonSchema : Track is missing required codec information : Format: {Format}, Codec: {Codec}, State: {State} : {FileName}",
                Format,
                Codec,
                State,
                FileName
            );
            return false;
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
                "FfMpegToolJsonSchema : Invalid Language : Language: {Language}, State: {State} : {FileName}",
                Language,
                State,
                FileName
            );
        }

        // Leave the Language as is, no need to verify

        // Use stream index for id and number
        // TODO: How to get Matroska TrackNumber from ffProbe?
        Id = track.Index;
        Number = track.Index;

        // Check title for tags
        HasTags = TitleIsTag();

        // TODO: Set flags from title
        // Repair uses MkvPropEdit, only set for MkvMergeInfo

        return true;
    }

    public virtual bool Create(MediaInfoToolXmlSchema.Track track)
    {
        Debug.Assert(Parser == MediaTool.ToolType.MediaInfo);

        // Required
        Format = track.Format;
        Codec = track.CodecId;
        if (string.IsNullOrEmpty(Format) || string.IsNullOrEmpty(Codec))
        {
            HasErrors = true;
            State = StateType.Unsupported;
            Log.Error(
                "MediaInfoToolXmlSchema : Track is missing required codec information : Format: {Format}, Codec: {Codec}, State: {State} : {FileName}",
                Format,
                Codec,
                State,
                FileName
            );
            return false;
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

        // Use Id for Number
        // https://github.com/MediaArea/MediaInfo/issues/201
        // 1
        // 3-CC1
        // 1 / 8876149d-48f0-4148-8225-dc0b53a50b90
        Match match = MediaInfoTool.TrackRegex().Match(track.Id);
        Debug.Assert(match.Success);
        // Use Number before dash as Matroska TrackNumber
        Number = int.Parse(match.Groups["id"].Value, CultureInfo.InvariantCulture);

        // Use StreamOrder for Id
        // 0
        // 0-1
        match = MediaInfoTool.TrackRegex().Match(track.StreamOrder);
        Debug.Assert(match.Success);
        Id = int.Parse(match.Groups["id"].Value, CultureInfo.InvariantCulture);

        // Check title for tags
        HasTags = TitleIsTag();

        // TODO: Set flags from title, flags are incomplete

        return true;
    }

    public virtual void WriteLine() =>
        Log.Information(
            "Parser: {Parser}, Type: {Type}, Format: {Format}, Codec: {Codec}, Language: {Language}, LanguageIetf: {LanguageIetf}, "
                + "Id: {Id}, Number: {Number}, Title: {Title}, Flags: {Flags}, State: {State}, HasErrors: {HasErrors}, HasTags: {HasTags} : {FileName}",
            Parser,
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
            HasTags,
            FileName
        );

    public virtual void WriteLine(string prefix) =>
        Log.Information(
            "{Prefix} : Parser: {Parser}, Type: {Type}, Format: {Format}, Codec: {Codec}, Language: {Language}, LanguageIetf: {LanguageIetf}, "
                + "Id: {Id}, Number: {Number}, Title: {Title}, Flags: {Flags}, State: {State}, HasErrors: {HasErrors}, HasTags: {HasTags} : {FileName}",
            prefix,
            Parser,
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
            HasTags,
            FileName
        );

    public bool TitleIsTag()
    {
        // No title no tag
        if (string.IsNullOrEmpty(Title))
        {
            return false;
        }

        // Has title but is not flag
        // TODO: Need so more sanitization to ensure title is clean
        return !TitleContainsFlag();
    }

    public bool TitleContainsFlag() =>
        // Title contains a flag
        s_titleFlags.Any(item => Title.Contains(item.Name, StringComparison.OrdinalIgnoreCase));

    public void SetFlagsFromTitle() =>
        // Add flags based on flag presence in the title
        s_titleFlags.ForEach(item =>
        {
            // Set flag if present in the title
            if (
                Title.Contains(item.Name, StringComparison.OrdinalIgnoreCase)
                && !Flags.HasFlag(item.Flag)
            )
            {
                // Set track error state and recommend setting the track flags
                HasErrors = true;
                State = StateType.SetFlags;
                Flags |= item.Flag;
                Log.Information(
                    "{Parser} : Setting track Flag from Title : Title: {Title}, Flag: {Flag}, State: {State} : {FileName}",
                    Parser,
                    Title,
                    item.Flag,
                    State,
                    FileName
                );
            }
        });

    public static IEnumerable<FlagsType> GetFlags(FlagsType flagsType) =>
        Enum.GetValues<FlagsType>()
            .Where(enumValue => flagsType.HasFlag(enumValue) && enumValue != FlagsType.None);

    public static IEnumerable<FlagsType> GetFlags() =>
        Enum.GetValues<FlagsType>().Where(enumValue => enumValue != FlagsType.None);

    // Track title to flag mapping
    private static readonly List<(string Name, FlagsType Flag)> s_titleFlags =
    [
        new("SDH", FlagsType.HearingImpaired),
        new("CC", FlagsType.HearingImpaired),
        new("Commentary", FlagsType.Commentary),
        new("Forced", FlagsType.Forced),
    ];
}
