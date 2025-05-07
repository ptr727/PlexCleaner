using System;
using Serilog;

namespace PlexCleaner;

public class SubtitleInfo : TrackInfo
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor")]
    public SubtitleInfo(MediaTool.ToolType parser, string fileName)
        : base(parser, fileName) { }

    public override bool Create(FfMpegToolJsonSchema.Track track)
    {
        // Fixup before calling base
        if (string.IsNullOrEmpty(track.CodecName) || string.IsNullOrEmpty(track.CodecLongName))
        {
            // Some subtitle codecs are not supported by FFmpeg, e.g. S_TEXT / WEBVTT, but are supported by MKVToolNix
            Log.Warning(
                "FfMpegToolJsonSchema : Overriding unknown subtitle codec : Format: {Format}, Codec: {Codec} : {FileName}",
                track.CodecLongName,
                track.CodecName,
                FileName
            );
            if (string.IsNullOrEmpty(track.CodecName))
            {
                track.CodecName = "unknown";
            }
            if (string.IsNullOrEmpty(track.CodecLongName))
            {
                track.CodecLongName = "unknown";
            }
        }

        // Call base
        return base.Create(track);
    }

    public override bool Create(MediaInfoToolXmlSchema.Track track)
    {
        // Call base first
        if (!base.Create(track))
        {
            return false;
        }

        // We need MuxingMode to be set for VOBSUB
        // https://forums.plex.tv/discussion/290723/long-wait-time-before-playing-some-content-player-says-directplay-server-says-transcoding
        // https://gitlab.com/mbunkus/mkvtoolnix/-/issues/2131
        if (
            track.CodecId.Equals("S_VOBSUB", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrEmpty(track.MuxingMode)
        )
        {
            // Set track error and recommend remove, remux does not fix this error
            HasErrors = true;
            State = StateType.Remove;
            Log.Warning(
                "MediaInfoToolXmlSchema : MuxingMode not specified for S_VOBSUB Codec : State: {State} : {FileName}",
                State,
                FileName
            );
        }

        return true;
    }

    public static SubtitleInfo Create(string fileName, FfMpegToolJsonSchema.Track track)
    {
        SubtitleInfo subtitleInfo = new(MediaTool.ToolType.FfProbe, fileName);
        return subtitleInfo.Create(track) ? subtitleInfo : throw new NotSupportedException();
    }

    public static SubtitleInfo Create(string fileName, MediaInfoToolXmlSchema.Track track)
    {
        SubtitleInfo subtitleInfo = new(MediaTool.ToolType.MediaInfo, fileName);
        return subtitleInfo.Create(track) ? subtitleInfo : throw new NotSupportedException();
    }

    public static SubtitleInfo Create(string fileName, MkvToolJsonSchema.Track track)
    {
        SubtitleInfo subtitleInfo = new(MediaTool.ToolType.MkvMerge, fileName);
        return subtitleInfo.Create(track) ? subtitleInfo : throw new NotSupportedException();
    }
}
