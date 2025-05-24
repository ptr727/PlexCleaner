#region

using System;
using System.Globalization;
using Serilog;

#endregion

namespace PlexCleaner;

public class SubtitleProps(MediaProps mediaProps) : TrackProps(TrackType.Subtitle, mediaProps)
{
    // Required
    // Format = track.Codec;
    // Codec = track.Properties.CodecId;
    public override bool Create(MkvToolJsonSchema.Track track) => base.Create(track);

    // Required
    // Format = track.CodecName;
    // Codec = track.CodecLongName;
    public override bool Create(FfMpegToolJsonSchema.Track track) => base.Create(track);

    // Required
    // Format = track.Format;
    // Codec = track.CodecId;
    public override bool Create(MediaInfoToolXmlSchema.Track track)
    {
        // Handle closed captions
        if (!HandleClosedCaptions(track))
        {
            return false;
        }

        // Call base
        if (!base.Create(track))
        {
            return false;
        }

        // We need MuxingMode to be set for VOBSUB
        // https://forums.plex.tv/discussion/290723/long-wait-time-before-playing-some-content-player-says-directplay-server-says-transcoding
        if (
            track.CodecId.Equals("S_VOBSUB", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrEmpty(track.MuxingMode)
        )
        {
            // Set track error and recommend remove, remux does not fix this error
            HasErrors = true;
            State = StateType.Remove;
            Log.Warning(
                "{Parser} : {Type} : MuxingMode not specified for S_VOBSUB Codec : State: {State} : {FileName}",
                Parent.Parser,
                Type,
                State,
                Parent.FileName
            );
        }

        return true;
    }

    private bool HandleClosedCaptions(MediaInfoToolXmlSchema.Track track)
    {
        // Handle closed caption tracks presented as subtitle tracks
        // return false to abort normal processing

        // <track type="Video" typeorder="4">
        //     <ID>256</ID>
        //     <Format>MPEG Video</Format>
        // <track type="Text" typeorder="1">
        //     <ID>256-CC1</ID>
        //     <Format>EIA-608</Format>
        //     <MuxingMode>A/53 / DTVCC Transport</MuxingMode>
        if (
            !track.Format.Equals("EIA-608", StringComparison.OrdinalIgnoreCase)
            && !track.Format.Equals("EIA-708", StringComparison.OrdinalIgnoreCase)
        )
        {
            // Not CC track
            return true;
        }

        // Parse the track number
        if (!MediaInfo.Tool.ParseSubTrack(track.Id, out long trackId))
        {
            Log.Error(
                "{Parser} : {Type} : Failed to parse closed caption sub-track number : Id: {Id}, Container: {Container} : {FileName}",
                Parent.Parser,
                Type,
                track.Id,
                Parent.Container,
                Parent.FileName
            );
            return false;
        }

        // Set codec to muxing mode
        if (string.IsNullOrEmpty(track.CodecId))
        {
            track.CodecId = track.MuxingMode;
        }

        // Set normalized track id
        string originalId = track.Id;
        track.Id = trackId.ToString(CultureInfo.InvariantCulture);

        // SCTE 128 / DTVCC Transport : Separate stream
        // A/53 / DTVCC Transport / MXF : Embedded in video stream
        // A/53 / DTVCC Transport : Embedded in video stream
        // https://github.com/MediaArea/MediaInfoLib/issues/2307

        // Separate stream
        if (track.MuxingMode.Contains("SCTE 128", StringComparison.OrdinalIgnoreCase))
        {
            // Not a CC track
            return true;
        }

        // If not embedded in video A/53 what is it?
        if (!track.MuxingMode.Contains("A/53", StringComparison.OrdinalIgnoreCase))
        {
            // Final Cut ?
            Log.Warning(
                "{Parser} : {Type} : Unknown closed caption format : Format: {Format}, MuxingMode: {MuxingMode}, Container: {Container} : {FileName}",
                Parent.Parser,
                Type,
                track.Format,
                track.MuxingMode,
                Parent.Container,
                Parent.FileName
            );

            // Not a CC track
            return true;
        }

        // Find the matching video track
        if (Parent.Video.Find(item => item.Number == trackId) is not { } videoTrack)
        {
            // Could not find matching video track
            Log.Error(
                "{Parser} : {Type} : Failed to find video track associated with A/53 closed caption subtitle track : Id: {Id}, Container: {Container} : {FileName}",
                Parent.Parser,
                Type,
                originalId,
                Parent.Container,
                Parent.FileName
            );
            return false;
        }

        // Set the closed caption flag
        Log.Information(
            "{Parser} : {Type} : Setting closed caption flag on video track from A/53 subtitle track : Format: {Format}, MuxingMode: {MuxingMode}, Subtitle Id: {SubtitleId}, Video Id: {VideoId}, Container: {Container} : {FileName}",
            Parent.Parser,
            Type,
            track.Format,
            track.MuxingMode,
            originalId,
            videoTrack.Number,
            Parent.Container,
            Parent.FileName
        );
        videoTrack.ClosedCaptions = true;

        // Handled
        return false;
    }
}
