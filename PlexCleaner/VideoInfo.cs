using System;
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

public class VideoInfo : TrackInfo
{
    public VideoInfo(MkvToolJsonSchema.Track track)
        : base(track)
    {
        // Missing: Profile
        // Missing: Interlaced
        // Missing: HDR
        // Missing: ClosedCaptions

        // Cover art
        if (IsCoverArt)
        {
            Log.Warning(
                "MkvToolJsonSchema : Cover art video track : {Format}:{Codec}",
                Format,
                Codec
            );
        }
    }

    public VideoInfo(FfMpegToolJsonSchema.Track track)
        : base(track)
    {
        // Re-assign Codec to the CodecTagString instead of the CodecLongName
        // We need the tag for sub-formats like DivX / DX50
        // Ignore bad tags like 0x0000 / [0][0][0][0]
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

        // Test for interlaced
        // https://ffmpeg.org/ffprobe-all.html
        // Progressive, tt, bb, tb, bt
        Interlaced =
            !string.IsNullOrEmpty(track.FieldOrder)
            && !track.FieldOrder.Equals("Progressive", StringComparison.OrdinalIgnoreCase);

        // ClosedCaptions
        ClosedCaptions = track.ClosedCaptions != 0;

        // Missing: HDR

        // Cover art
        if (IsCoverArt)
        {
            Log.Warning(
                "FfMpegToolJsonSchema : Cover art video track : {Format}:{Codec}",
                Format,
                Codec
            );
        }
    }

    public VideoInfo(MediaInfoToolXmlSchema.Track track)
        : base(track)
    {
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
        // https://sourceforge.net/p/mediainfo/bugs/771/
        // https://github.com/MediaArea/MediaInfoLib/issues/1092
        // Test for Progressive, Interlaced, MBAFF, or empty
        Interlaced =
            !string.IsNullOrEmpty(track.ScanType)
            && !track.ScanType.Equals("Progressive", StringComparison.OrdinalIgnoreCase);

        // HDR
        FormatHdr = track.HdrFormat;

        // Missing: ClosedCaptions

        // Cover art
        if (IsCoverArt)
        {
            Log.Warning(
                "MediaInfoToolXmlSchema : Cover art video track : {Format}:{Codec}",
                Format,
                Codec
            );
        }
    }

    public string Profile { get; set; } = "";

    public bool Interlaced { get; set; }

    public string FormatHdr { get; set; } = "";

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

    public override void WriteLine(string prefix) =>
        // Add Profile and Interlaced
        // Keep in sync with TrackInfo::WriteLine
        Log.Information(
            "{Prefix} : Type: {Type}, Format: {Format}, HDR: {Hdr}, Codec: {Codec}, Language: {Language}, LanguageIetf: {LanguageIetf}, "
                + "Id: {Id}, Number: {Number}, Title: {Title}, Flags: {Flags}, Profile: {Profile}, Interlaced: {Interlaced}, "
                + "ClosedCaptions: {ClosedCaptions}, State: {State}, HasErrors: {HasErrors}, HasTags: {HasTags}, IsCoverArt: {IsCoverArt}",
            prefix,
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
            IsCoverArt
        );

    // Cover art and thumbnail formats
    private static readonly string[] s_coverArtFormat = ["jpg", "jpeg", "png"];
}
