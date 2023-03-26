using System;
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
    internal VideoInfo(MkvToolJsonSchema.Track track) : base(track)
    {
        // Missing: Profile
        // Missing: Interlaced
        // Missing: HDR
        // Missing: ClosedCaptions

        // Cover art
        if (MatchCoverArt())
        {
            Log.Logger.Warning("MkvToolJsonSchema : Cover art video track : {Format}:{Codec}", Format, Codec);
        }
    }

    internal VideoInfo(FfMpegToolJsonSchema.Stream stream) : base(stream)
    {
        // Re-assign Codec to the CodecTagString instead of the CodecLongName
        // We need the tag for sub-formats like DivX / DX50
        // Ignore bad tags like 0x0000 / [0][0][0][0]
        if (!string.IsNullOrEmpty(stream.CodecTagString) &&
            !stream.CodecTagString.Contains("[0]", StringComparison.OrdinalIgnoreCase))
        {
            Codec = stream.CodecTagString;
        }

        // Build the Profile
        Profile = string.IsNullOrEmpty(stream.Profile) switch
        {
            false when !string.IsNullOrEmpty(stream.Level) => $"{stream.Profile}@{stream.Level}",
            false => stream.Profile,
            _ => Profile
        };

        // Test for interlaced
        // https://ffmpeg.org/ffprobe-all.html
        // Progressive, tt, bb, tb, bt
        Interlaced = !string.IsNullOrEmpty(stream.FieldOrder) &&
                     !stream.FieldOrder.Equals("Progressive", StringComparison.OrdinalIgnoreCase);

        // ClosedCaptions
        ClosedCaptions = stream.ClosedCaptions;

        // Missing: HDR

        // Cover art
        if (MatchCoverArt())
        {
            Log.Logger.Warning("FfMpegToolJsonSchema : Cover art video track : {Format}:{Codec}", Format, Codec);
        }
    }

    internal VideoInfo(MediaInfoToolXmlSchema.Track track) : base(track)
    {
        // Build the Profile
        Profile = string.IsNullOrEmpty(track.FormatProfile) switch
        {
            false when !string.IsNullOrEmpty(track.FormatLevel) => $"{track.FormatProfile}@{track.FormatLevel}",
            false => track.FormatProfile,
            _ => Profile
        };

        // Test for interlaced
        // TODO: Does not currently work for HEVC
        // https://sourceforge.net/p/mediainfo/bugs/771/
        // https://github.com/MediaArea/MediaInfoLib/issues/1092
        // Test for Progressive, Interlaced, MBAFF, or empty
        Interlaced = !string.IsNullOrEmpty(track.ScanType) &&
                     !track.ScanType.Equals("Progressive", StringComparison.OrdinalIgnoreCase);

        // HDR
        FormatHdr = track.HdrFormat;

        // Missing: ClosedCaptions

        // Cover art
        if (MatchCoverArt())
        {
            Log.Logger.Warning("MediaInfoToolXmlSchema : Cover art video track : {Format}:{Codec}", Format, Codec);
        }
    }

    public string Profile { get; set; } = "";

    public bool Interlaced { get; set; }

    public string FormatHdr { get; set; } = "";

    public bool ClosedCaptions { get; set; }

    public bool CompareVideo(VideoFormat compare)
    {
        // Match the Format, Codec, and Profile
        // Null or empty string is a wildcard match
        bool formatMatch = string.IsNullOrEmpty(compare.Format) || compare.Format.Equals(Format, StringComparison.OrdinalIgnoreCase);
        bool codecMatch = string.IsNullOrEmpty(compare.Codec) || compare.Codec.Equals(Codec, StringComparison.OrdinalIgnoreCase);
        bool profileMatch = string.IsNullOrEmpty(compare.Profile) || compare.Profile.Equals(Profile, StringComparison.OrdinalIgnoreCase);

        return formatMatch && codecMatch && profileMatch;
    }

    public override void WriteLine(string prefix)
    {
        // Add Profile and Interlaced
        // Keep in sync with TrackInfo::WriteLine
        Log.Logger.Information("{Prefix} : Type: {Type}, Format: {Format}, HDR: {Hdr}, Codec: {Codec}, Language: {Language}, LanguageIetf: {LanguageIetf}, " +
                               "Id: {Id}, Number: {Number}, Title: {Title}, Flags: {Flags}, Profile: {Profile}, Interlaced: {Interlaced}, " +
                               "ClosedCaptions: {ClosedCaptions}, State: {State}, HasErrors: {HasErrors}, HasTags: {HasTags}",
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
            HasTags);
    }
}
