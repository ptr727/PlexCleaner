using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog;

namespace PlexCleaner;

public partial class TrackInfo
{
    public enum StateType 
    { 
        None, 
        Keep, // PassThrough
        Remove, 
        ReMux, 
        ReEncode, 
        DeInterlace,
        SetFlags,
        SetLanguage
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
        Commentary = 1 << 6
    }

    protected TrackInfo() { }

    internal TrackInfo(MkvToolJsonSchema.Track trackJson)
    {
        Format = trackJson.Codec;
        Codec = trackJson.Properties.CodecId;
        Title = trackJson.Properties.TrackName;
        
        if (trackJson.Properties.DefaultTrack)
        {
            Flags |= FlagsType.Default;
        }
        if (trackJson.Properties.Original)
        {
            Flags |= FlagsType.Original;
        }
        if (trackJson.Properties.Commentary)
        {
            Flags |= FlagsType.Commentary;
        }
        if (trackJson.Properties.VisualImpaired)
        {
            Flags |= FlagsType.VisualImpaired;
        }
        if (trackJson.Properties.HearingImpaired)
        {
            Flags |= FlagsType.HearingImpaired;
        }
        if (trackJson.Properties.TextDescriptions)
        {
            Flags |= FlagsType.Descriptions;
        }
        if (trackJson.Properties.Forced)
        {
            Flags |= FlagsType.Forced;
        }

        // ISO 639-2B tag
        Language = trackJson.Properties.Language;

        // IETF / RFC 5646 BCP 47 tag
        // https://gitlab.com/mbunkus/mkvtoolnix/-/wikis/Languages-in-Matroska-and-MKVToolNix
        // https://r12a.github.io/app-subtags/
        LanguageIetf = trackJson.Properties.LanguageIetf;

        // If both Language and LanguageIetf are set, verify they match
        if (!string.IsNullOrEmpty(Language) && !string.IsNullOrEmpty(LanguageIetf))
        {
            // Lookup the ISO-639-2B from LanguageIetf
            var lookupLanguage = PlexCleaner.Language.GetIso639Tag(LanguageIetf, true);
            if (string.IsNullOrEmpty(lookupLanguage))
            {
                // TODO: Using CultureInfo is not a reliable lookup tool, e.g.
                // "cmn-Hant" !-> "chi"
                // "yue-Hant"!-> "chi"
                // "no-NO"!-> "nor"
                Log.Logger.Warning("MkvToolJsonSchema : Failed to lookup ISO639 Language from IETF LanguageIetf : {LanguageIetf} !-> {Language}", LanguageIetf, Language);
            }
            // Compare lookup language Language
            else if (!Language.Equals(lookupLanguage, StringComparison.OrdinalIgnoreCase))
            {
                // Set track error and recommend ReMux
                HasErrors = true;
                State = StateType.ReMux;
                Log.Logger.Warning("MkvToolJsonSchema : LanguageIetf to Language Mismatch : {LanguageIetf} !-> {Language} : {State}", LanguageIetf, Language, State);
            }
        }

        // Language is set but IETF language is not set
        if (!string.IsNullOrEmpty(Language) && string.IsNullOrEmpty(LanguageIetf))
        {
            // Set track error and recommend SetLanguage (ReMux with check for SetIetfLanguageTags)
            HasErrors = true;
            State = StateType.SetLanguage;

            // Get the RFC-5646 tag from the ISO-639-2B tag
            var lookupLanguage = PlexCleaner.Language.GetIetfTag(Language, true);
            if (string.IsNullOrEmpty(lookupLanguage))
            {
                // No matching language found
                Log.Logger.Warning("MkvToolJsonSchema : IETF language not set, failed to lookup IETF language from ISO639 language : {Language} : {State}", Language, State);
            }
            else 
            {
                // Set IETF from lookup
                LanguageIetf = lookupLanguage;
                Log.Logger.Warning("MkvToolJsonSchema : IETF language not set, converting ISO639 to IETF : {Language} -> {LanguageIetf} : {State}", Language, LanguageIetf, State);
            }
        }

        // Language is not set but IETF language is set
        if (string.IsNullOrEmpty(Language) && !string.IsNullOrEmpty(LanguageIetf))
        {
            // Set track error and recommend remux
            HasErrors = true;
            State = StateType.ReMux;

            // Get the ISO-639-2B tag from the RFC-5646 tag
            var lookupLanguage = PlexCleaner.Language.GetIso639Tag(LanguageIetf, true);
            if (string.IsNullOrEmpty(lookupLanguage))
            {
                // No matching language found
                Log.Logger.Warning("MkvToolJsonSchema : ISO639 language not set, failed to lookup ISO639 language from IETF language : {Language} : {State}", LanguageIetf, State);
            }
            else
            {
                // Set ISO-639-2B from lookup
                Language = lookupLanguage;
                Log.Logger.Warning("MkvToolJsonSchema : ISO639 language not set, converting IETF to ISO639: {LanguageIetf} -> {Language} : {State}", LanguageIetf, Language, State);
            }
        }

        // If the "language" and "tag_language" fields are set FfProbe uses the tag language instead of the track language
        // https://github.com/MediaArea/MediaAreaXml/issues/34
        if (!string.IsNullOrEmpty(trackJson.Properties.TagLanguage) &&
            !string.IsNullOrEmpty(trackJson.Properties.Language) &&
            !trackJson.Properties.Language.Equals(trackJson.Properties.TagLanguage, StringComparison.OrdinalIgnoreCase))
        {
            // Set track error and recommend remux
            HasErrors = true;
            State = StateType.ReMux;
            Log.Logger.Warning("MkvToolJsonSchema : Tag Language and Track Language Mismatch : {TagLanguage} != {Language} : {State}", 
                trackJson.Properties.TagLanguage, trackJson.Properties.Language, State);
        }

        // Take care to use id and number correctly in MkvMerge and MkvPropEdit
        // https://gitlab.com/mbunkus/mkvtoolnix/-/wikis/About-track-UIDs,-track-numbers-and-track-IDs#track-numbers
        // Id: 0-based track number internally assigned
        Id = trackJson.Id;
        // Number: 1-based track number from Matroska header
        Number = trackJson.Properties.Number;

        // TODO: Anything other than title for tags?
        HasTags = NotTrackTitleFlag();

        // Set flags from title
        SetFlagsFromTitle("MkvToolJsonSchema");

        // Verify required info
        Debug.Assert(!string.IsNullOrEmpty(Format));
        Debug.Assert(!string.IsNullOrEmpty(Codec));
    }

