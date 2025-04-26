namespace PlexCleaner;

public class AudioInfo : TrackInfo
{
    public AudioInfo() { }

    public AudioInfo(MkvToolJsonSchema.Track track)
        : base(track) { }

    public AudioInfo(FfMpegToolJsonSchema.Track track)
        : base(track) { }

    public AudioInfo(MediaInfoToolXmlSchema.Track track)
        : base(track) { }
}
