using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;

// TODO: Find a better way to create profile levels
// https://trac.ffmpeg.org/ticket/2901
// https://stackoverflow.com/questions/42619191/what-does-level-mean-in-ffprobe-output
// https://www.ffmpeg.org/doxygen/3.2/nvEncodeAPI_8h_source.html#l00331
// https://www.ffmpeg.org/doxygen/3.2/avcodec_8h_source.html#l03210
// https://www.ffmpeg.org/doxygen/3.2/mpeg12enc_8c_source.html#l00138
// https://en.wikipedia.org/wiki/H.264/MPEG-4_AVC#Levels
// https://en.wikipedia.org/wiki/Dolby_Vision

namespace PlexCleaner;

public class VideoProps : TrackProps
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor")]
    public VideoProps(MediaTool.ToolType parser, string fileName)
        : base(parser, fileName) { }

    public override bool Create(MkvToolJsonSchema.Track track)
    {
        // Call base first
        if (!base.Create(track))
        {
            return false;
        }

        // Missing: Profile
        // Missing: Interlaced
        // Missing: HDR
        // Missing: ClosedCaptions

        // Cover art
        if (IsCoverArt)
        {
            Log.Warning(
                "MkvToolJsonSchema : Cover art video track : {Format}:{Codec} : {FileName}",
                Format,
                Codec,
                FileName
            );
        }

        return true;
    }

    public override bool Create(FfMpegToolJsonSchema.Track track)
    {
        // Call base first
        if (!base.Create(track))
        {
            return false;
        }

        // Re-assign Codec to the CodecTagString instead of the CodecLongName
        // We need the tag for sub-formats like DivX / DX50
        // Ignore bad tags like codec_tag: 0x0000 or codec_tag_string: [0][0][0][0]
        if (
            !string.IsNullOrEmpty(track.CodecTagString)
            && !track.CodecTagString.Contains("[0]", StringComparison.OrdinalIgnoreCase)
        )
        {
            Codec = track.CodecTagString;
        }

        // Build the Profile
        Profile = string.IsNullOrEmpty(track.Profile) switch
        {
            false when track.Level != 0 => $"{track.Profile}@{track.Level}",
            false => track.Profile,
            _ => Profile,
        };

        // Test for interlaced in field_order
        // https://ffmpeg.org/ffmpeg-codecs.html
        // Progressive, tt, bb, tb, bt
        Interlaced = track.FieldOrder.ToLowerInvariant() is "tt" or "bb" or "tb" or "bt";

        // ClosedCaptions
        ClosedCaptions = track.ClosedCaptions != 0;

        // Missing: HDR

        // Cover art
        if (IsCoverArt)
        {
            Log.Warning(
                "FfMpegToolJsonSchema : Cover art video track : {Format}:{Codec} : {FileName}",
                Format,
                Codec,
                FileName
            );
        }

        return true;
    }

    public override bool Create(MediaInfoToolXmlSchema.Track track)
    {
        // Call base first
        if (!base.Create(track))
        {
            return false;
        }

        // Build the Profile
        Profile = string.IsNullOrEmpty(track.FormatProfile) switch
        {
            false when !string.IsNullOrEmpty(track.FormatLevel) =>
                $"{track.FormatProfile}@{track.FormatLevel}",
            false => track.FormatProfile,
            _ => Profile,
        };

        // Test for interlaced
        // TODO: Does not work for H265
        // https://github.com/MediaArea/MediaInfoLib/issues/1092
        // Only set when ScanType is Interlaced
        Interlaced = track.ScanType.Equals("Interlaced", StringComparison.OrdinalIgnoreCase);

        // HDR
        FormatHdr = track.HdrFormat;

        // Cover art
        if (IsCoverArt)
        {
            Log.Warning(
                "MediaInfoToolXmlSchema : Cover art video track : {Format}:{Codec} : {fileName}",
                Format,
                Codec,
                FileName
            );
        }

        return true;
    }

    public static VideoProps Create(string fileName, FfMpegToolJsonSchema.Track track)
    {
        VideoProps videoProps = new(MediaTool.ToolType.FfProbe, fileName);
        return videoProps.Create(track) ? videoProps : throw new NotSupportedException();
    }

    public static VideoProps Create(string fileName, MediaInfoToolXmlSchema.Track track)
    {
        VideoProps videoProps = new(MediaTool.ToolType.MediaInfo, fileName);
        return videoProps.Create(track) ? videoProps : throw new NotSupportedException();
    }

    public static VideoProps Create(string fileName, MkvToolJsonSchema.Track track)
    {
        VideoProps videoProps = new(MediaTool.ToolType.MkvMerge, fileName);
        return videoProps.Create(track) ? videoProps : throw new NotSupportedException();
    }

    public string Profile { get; set; } = string.Empty;

    public bool Interlaced { get; set; }

    public string FormatHdr { get; set; } = string.Empty;

    public bool ClosedCaptions { get; set; }

    public bool IsCoverArt => MatchCoverArt(Codec) || MatchCoverArt(Format);

    public static bool MatchCoverArt(string codec) =>
        s_coverArtFormat.Any(cover => codec.Contains(cover, StringComparison.OrdinalIgnoreCase));

    public bool CompareVideo(VideoFormat compare)
    {
        // Match the Format, Codec, and Profile
        // Null or empty string is a wildcard match
        bool formatMatch =
            string.IsNullOrEmpty(compare.Format)
            || compare.Format.Equals(Format, StringComparison.OrdinalIgnoreCase);
        bool codecMatch =
            string.IsNullOrEmpty(compare.Codec)
            || compare.Codec.Equals(Codec, StringComparison.OrdinalIgnoreCase);
        bool profileMatch =
            string.IsNullOrEmpty(compare.Profile)
            || compare.Profile.Equals(Profile, StringComparison.OrdinalIgnoreCase);

        return formatMatch && codecMatch && profileMatch;
    }

    public override void WriteLine() =>
        // Keep in sync with TrackInfo::WriteLine
        Log.Information(
            "Parser: {Parser}, Type: {Type}, Format: {Format}, HDR: {Hdr}, Codec: {Codec}, Language: {Language}, LanguageIetf: {LanguageIetf}, "
                + "Id: {Id}, Number: {Number}, Title: {Title}, Flags: {Flags}, Profile: {Profile}, Interlaced: {Interlaced}, "
                + "ClosedCaptions: {ClosedCaptions}, State: {State}, HasErrors: {HasErrors}, HasTags: {HasTags}, IsCoverArt: {IsCoverArt} : {FileName}",
            Parser,
            GetType().Name,
            Format,
            FormatHdr,
            Codec,
            Language,
            LanguageIetf,
            Id,
            Number,
            Title,
            Flags,
            Profile,
            Interlaced,
            ClosedCaptions,
            State,
            HasErrors,
            HasTags,
            IsCoverArt,
            FileName
        );

    public override void WriteLine(string prefix) =>
        // Keep in sync with TrackInfo::WriteLine
        Log.Information(
            "{Prefix} : Parser: {Parser}, Type: {Type}, Format: {Format}, HDR: {Hdr}, Codec: {Codec}, Language: {Language}, LanguageIetf: {LanguageIetf}, "
                + "Id: {Id}, Number: {Number}, Title: {Title}, Flags: {Flags}, Profile: {Profile}, Interlaced: {Interlaced}, "
                + "ClosedCaptions: {ClosedCaptions}, State: {State}, HasErrors: {HasErrors}, HasTags: {HasTags}, IsCoverArt: {IsCoverArt} : {FileName}",
            prefix,
            Parser,
            GetType().Name,
            Format,
            FormatHdr,
            Codec,
            Language,
            LanguageIetf,
            Id,
            Number,
            Title,
            Flags,
            Profile,
            Interlaced,
            ClosedCaptions,
            State,
            HasErrors,
            HasTags,
            IsCoverArt,
            FileName
        );

    // Cover art and thumbnail formats
    private static readonly List<string> s_coverArtFormat = ["jpg", "jpeg", "png"];
}