    internal TrackInfo(FfMpegToolJsonSchema.Stream trackJson)
    {
        Format = trackJson.CodecName;
        Codec = trackJson.CodecLongName;

        if (trackJson.Disposition.Default)
        {
            Flags |= FlagsType.Default;
        }
        if (trackJson.Disposition.Forced)
        {
            Flags |= FlagsType.Forced;
        }
        if (trackJson.Disposition.Original)
        {
            Flags |= FlagsType.Original;
        }
        if (trackJson.Disposition.Comment)
        {
            Flags |= FlagsType.Commentary;
        }
        if (trackJson.Disposition.HearingImpaired)
        {
            Flags |= FlagsType.HearingImpaired;
        }
        if (trackJson.Disposition.VisualImpaired)
        {
            Flags |= FlagsType.VisualImpaired;
        }
        if (trackJson.Disposition.Descriptions)
        {
            Flags |= FlagsType.Descriptions;
        }

        Title = trackJson.Tags.FirstOrDefault(item => item.Key.Equals("title", StringComparison.OrdinalIgnoreCase)).Value ?? "";
        Language = trackJson.Tags.FirstOrDefault(item => item.Key.Equals("language", StringComparison.OrdinalIgnoreCase)).Value ?? "";

        // TODO: FfProbe uses the tag language value instead of the track language
        // Some files show MediaInfo and MkvMerge say language is "eng", FfProbe says language is "und"
        // https://github.com/MediaArea/MediaAreaXml/issues/34

        // Some sample files use "???" or "null" for the language
        if (Language.Equals("???", StringComparison.OrdinalIgnoreCase) ||
            Language.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            // Set track error and recommend remux
            HasErrors = true;
            State = StateType.ReMux;
            Log.Logger.Warning("FfMpegToolJsonSchema : Invalid Language : {Language} : {State}", Language, State);
        }

        // Leave the Language as is, no need to verify

        // Use stream index for id and number
        // TODO: How to get Matroska TrackNumber from ffProbe?
        Id = trackJson.Index;
        Number = trackJson.Index;

        // TODO: Anything other than title for tags?
        HasTags = NotTrackTitleFlag();

        // TODO: Set flags from title
        // Repair uses MkvPropEdit, only set for MkvMergeInfo
        // SetFlagsFromTitle("FfMpegToolJsonSchema");

        // Verify required info
        Debug.Assert(!string.IsNullOrEmpty(Format));
        Debug.Assert(!string.IsNullOrEmpty(Codec));
    }

