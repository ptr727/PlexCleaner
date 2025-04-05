using System;
using Serilog;

namespace PlexCleaner;

public class SubtitleInfo : TrackInfo
{
    public SubtitleInfo(MkvToolJsonSchema.Track track) : base(track) { }
    public SubtitleInfo(FfMpegToolJsonSchema.Track track) : base(track) { }

    public SubtitleInfo(MediaInfoToolXmlSchema.Track track) : base(track)
    {
        // We need MuxingMode for VOBSUB else Plex on Nvidia Shield TV will hang on play start
        // https://forums.plex.tv/discussion/290723/long-wait-time-before-playing-some-content-player-says-directplay-server-says-transcoding
        // https://github.com/mbunkus/mkvtoolnix/issues/2131
        // https://gitlab.com/mbunkus/mkvtoolnix/-/issues/2131
        if (track.CodecId.Equals("S_VOBSUB", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrEmpty(track.MuxingMode))
        {
            // Set track error and recommend remove, remux does not fix this error
            HasErrors = true;
            State = StateType.Remove;
            Log.Logger.Warning("MediaInfoToolXmlSchema : MuxingMode not specified for S_VOBSUB Codec : State: {State}", State);
        }
    }
}
