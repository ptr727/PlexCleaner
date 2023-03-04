using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog;

namespace PlexCleaner;

public partial class TrackInfo
{
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
        Default = track.Properties.DefaultTrack;

        // TODO: Add support for new BCP 47 language tag support
        // https://gitlab.com/mbunkus/mkvtoolnix/-/wikis/Languages-in-Matroska-and-MKVToolNix

        // If the "language" and "tag_language" fields are set FfProbe uses the tag language instead of the track language
        // https://github.com/MediaArea/MediaAreaXml/issues/34
        if (!string.IsNullOrEmpty(track.Properties.TagLanguage) &&
            !string.IsNullOrEmpty(track.Properties.Language) &&
            !track.Properties.Language.Equals(track.Properties.TagLanguage, StringComparison.OrdinalIgnoreCase))
        {
            HasErrors = true;
            Log.Logger.Warning("Tag and Track Language Mismatch : {TagLanguage} != {Language}", track.Properties.TagLanguage, track.Properties.Language);
        }

        // Set language
        if (string.IsNullOrEmpty(track.Properties.Language))
        {
            Language = "und";
        }
        else
        {
            // MkvMerge normally sets the language to und or 3 letter ISO 639-2 code
            // Try to lookup the language to make sure it is correct
            var lang = PlexCleaner.Language.GetIso6393(track.Properties.Language);
            if (lang != null)
            {
                Language = lang.Part2B;
            }
            else
            {
                HasErrors = true;
                Log.Logger.Warning("Invalid Language : {Language}", track.Properties.Language);
                Language = "und";
            }
        }

        // Take care to use id and number correctly in MkvMerge and MkvPropEdit
        Id = track.Id;
        Number = track.Properties.Number;

        // Has tags
        HasTags = MediaInfo.IsTagTitle(Title);

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

        // Fixed attributes
        Format = stream.CodecName;
        Codec = stream.CodecLongName;
        Default = stream.Disposition.Default;

        // Variable attributes
        Title = stream.Tags.FirstOrDefault(item => item.Key.Equals("title", StringComparison.OrdinalIgnoreCase)).Value ?? "";
        Language = stream.Tags.FirstOrDefault(item => item.Key.Equals("language", StringComparison.OrdinalIgnoreCase)).Value ?? "";

        // TODO: FfProbe uses the tag language value instead of the track language
        // Some files show MediaInfo and MkvMerge say language is "eng", FfProbe says language is "und"
        // https://github.com/MediaArea/MediaAreaXml/issues/34

        // Set language
        if (string.IsNullOrEmpty(Language))
        {
            Language = "und";
        }
        // Some sample files are "???" or "null", set to und
        else if (Language.Equals("???", StringComparison.OrdinalIgnoreCase) ||
                 Language.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            HasErrors = true;
            Log.Logger.Warning("Invalid Language : {Language}", Language);
            Language = "und";
        }
        else
        {
            // FfProbe normally sets a 3 letter ISO 639-2 code, but some samples have 2 letter codes
            // Try to lookup the language to make sure it is correct
            var lang = PlexCleaner.Language.GetIso6393(Language);
            if (lang != null)
            {
                Language = lang.Part2B;
            }
            else
            {
                HasErrors = true;
                Log.Logger.Warning("Invalid Language : {Language}", Language);
                Language = "und";
            }
        }

        // Use index for number
        Id = stream.Index;
        Number = stream.Index;

        // Has tags
        HasTags = MediaInfo.IsTagTitle(Title);

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
        Default = track.Default;

        // Set language
        if (string.IsNullOrEmpty(track.Language))
        {
            Language = "und";
        }
        else
        {
            // MediaInfo uses ab or abc or ab-cd tags, we need to convert to ISO 639-2
            // https://github.com/MediaArea/MediaAreaXml/issues/33
            // Try to lookup the language to make sure it is correct
            var lang = PlexCleaner.Language.GetIso6393(track.Language);
            if (lang != null)
            {
                Language = lang.Part2B;
            }
            else
            {
                HasErrors = true;
                Log.Logger.Warning("Invalid Language : {Language}", track.Language);
                Language = "und";
            }
        }

        // FfProbe and MkvMerge use chi not zho
        // https://github.com/mbunkus/mkvtoolnix/issues/1149
        if (Language.Equals("zho", StringComparison.OrdinalIgnoreCase))
        {
            Language = "chi";
        }

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
        HasTags = MediaInfo.IsTagTitle(Title);

        // Verify required info
        Debug.Assert(!string.IsNullOrEmpty(Format));
        Debug.Assert(!string.IsNullOrEmpty(Codec));
    }

    public string Format { get; set; } = "";
    public string Codec { get; set; } = "";
    public string Language { get; set; } = "";
    public int Id { get; set; }
    public int Number { get; set; }
    public enum StateType { None, Keep, Remove, ReMux, ReEncode, DeInterlace }
    public StateType State { get; set; } = StateType.None;
    public string Title { get; set; } = "";
    public bool Default { get; set; }
    public bool HasTags { get; set; }
    public bool HasErrors { get; set; }

    public bool IsLanguageUnknown()
    {
        // Test for empty or "und" field values
        return string.IsNullOrEmpty(Language) ||
               Language.Equals("und", StringComparison.OrdinalIgnoreCase);
    }

    public virtual void WriteLine(string prefix)
    {
        Log.Logger.Information("{Prefix} : Type: {Type}, Format: {Format}, Codec: {Codec}, Language: {Language}, Id: {Id}, Number: {Number}, " +
                               "Title: {Title}, Default: {Default}, State: {State}, HasErrors: {HasErrors}, HasTags: {HasTags}",
            prefix,
            GetType().Name,
            Format,
            Codec,
            Language,
            Id,
            Number,
            Title,
            Default,
            State,
            HasErrors,
            HasTags);
    }

    [GeneratedRegex(@"(?<id>\d)")]
    private static partial Regex TrackRegex();
}
