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
            // ScanType missing
        }

        internal VideoInfo(FfMpegToolJsonSchema.Stream stream) : base(stream)
        {
            // ScanType missing

            // Re-assign Codec to the CodecTagString intead of the CodecLongName
            // We need the tag for sub-formats like DivX / DX50
            // Ignore bad tags like [0][0][0][0]
            if (!string.IsNullOrEmpty(stream.CodecTagString) &&
                !stream.CodecTagString.Contains("[0]", StringComparison.OrdinalIgnoreCase))
                Codec = stream.CodecTagString;

            // Build the Profile
            if (!string.IsNullOrEmpty(stream.Profile) && !string.IsNullOrEmpty(stream.Level))
                Profile = $"{stream.Profile}@{stream.Level}";
            else if (!string.IsNullOrEmpty(stream.Profile))
                Profile = stream.Profile;
        }

        internal VideoInfo(MediaInfoToolXmlSchema.Track track) : base(track)
        {
            // Build the Profile
            if (!string.IsNullOrEmpty(track.FormatProfile) && !string.IsNullOrEmpty(track.FormatLevel))
                Profile = $"{track.FormatProfile}@{track.FormatLevel}";
            else if (!string.IsNullOrEmpty(track.FormatProfile))
                Profile = track.FormatProfile;

            // Used for interlaced detections
            ScanType = track.ScanType;
        }

        public string Profile { get; set; } = "";

        public string ScanType { get; set; } = "";

        public bool IsInterlaced()
        {
            // TODO : Find a better way to do this
            // This only works for MediaInfo
            // http://www.aktau.be/2013/09/22/detecting-interlaced-video-with-ffmpeg/
            // https://sourceforge.net/p/mediainfo/bugs/771/
            // Test for Progressive, Interlaced, MBAFF
            // Not reliable as some media does not have the ScanType field set
            return !ScanType.Equals("Progressive", StringComparison.OrdinalIgnoreCase);
        }

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
            return $"Video : Profile : {Profile}, ScanType : {ScanType}, {base.ToString()}";
        }
    }
}