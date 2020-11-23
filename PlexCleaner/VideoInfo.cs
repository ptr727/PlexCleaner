using System;

// TODO : Find a better way to create profile levels
// https://trac.ffmpeg.org/ticket/2901
// https://stackoverflow.com/questions/42619191/what-does-level-mean-in-ffprobe-output
// https://www.ffmpeg.org/doxygen/3.2/nvEncodeAPI_8h_source.html#l00331
// https://www.ffmpeg.org/doxygen/3.2/avcodec_8h_source.html#l03210
// https://www.ffmpeg.org/doxygen/3.2/mpeg12enc_8c_source.html#l00138
// https://en.wikipedia.org/wiki/H.264/MPEG-4_AVC#Levels

namespace PlexCleaner
{
    public class VideoInfo : TrackInfo
    {
        public VideoInfo() { }

        internal VideoInfo(MkvToolJsonSchema.Track track) : base(track)
        {
            // Profile missing
            // Interlaced missing
        }

        internal VideoInfo(FfMpegToolJsonSchema.Stream stream) : base(stream)
        {
            // Re-assign Codec to the CodecTagString instead of the CodecLongName
            // We need the tag for sub-formats like DivX / DX50
            // Ignore bad tags like 0x0000 / [0][0][0][0]
            if (!string.IsNullOrEmpty(stream.CodecTagString) &&
                !stream.CodecTagString.Contains("[0]", StringComparison.OrdinalIgnoreCase))
                Codec = stream.CodecTagString;

            // Build the Profile
            if (!string.IsNullOrEmpty(stream.Profile) && !string.IsNullOrEmpty(stream.Level))
                Profile = $"{stream.Profile}@{stream.Level}";
            else if (!string.IsNullOrEmpty(stream.Profile))
                Profile = stream.Profile;

            // Test for interlaced
            // https://ffmpeg.org/ffprobe-all.html
            // Progressive, tt, bb, tb, bt
            Interlaced = !string.IsNullOrEmpty(stream.FieldOrder) &&
                         !stream.FieldOrder.Equals("Progressive", StringComparison.OrdinalIgnoreCase);
        }

        internal VideoInfo(MediaInfoToolXmlSchema.Track track) : base(track)
        {
            // Build the Profile
            if (!string.IsNullOrEmpty(track.FormatProfile) && !string.IsNullOrEmpty(track.FormatLevel))
                Profile = $"{track.FormatProfile}@{track.FormatLevel}";
            else if (!string.IsNullOrEmpty(track.FormatProfile))
                Profile = track.FormatProfile;

            // Test for interlaced
            // TODO : Does not currently work for HEVC
            // https://sourceforge.net/p/mediainfo/bugs/771/
            // https://github.com/MediaArea/MediaInfoLib/issues/1092
            // Test for Progressive, Interlaced, MBAFF, or empty
            Interlaced = !string.IsNullOrEmpty(track.ScanType) &&
                         !track.ScanType.Equals("Progressive", StringComparison.OrdinalIgnoreCase);
        }

        public string Profile { get; set; } = "";

        public bool Interlaced { get; set; }

        public bool CompareVideo(VideoInfo compare)
        {
            if (compare == null)
                throw new ArgumentNullException(nameof(compare));

            // Match the Format, Codec, and Profile
            // * is a wildcard match
            bool formatMatch = compare.Format.Equals(Format, StringComparison.OrdinalIgnoreCase) || 
                               compare.Format.Equals("*", StringComparison.OrdinalIgnoreCase);
            bool codecMatch = compare.Codec.Equals(Codec, StringComparison.OrdinalIgnoreCase) || 
                              compare.Codec.Equals("*", StringComparison.OrdinalIgnoreCase);
            bool profileMatch = compare.Profile.Equals(Profile, StringComparison.OrdinalIgnoreCase) || 
                                compare.Profile.Equals("*", StringComparison.OrdinalIgnoreCase);

            return formatMatch && codecMatch && profileMatch;
        }

        public override string ToString()
        {
            return $"Video : Profile : {Profile}, Interlaced : {Interlaced}, {base.ToString()}";
        }
    }
}