using System;
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
        DeInterlace 
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

        // ISO 639-3 tag
        Language = trackJson.Properties.Language;
        
        // IETF / BCP 47 / RFC 5646 tag
        // https://gitlab.com/mbunkus/mkvtoolnix/-/wikis/Languages-in-Matroska-and-MKVToolNix
        // https://r12a.github.io/app-subtags/
        LanguageIetf = trackJson.Properties.LanguageIetf;

        // Setting HasErrors will force a remux and MkvMerge will correct track languages errors

        // Convert the ISO 639-3 tag to RFC 5646
        if (string.IsNullOrEmpty(trackJson.Properties.LanguageIetf) &&
            !string.IsNullOrEmpty(trackJson.Properties.Language))
        {
            var lookupLanguage = PlexCleaner.Language.GetIetfTag(Language, true);
            if (string.IsNullOrEmpty(lookupLanguage))
            {
                Log.Logger.Warning("MkvToolJsonSchema : Failed to lookup IETF language from ISO639-3 language : {Language}", Language);

                // Set track error and recommend remux
                HasErrors = true;
                State = StateType.ReMux;
            }
            else 
            {
                Log.Logger.Warning("MkvToolJsonSchema : IETF language not set, converting ISO639-3 to IETF : {Language} -> {IetfLanguage}", Language, lookupLanguage);
                LanguageIetf = lookupLanguage;

                // Conditionally flag as track error to be corrected with remuxing
                if (Program.Config.ProcessOptions.SetIetfLanguageTags)
                {
                    // Set track error and recommend remux
                    HasErrors = true;
                    State = StateType.ReMux;
                }
            }
        }

        // If the "language" and "tag_language" fields are set FfProbe uses the tag language instead of the track language
        // https://github.com/MediaArea/MediaAreaXml/issues/34
        if (!string.IsNullOrEmpty(trackJson.Properties.TagLanguage) &&
            !string.IsNullOrEmpty(trackJson.Properties.Language) &&
            !trackJson.Properties.Language.Equals(trackJson.Properties.TagLanguage, StringComparison.OrdinalIgnoreCase))
        {
            Log.Logger.Warning("MkvToolJsonSchema : Tag Language and Track Language Mismatch : {TagLanguage} != {Language}", trackJson.Properties.TagLanguage, trackJson.Properties.Language);

            // Set track error and recommend remux
            HasErrors = true;
            State = StateType.ReMux;
        }

        // Take care to use id and number correctly in MkvMerge and MkvPropEdit
        // https://gitlab.com/mbunkus/mkvtoolnix/-/wikis/About-track-UIDs,-track-numbers-and-track-IDs#track-numbers
        // Id: 0-based track number internally assigned
        Id = trackJson.Id;
        // Number: 1-based track number from Matroska header
        Number = trackJson.Properties.Number;

        // Has tags
        HasTags = IsTagTitle(Title);

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
            Log.Logger.Warning("FfMpegToolJsonSchema : Invalid Language : {Language}", Language);

            // Set track error and recommend remux
            HasErrors = true;
            State = StateType.ReMux;
        }

        // Leave the Language as is, no need to verify

        // Use stream index for id and number
        // TODO: How to get Matroska TrackNumber from ffProbe?
        Id = trackJson.Index;
        Number = trackJson.Index;

        // Has tags
        HasTags = IsTagTitle(Title);

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

        // Has tags
        HasTags = IsTagTitle(Title);

        // Verify required info
        Debug.Assert(!string.IsNullOrEmpty(Format));
        Debug.Assert(!string.IsNullOrEmpty(Codec));
    }

    public string Format { get; set; } = "";
    public string Codec { get; set; } = "";
    public string Language { get; set; } = "";
    public string LanguageIetf { get; set; } = "";
    public string AnyLanguage { get => !string.IsNullOrEmpty(LanguageIetf) ? LanguageIetf : Language; }
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

    public static bool IsUsefulTrackTitle(string title)
    {
        // Does the track have a useful title
        return UsefulTitles.Any(useful => title.Equals(useful, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsTagTitle(string title)
    {
        // Empty is not a tag
        if (string.IsNullOrEmpty(title))
        {
            return false;
        }

        // Useful is not a tag
        return !IsUsefulTrackTitle(title);
    }

    public static bool MatchCoverArt(string codec)
    {
        return CoverArtFormat.Any(cover => codec.Contains(cover, StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"(?<id>\d)")]
    private static partial Regex TrackRegex();

    // Cover art and thumbnail formats
    private static readonly string[] CoverArtFormat = { "jpg", "jpeg", "mjpeg", "png" };
    // Not so useful track titles
    private static readonly string[] UsefulTitles = { "SDH", "Commentary", "Forced" };
}
