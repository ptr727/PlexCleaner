using System;

namespace PlexCleaner
{
    public class SubtitleInfo : TrackInfo
    {
        public SubtitleInfo() { }
        internal SubtitleInfo(MkvToolJsonSchema.Track track) : base(track) 
        {
            Forced = track.Properties.Forced;
        }
        internal SubtitleInfo(FfMpegToolJsonSchema.Stream stream) : base(stream) 
        {
            Forced = stream.Disposition.Forced;
        }

        internal SubtitleInfo(MediaInfoToolXmlSchema.Track track) : base(track)
        {
            Forced = track.Forced;

            // We need MuxingMode for VOBSUB else Plex on Nvidia Shield TV will hang on play start
            // https://forums.plex.tv/discussion/290723/long-wait-time-before-playing-some-content-player-says-directplay-server-says-transcoding
            // https://github.com/mbunkus/mkvtoolnix/issues/2131
            // https://gitlab.com/mbunkus/mkvtoolnix/-/issues/2131
            if (track.CodecId.Equals("S_VOBSUB", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrEmpty(track.MuxingMode))
            { 
                HasErrors = true;
                Serilog.Log.Logger.Warning("MuxingMode not specified for S_VOBSUB Codec");
            }
        }

        public bool Forced { get; set; }

        public override string ToString()
        {
            return $"Subtitle : Forced : {Forced}, {base.ToString()}";
        }
    }
}