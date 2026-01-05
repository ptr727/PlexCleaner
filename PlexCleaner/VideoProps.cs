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

public class VideoProps(MediaProps mediaProps) : TrackProps(TrackType.Video, mediaProps)
{
    // Cover art and thumbnail formats
    private static readonly List<string> s_coverArtFormat = ["jpg", "jpeg", "png"];

    public string Profile { get; set; } = string.Empty;

    public bool Interlaced { get; set; }

    public string FormatHdr { get; set; } = string.Empty;

    public bool ClosedCaptions { get; set; }

    public bool CoverArt => MatchCoverArt(Codec) || MatchCoverArt(Format);

    // Required
    // Format = track.Codec;
    // Codec = track.Properties.CodecId;
    public override bool Create(MkvToolJsonSchema.Track track)
    {
        // Call base
        if (!base.Create(track))
        {
            return false;
        }

        // Missing: Profile
        // Missing: Interlaced
        // Missing: HDR
        // Missing: ClosedCaptions

        // Cover art
        if (CoverArt)
        {
            Log.Warning(
                "{Parser} : {Type} : Cover art video track : Format: {Format}, Codec: {Codec} : {FileName}",
                Parent.Parser,
                Type,
                Format,
                Codec,
                Parent.FileName
            );
        }

        return true;
    }

    // Required
    // Format = track.CodecName;
    // Codec = track.CodecLongName;
    public override bool Create(FfMpegToolJsonSchema.Track track)
    {
        // Call base
        if (!base.Create(track))
        {
            return false;
        }

        // Re-assign Codec to the CodecTagString instead of the CodecLongName
        // We need the tag for sub-formats like DivX / DX50
        // Ignore unknown tags like codec_tag: 0x0000 or codec_tag_string: [0][0][0][0]
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
        if (CoverArt)
        {
            Log.Warning(
                "{Parser} : {Type} : Cover art video track : Format: {Format}, Codec: {Codec} : {FileName}",
                Parent.Parser,
                Type,
                Format,
                Codec,
                Parent.FileName
            );
        }

        return true;
    }

    // Required
    // Format = track.Format;
    // Codec = track.CodecId;
    public override bool Create(MediaInfoToolJsonSchema.Track track)
    {
        // Call base
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
        // TODO: May not not work for H265
        // https://github.com/MediaArea/MediaInfoLib/issues/1092
        // Only set when ScanType is Interlaced
        Interlaced = track.ScanType.Equals("Interlaced", StringComparison.OrdinalIgnoreCase);

        // HDR
        FormatHdr = track.HdrFormat;

        // Cover art
        if (CoverArt)
        {
            Log.Warning(
                "{Parser} : {Type} : Cover art video track : Format: {Format}, Codec: {Codec} : {fileName}",
                Parent.Parser,
                Type,
                Format,
                Codec,
                Parent.FileName
            );
        }

        return true;
    }

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
            "{Parser} : {Type} : Format: {Format}, Codec: {Codec}, Language: {Language}, Ietf: {Ietf}, "
                + "Title: {Title}, Flags: {Flags}, State: {State}, Errors: {Errors}, Tags: {Tags}, "
                + "Profile: {Profile}, Interlaced: {Interlaced}, HDR: {HDR}, CC: {CC}, CoverArt: {CoverArt}, "
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
            Profile,
            Interlaced,
            FormatHdr,
            ClosedCaptions,
            CoverArt,
            Id,
            Number,
            Uid,
            Parent.Container,
            Parent.FileName
        );

    public override void WriteLine(string prefix) =>
        // Keep in sync with TrackInfo::WriteLine
        Log.Information(
            "{Prefix} : "
                + "{Parser} : {Type} : Format: {Format}, Codec: {Codec}, Language: {Language}, Ietf: {Ietf}, "
                + "Title: {Title}, Flags: {Flags}, State: {State}, Errors: {Errors}, Tags: {Tags}, "
                + "Profile: {Profile}, Interlaced: {Interlaced}, HDR: {HDR}, CC: {CC}, CoverArt: {CoverArt}, "
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
            Profile,
            Interlaced,
            FormatHdr,
            ClosedCaptions,
            CoverArt,
            Id,
            Number,
            Uid,
            Parent.Container,
            Parent.FileName
        );
}
