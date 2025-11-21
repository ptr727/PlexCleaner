namespace PlexCleaner;

public class AudioProps(MediaProps mediaProps) : TrackProps(TrackType.Audio, mediaProps)
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
    public override bool Create(MediaInfoToolXmlSchema.Track track) => base.Create(track);
}
