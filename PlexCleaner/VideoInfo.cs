using System;

namespace PlexCleaner
{
    public static partial class Info
    {
        public class VideoInfo : TrackInfo
        {
            public VideoInfo() { }
            public VideoInfo(MkvTool.TrackJson track) : base(track)
            {
                // No profile information in MKVMerge
            }
            internal VideoInfo(FfMpegTool.StreamJson stream) : base(stream)
            {
                // TODO : Find a better way to do this
                // https://trac.ffmpeg.org/ticket/2901
                // https://stackoverflow.com/questions/42619191/what-does-level-mean-in-ffprobe-output
                // https://www.ffmpeg.org/doxygen/3.2/nvEncodeAPI_8h_source.html#l00331
                // https://www.ffmpeg.org/doxygen/3.2/avcodec_8h_source.html#l03210
                // https://www.ffmpeg.org/doxygen/3.2/mpeg12enc_8c_source.html#l00138
                // https://en.wikipedia.org/wiki/H.264/MPEG-4_AVC#Levels
                if (!string.IsNullOrEmpty(stream.Profile) && !string.IsNullOrEmpty(stream.Level))
                    Profile = $"{stream.Profile}@{stream.Level}";
                else if (!string.IsNullOrEmpty(stream.Profile))
                    Profile = stream.Profile;
            }
            public VideoInfo(MediaInfoTool.TrackXml track) : base(track)
            {
                // TODO : Find a better way to do this
                if (!string.IsNullOrEmpty(track.FormatProfile) && !string.IsNullOrEmpty(track.FormatLevel))
                    Profile = $"{track.FormatProfile}@{track.FormatLevel}";
                else if (!string.IsNullOrEmpty(track.FormatProfile))
                    Profile = track.FormatProfile;

                // Used for interlaced detections
                ScanType = track.ScanType;
            }

            public bool CompareVideo(VideoInfo compare)
            {
                if (compare == null)
                    throw new ArgumentNullException(nameof(compare));

                // Match the logic in Process
                // Compare the format
                if (!Format.Equals(compare.Format, StringComparison.OrdinalIgnoreCase))
                    return false;

                return string.IsNullOrEmpty(compare.Profile) || 
                       compare.Profile.Equals("*", StringComparison.OrdinalIgnoreCase) ||
                       Profile.Equals(compare.Profile, StringComparison.OrdinalIgnoreCase);
            }

            public override string ToString()
            {
                return $"Video : Format : {Format}, Codec : {Codec}, Profile : {Profile}, Language : {Language}, Id : {Id}, Number : {Number}, ScanType : {ScanType}";
            }
        }
    }
}