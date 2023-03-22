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
        Keep, 
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

    internal TrackInfo(MkvToolJsonSchema.Track track)
    {
        if (track == null)
        {
            throw new ArgumentNullException(nameof(track));
        }

        Format = track.Codec;
        Codec = track.Properties.CodecId;
        Title = track.Properties.TrackName;
        
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

        // ISO 639-3 tag
        Language = track.Properties.Language;
        
        // IETF / BCP 47 / RFC 5646 tag
        // https://gitlab.com/mbunkus/mkvtoolnix/-/wikis/Languages-in-Matroska-and-MKVToolNix
        // https://r12a.github.io/app-subtags/
        LanguageIetf = track.Properties.LanguageIetf;

        // Convert the ISO 639-3 tag to RFC 5646
        if (string.IsNullOrEmpty(track.Properties.LanguageIetf) &&
            !string.IsNullOrEmpty(track.Properties.Language))
        {
            var lookupLanguage = PlexCleaner.Language.GetIetfTag(Language, true);
            if (string.IsNullOrEmpty(lookupLanguage))
            {
                // TODO: Will remux fix this?
                Log.Logger.Warning("MkvToolJsonSchema : Failed to lookup IETF language from ISO639-3 language : {Language}", Language);
                HasErrors = true;
            }
            else 
            {
                Log.Logger.Information("MkvToolJsonSchema : Assigning IETF Language from ISO639-3 Language : {Language} -> {IETFLanguage}", Language, lookupLanguage);
                LanguageIetf = lookupLanguage;
            }
        }

        // If the "language" and "tag_language" fields are set FfProbe uses the tag language instead of the track language
        // https://github.com/MediaArea/MediaAreaXml/issues/34
        if (!string.IsNullOrEmpty(track.Properties.TagLanguage) &&
            !string.IsNullOrEmpty(track.Properties.Language) &&
            !track.Properties.Language.Equals(track.Properties.TagLanguage, StringComparison.OrdinalIgnoreCase))
        {
            Log.Logger.Warning("MkvToolJsonSchema : Tag Language and Track Language Mismatch : {TagLanguage} != {Language}", track.Properties.TagLanguage, track.Properties.Language);
            HasErrors = true;
        }

        // Take care to use id and number correctly in MkvMerge and MkvPropEdit
        Id = track.Id;
        Number = track.Properties.Number;

        // Has tags
        HasTags = IsTagTitle(Title);

        // Verify required info
        Debug.Assert(!string.IsNullOrEmpty(Format));
        Debug.Assert(!string.IsNullOrEmpty(Codec));
    }

    internal TrackInfo(FfMpegToolJsonSchema.Stream stream)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        Format = stream.CodecName;
        Codec = stream.CodecLongName;

        if (stream.Disposition.Default)
        {
            Flags |= FlagsType.Default;
        }
        if (stream.Disposition.Forced)
        {
            Flags |= FlagsType.Forced;
        }
        if (stream.Disposition.Original)
        {
            Flags |= FlagsType.Original;
        }
        if (stream.Disposition.Comment)
        {
            Flags |= FlagsType.Commentary;
        }
        if (stream.Disposition.HearingImpaired)
        {
            Flags |= FlagsType.HearingImpaired;
        }
        if (stream.Disposition.VisualImpaired)
        {
            Flags |= FlagsType.VisualImpaired;
        }
        if (stream.Disposition.Descriptions)
        {
            Flags |= FlagsType.Descriptions;
        }

        Title = stream.Tags.FirstOrDefault(item => item.Key.Equals("title", StringComparison.OrdinalIgnoreCase)).Value ?? "";
        Language = stream.Tags.FirstOrDefault(item => item.Key.Equals("language", StringComparison.OrdinalIgnoreCase)).Value ?? "";

        // TODO: FfProbe uses the tag language value instead of the track language
        // Some files show MediaInfo and MkvMerge say language is "eng", FfProbe says language is "und"
        // https://github.com/MediaArea/MediaAreaXml/issues/34

        // Some sample files use "???" or "null" for the language
        if (Language.Equals("???", StringComparison.OrdinalIgnoreCase) ||
            Language.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            Log.Logger.Warning("FfMpegToolJsonSchema : Invalid Language : {Language}", Language);
            HasErrors = true;
        }

        // Leave the Language as is, no need to verify

        // Use index for number
        Id = stream.Index;
        Number = stream.Index;

        // Has tags
        HasTags = IsTagTitle(Title);

        // Verify required info
        Debug.Assert(!string.IsNullOrEmpty(Format));
        Debug.Assert(!string.IsNullOrEmpty(Codec));
    }

    internal TrackInfo(MediaInfoToolXmlSchema.Track track)
    {
        if (track == null)
        {
            throw new ArgumentNullException(nameof(track));
        }

        Format = track.Format;
        Codec = track.CodecId;
        Title = track.Title;
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

        // ID can be in a variety of formats:
        // 1
        // 3-CC1
        // 1 / 8876149d-48f0-4148-8225-dc0b53a50b90
        // https://github.com/MediaArea/MediaInfo/issues/201
        var match = TrackRegex().Match(track.Id);
        Debug.Assert(match.Success);
        Id = int.Parse(match.Groups["id"].Value);

        // Use streamorder for number
        Number = track.StreamOrder;

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