    internal TrackInfo(MediaInfoToolXmlSchema.Track trackXml)
    {
        Format = trackXml.Format;
        Codec = trackXml.CodecId;
        Title = trackXml.Title;
        Language = trackXml.Language;

        // TODO: Missing flags
        // Original
        // Commentary
        // VisualImpaired
        // HearingImpaired
        // Descriptions

        if (trackXml.Default)
        {
            Flags |= FlagsType.Default;
        }
        if (trackXml.Forced)
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
        var match = TrackRegex().Match(trackXml.Id);
        Debug.Assert(match.Success);
        // Use number before dash as Matroska TrackNumber
        Number = int.Parse(match.Groups["id"].Value);

        // Use StreamOrder for Id
        Id = trackXml.StreamOrder;

        // TODO: Anything other than title for tags?
        HasTags = NotTrackTitleFlag();

        // TODO: Set flags from title, flags are incomplete
         // SetFlagsFromTitle("MediaInfoToolXmlSchema");

        // Verify required info
        Debug.Assert(!string.IsNullOrEmpty(Format));
        Debug.Assert(!string.IsNullOrEmpty(Codec));
    }

    public string Format { get; set; } = "";
    public string Codec { get; set; } = "";
    public string Language { get; set; } = "";
    public string LanguageIetf { get; set; } = "";
    public string LanguageAny { get => !string.IsNullOrEmpty(LanguageIetf) ? LanguageIetf : Language; }
    public int Id { get; set; }
    public int Number { get; set; }
    public StateType State { get; set; } = StateType.None;
    public string Title { get; set; } = "";
    public bool HasTags { get; set; }
    public bool HasErrors { get; set; }
    public FlagsType Flags { get; set; } = FlagsType.None;

    public virtual void WriteLine(string prefix)
    {
        Log.Logger.Information("{Prefix} : Type: {Type}, Format: {Format}, Codec: {Codec}, Language: {Language}, LanguageIetf: {LanguageIetf}, Id: {Id}, " +
                               "Number: {Number}, Title: {Title}, Flags: {Flags}, State: {State}, HasErrors: {HasErrors}, HasTags: {HasTags}",
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
            HasTags);
    }
    
    public bool NotTrackTitleFlag()
    {
        // NOT logic, i.e. title is not a flag
        if (string.IsNullOrEmpty(Title))
        { 
            // Empty is NOT a flag
            return false;
        }

        // NOT a flag is NOT a flag
        return !TitleFlags.Any(tuple => Title.Contains(tuple.Item1, StringComparison.OrdinalIgnoreCase));
    }

    public void SetFlagsFromTitle(string log)
    {
        // Add flags based on titles
        foreach (var tuple in TitleFlags)
        {
            // Only process if matching flag is not already set
            if (Title.Contains(tuple.Item1, StringComparison.OrdinalIgnoreCase) &&
                !Flags.HasFlag(tuple.Item2))
            {
                // Set track error state and recommend setting the track flags
                HasErrors = true;
                State = StateType.SetFlags;
                Flags |= tuple.Item2;
                Log.Logger.Warning("{Log} : Setting track Flag from Title : {Title} -> {Flag} : {State}", log, Title, tuple.Item2, State);
            }
        }
    }

    public bool MatchCoverArt()
    {
        return MatchCoverArt(Codec) || MatchCoverArt(Format);
    }

    public static bool MatchCoverArt(string codec)
    {
        return CoverArtFormat.Any(cover => codec.Contains(cover, StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<FlagsType> GetFlags(FlagsType flagsType)
    {
        return Enum.GetValues<FlagsType>().Where(enumValue => flagsType.HasFlag(enumValue) && enumValue != FlagsType.None);
    }

    public static IEnumerable<FlagsType> GetFlags()
    {
        return Enum.GetValues<FlagsType>().Where(enumValue => enumValue != FlagsType.None);
    }

    [GeneratedRegex(@"(?<id>\d)")]
    private static partial Regex TrackRegex();

    // Cover art and thumbnail formats
    private static readonly string[] CoverArtFormat = { "jpg", "jpeg", "png" };

    // Track title to flag mapping
    private static readonly ValueTuple<string, FlagsType>[] TitleFlags = 
    { 
        new ("SDH", FlagsType.HearingImpaired),
        new ("CC", FlagsType.HearingImpaired),
        new ("Commentary", FlagsType.Commentary),
        new ("Forced", FlagsType.Forced)
    };
}
