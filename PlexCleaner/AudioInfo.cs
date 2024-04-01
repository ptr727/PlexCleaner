namespace PlexCleaner;

public class AudioInfo : TrackInfo
{
    public AudioInfo() { }
    public AudioInfo(MkvToolJsonSchema.Track track) : base(track) { }
    public AudioInfo(FfMpegToolJsonSchema.Stream stream) : base(stream) { }
    public AudioInfo(MediaInfoToolXmlSchema.Track track) : base(track) { }
}

// No need to override WriteLine()
