using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Serilog;

namespace PlexCleaner;

public class TrackProps(TrackProps.TrackType trackType, MediaProps mediaProps)
{
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

    public enum TrackType
    {
        None,
        Audio,
        Subtitle,
        Video,
    }

    // Track title to flag mapping
    private static readonly List<(string Name, FlagsType Flag)> s_titleFlags =
    [
        new("SDH", FlagsType.HearingImpaired),
        new("CC", FlagsType.HearingImpaired),
        new("Commentary", FlagsType.Commentary),
        new("Forced", FlagsType.Forced),
    ];

    public TrackType Type => trackType;
    public MediaProps Parent => mediaProps;

    public string Format { get; set; } = string.Empty;
    public string Codec { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string LanguageIetf { get; set; } = string.Empty;
    public string LanguageAny => !string.IsNullOrEmpty(LanguageIetf) ? LanguageIetf : Language;
    public ulong Uid { get; set; }
    public long Id { get; set; }
    public long Number { get; set; }
    public StateType State { get; set; } = StateType.None;
    public string Title { get; set; } = string.Empty;
    public bool HasTags { get; set; }
    public bool HasErrors { get; set; }
    public FlagsType Flags { get; set; } = FlagsType.None;

    // Required
    // Format = track.Codec;
    // Codec = track.Properties.CodecId;
    public virtual bool Create(MkvToolJsonSchema.Track track)
    {
        Debug.Assert(Parent.Parser == MediaTool.ToolType.MkvMerge);

        // Fixup non-MKV container formats
        if (
            !Parent.IsContainerMkv()
            && (string.IsNullOrEmpty(track.Codec) || string.IsNullOrEmpty(track.Properties.CodecId))
        )
        {
            if (string.IsNullOrEmpty(track.Codec))
            {
                track.Codec = "unknown";
            }
            if (string.IsNullOrEmpty(track.Properties.CodecId))
            {
                track.Properties.CodecId = "unknown";
            }
            Log.Warning(
                "{Parser} : {Type} : Overriding unknown format or codec : Format: {Format}, Codec: {Codec}, Container: {Container} : {FileName}",
                Parent.Parser,
                Type,
                track.Codec,
                track.Properties.CodecId,
                Parent.Container,
                Parent.FileName
            );
        }

        // Required
        Format = track.Codec;
        Codec = track.Properties.CodecId;
        if (string.IsNullOrEmpty(Format) || string.IsNullOrEmpty(Codec))
        {
            HasErrors = true;
            State = StateType.Unsupported;
            Log.Error(
                "{Parser} : {Type} : Track is missing required format and codec : Format: {Format}, Codec: {Codec}, Container: {Container} : {FileName}",
                Parent.Parser,
                Type,
                Format,
                Codec,
                Parent.Container,
                Parent.FileName
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

        // Use uid, id, and number correctly in MkvMerge and MkvPropEdit
        // https://codeberg.org/mbunkus/mkvtoolnix/wiki/About-track-UIDs%2C-track-numbers-and-track-IDs

        // Id: MkvMerge internally assigned id
        Id = track.Id;

        // Uid: Matroska unique track id
        // TODO: Consider switching to using uid vs. number as it is unique and deterministic in MkvMerge and MkvPropEdit
        Uid = track.Properties.Uid;

        // Number: Matroska track number from block header
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
            string isoLookup = PlexCleaner.Language.Lookup.GetIsoFromIetf(LanguageIetf);

            if (string.IsNullOrEmpty(isoLookup))
            {
                // Set track error and recommend ReMux
                HasErrors = true;
                State = StateType.ReMux;

                // Failed to lookup ISO tag from IETF tag
                Log.Error(
                    "{Parser} : {Type} : Failed to lookup ISO639 tag from IETF tag : ISO639: {Language}, IETF: {LanguageIetf}, State: {State} : {FileName}",
                    Parent.Parser,
                    Type,
                    Language,
                    LanguageIetf,
                    State,
                    Parent.FileName
                );
            }
            else if (!Language.Equals(isoLookup, StringComparison.OrdinalIgnoreCase))
            {
                // Set track error and recommend ReMux
                HasErrors = true;
                State = StateType.ReMux;

                // Lookup ISO from IETF is good, but ISO lookup does not match set ISO language
                Log.Error(
                    "{Parser} : {Type} : Failed to match ISO639 tag with ISO639 from IETF tag : ISO639: {Language}, IETF: {LanguageIetf}, ISO639 from IETF: {Lookup}, State: {State} : {FileName}",
                    Parent.Parser,
                    Type,
                    Language,
                    LanguageIetf,
                    isoLookup,
                    State,
                    Parent.FileName
                );
            }
            // Lookup good and matches
        }

        // Language is set but IETF language is not set
        if (languageSet && !languageIetfSet)
        {
            // Get the IETF tag from the ISO tag
            string ietfLookup = PlexCleaner.Language.Lookup.GetIetfFromIso(Language);

            if (string.IsNullOrEmpty(ietfLookup))
            {
                // Set track error and recommend remux
                HasErrors = true;
                State = StateType.ReMux;

                // Failed to lookup IETF tag from ISO tag
                Log.Error(
                    "{Parser} : {Type} : Failed to lookup IETF tag from ISO639 tag : ISO639: {Language}, State: {State} : {FileName}",
                    Parent.Parser,
                    Type,
                    Language,
                    State,
                    Parent.FileName
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
                Log.Warning(
                    "{Parser} : {Type} : Setting IETF tag from ISO639 tag : ISO639: {Language}, IETF: {LanguageIetf}, State: {State} : {FileName}",
                    Parent.Parser,
                    Type,
                    Language,
                    LanguageIetf,
                    State,
                    Parent.FileName
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
            string isoLookup = PlexCleaner.Language.Lookup.GetIsoFromIetf(LanguageIetf);

            if (string.IsNullOrEmpty(isoLookup))
            {
                // Failed to lookup ISO from IETF
                Log.Error(
                    "{Parser} : {Type} : Failed to lookup ISO639 tag from IETF tag : IETF: {LanguageIetf}, State: {State} : {FileName}",
                    Parent.Parser,
                    Type,
                    LanguageIetf,
                    State,
                    Parent.FileName
                );
            }
            else
            {
                // Set ISO from lookup
                Language = isoLookup;
                Log.Warning(
                    "{Parser} : {Type} : Setting ISO639 tag from IETF tag : ISO639: {Language}, IETF: {LanguageIetf}, State: {State} : {FileName}",
                    Parent.Parser,
                    Type,
                    Language,
                    LanguageIetf,
                    State,
                    Parent.FileName
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
                "{Parser} : {Type} : TagLanguage does not match Language : TagLanguage: {TagLanguage}, Language: {Language}, State: {State} : {FileName}",
                Parent.Parser,
                Type,
                track.Properties.TagLanguage,
                track.Properties.Language,
                State,
                Parent.FileName
            );
        }

        return true;
    }

    // Required
    // Format = track.CodecName;
    // Codec = track.CodecLongName;
    public virtual bool Create(FfMpegToolJsonSchema.Track track)
    {
        Debug.Assert(Parent.Parser == MediaTool.ToolType.FfProbe);

        // Fixup non-MKV container formats
        if (
            !Parent.IsContainerMkv()
            && (string.IsNullOrEmpty(track.CodecName) || string.IsNullOrEmpty(track.CodecLongName))
        )
        {
            if (string.IsNullOrEmpty(track.CodecName))
            {
                track.CodecName = string.IsNullOrEmpty(track.CodecTagString)
                    ? track.CodecLongName
                    : track.CodecTagString;
            }
            if (string.IsNullOrEmpty(track.CodecLongName))
            {
                track.CodecLongName = string.IsNullOrEmpty(track.CodecTagString)
                    ? track.CodecName
                    : track.CodecTagString;
            }
            Log.Warning(
                "{Parser} : {Type} : Overriding unknown format or codec : Format: {Format}, Codec: {Codec}, Container: {Container} : {FileName}",
                Parent.Parser,
                Type,
                track.CodecName,
                track.CodecLongName,
                Parent.Container,
                Parent.FileName
            );
        }

        // FFprobe does not identify some codecs
        // e.g. Subtitle S_TEXT/WEBVTT
        // "codec_type": "subtitle",
        // "codec_tag_string": "[0][0][0][0]"
        // "codec_tag": "0x0000"
        // E.g. Audio A_QUICKTIME
        // "codec_type": "audio"
        // "codec_tag_string": "enca",
        // "codec_tag": "0x61636e65",
        if (
            string.IsNullOrEmpty(track.CodecName)
            && string.IsNullOrEmpty(track.CodecLongName)
            && !string.IsNullOrEmpty(track.CodecTagString)
        )
        {
            track.CodecName = track.CodecTagString.Contains(
                "[0]",
                StringComparison.OrdinalIgnoreCase
            )
                ? "unknown"
                : track.CodecTagString;
            track.CodecLongName = track.CodecTagString.Contains(
                "[0]",
                StringComparison.OrdinalIgnoreCase
            )
                ? "unknown"
                : track.CodecTagString;
            Log.Warning(
                "{Parser} : {Type} : Overriding unknown format or codec : Format: {Format}, Codec: {Codec}, Container: {Container} : {FileName}",
                Parent.Parser,
                Type,
                track.CodecName,
                track.CodecLongName,
                Parent.Container,
                Parent.FileName
            );
        }

        // Required
        Format = track.CodecName;
        Codec = track.CodecLongName;
        if (string.IsNullOrEmpty(Format) || string.IsNullOrEmpty(Codec))
        {
            HasErrors = true;
            State = StateType.Unsupported;
            Log.Error(
                "{Parser} : {Type} : Track is missing required format and codec : Format: {Format}, Codec: {Codec}, Container: {Container} : {FileName}",
                Parent.Parser,
                Type,
                Format,
                Codec,
                Parent.Container,
                Parent.FileName
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
                .Value
            ?? "";

        // Language
        Language =
            track
                .Tags.FirstOrDefault(item =>
                    item.Key.Equals("language", StringComparison.OrdinalIgnoreCase)
                )
                .Value
            ?? "";

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
                "{Parser} : {Type} : Invalid language : Language: {Language}, State: {State} : {FileName}",
                Parent.Parser,
                Type,
                Language,
                State,
                Parent.FileName
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

    // Required
    // Format = track.Format;
    // Codec = track.CodecId;
    public virtual bool Create(MediaInfoToolXmlSchema.Track track)
    {
        Debug.Assert(Parent.Parser == MediaTool.ToolType.MediaInfo);

        // Handle sub-tracks
        if (!HandleSubTracks(track))
        {
            return false;
        }

        // Fixup non-MKV container formats
        if (
            !Parent.IsContainerMkv()
            && (string.IsNullOrEmpty(track.Format) || string.IsNullOrEmpty(track.CodecId))
        )
        {
            if (string.IsNullOrEmpty(track.Format))
            {
                track.Format = string.IsNullOrEmpty(track.MuxingMode)
                    ? track.CodecId
                    : track.MuxingMode;
            }
            if (string.IsNullOrEmpty(track.CodecId))
            {
                track.CodecId = string.IsNullOrEmpty(track.MuxingMode)
                    ? track.Format
                    : track.MuxingMode;
            }
            Log.Warning(
                "{Parser} : {Type} : Overriding unknown format or codec : Format: {Format}, Codec: {Codec}, Container: {Container} : {FileName}",
                Parent.Parser,
                Type,
                track.Format,
                track.CodecId,
                Parent.Container,
                Parent.FileName
            );
        }

        // Required
        Format = track.Format;
        Codec = track.CodecId;
        if (string.IsNullOrEmpty(Format) || string.IsNullOrEmpty(Codec))
        {
            HasErrors = true;
            State = StateType.Unsupported;
            Log.Error(
                "{Parser} : {Type} : Track is missing required format and codec : Format: {Format}, Codec: {Codec}, Container: {Container} : {FileName}",
                Parent.Parser,
                Type,
                Format,
                Codec,
                Parent.Container,
                Parent.FileName
            );
            return false;
        }

        // Title
        Title = track.Title;

        // Language
        // MediaInfo uses ab or abc or ab-cd language tags
        // https://github.com/MediaArea/MediaAreaXml/issues/33
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

        // Sub-tracks should already be filtered out
        // Uid, Id and StreamOrder should be all numeric if set

        // Use StreamOrder for Id
        if (string.IsNullOrEmpty(track.StreamOrder))
        {
            Id = 0;
        }
        else
        {
            Debug.Assert(track.StreamOrder.All(char.IsDigit));
            Id = long.Parse(track.StreamOrder, CultureInfo.InvariantCulture);
        }

        // Use Id for Number
        if (string.IsNullOrEmpty(track.Id))
        {
            Number = 0;
        }
        else
        {
            Debug.Assert(track.Id.All(char.IsDigit));
            Number = long.Parse(track.Id, CultureInfo.InvariantCulture);
        }

        // Use UniqueId for Uid
        if (string.IsNullOrEmpty(track.UniqueId))
        {
            Uid = 0;
        }
        else
        {
            Debug.Assert(track.UniqueId.All(char.IsDigit));
            Uid = ulong.Parse(track.UniqueId, CultureInfo.InvariantCulture);
        }

        // Check title for tags
        HasTags = TitleIsTag();

        // TODO: Set flags from title, flags are incomplete

        return true;
    }

    private bool HandleSubTracks(MediaInfoToolXmlSchema.Track track)
    {
        // StreamOrder maps to Id
        // Id maps to Number
        // UniqueId maps to Uid

        // Only subtitle tracks containing closed captions are handled in SubtitleProps.HandleClosedCaptions()
        // All other sub-tracks are ignored
        // Look for any non-numeric in Id
        if (!string.IsNullOrEmpty(track.Id) && !track.Id.All(char.IsDigit))
        {
            // Ignoring sub-track
            Log.Warning(
                "{Parser} : {Type} : Ignoring sub-track : Id: {Id}, Number: {Id}, Container: {Container} : {FileName}",
                Parent.Parser,
                Type,
                track.StreamOrder,
                track.Id,
                Parent.Container,
                Parent.FileName
            );
            return false;
        }

        // Sanitize StreamOrder
        if (!string.IsNullOrEmpty(track.StreamOrder) && !track.StreamOrder.All(char.IsDigit))
        {
            if (!MediaInfo.Tool.ParseSubTrack(track.StreamOrder, out long trackId))
            {
                Log.Error(
                    "{Parser} : {Type} : Failed to parse sub-track number : Id: {Id}, Container: {Container} : {FileName}",
                    Parent.Parser,
                    Type,
                    track.StreamOrder,
                    Parent.Container,
                    Parent.FileName
                );
                return false;
            }
            track.StreamOrder = trackId.ToString(CultureInfo.InvariantCulture);
        }

        return true;
    }

    public virtual void WriteLine() =>
        Log.Information(
            "{Parser} : {Type} : Format: {Format}, Codec: {Codec}, Language: {Language}, Ietf: {Ietf}, "
                + "Title: {Title}, Flags: {Flags}, State: {State}, Errors: {Errors}, Tags: {Tags}, "
                + "Id: {Id}, Number: {Number}, Uid: {Uid}, Container: {Container} : {FileName}",
            Parent.Parser,
            Type,
            Format,
            Codec,
            Language,
            LanguageIetf,
            Title,
            Flags,
            State,
            HasErrors,
            HasTags,
            Id,
            Number,
            Uid,
            Parent.Container,
            Parent.FileName
        );

    public virtual void WriteLine(string prefix) =>
        Log.Information(
            "{Prefix} : "
                + "{Parser} : {Type} : Format: {Format}, Codec: {Codec}, Language: {Language}, Ietf: {Ietf}, "
                + "Title: {Title}, Flags: {Flags}, State: {State}, Errors: {Errors}, Tags: {Tags}, "
                + "Id: {Id}, Number: {Number}, Uid: {Uid}, Container: {Container} : {FileName}",
            prefix,
            Parent.Parser,
            Type,
            Format,
            Codec,
            Language,
            LanguageIetf,
            Title,
            Flags,
            State,
            HasErrors,
            HasTags,
            Id,
            Number,
            Uid,
            Parent.Container,
            Parent.FileName
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
                    "{Parser} : {Type} : Setting track Flag from Title : Title: {Title}, Flag: {Flag}, State: {State} : {FileName}",
                    Parent.Parser,
                    Type,
                    Title,
                    item.Flag,
                    State,
                    Parent.FileName
                );
            }
        });

    public static IEnumerable<FlagsType> GetFlags(FlagsType flagsType) =>
        Enum.GetValues<FlagsType>()
            .Where(enumValue => flagsType.HasFlag(enumValue) && enumValue != FlagsType.None);

    public static IEnumerable<FlagsType> GetFlags() =>
        Enum.GetValues<FlagsType>().Where(enumValue => enumValue != FlagsType.None);
}
