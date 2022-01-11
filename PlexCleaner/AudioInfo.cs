namespace PlexCleaner;

public class AudioInfo : TrackInfo
{
    public AudioInfo() { }
    internal AudioInfo(MkvToolJsonSchema.Track track) : base(track) { }
    internal AudioInfo(FfMpegToolJsonSchema.Stream stream) : base(stream) { }
    internal AudioInfo(MediaInfoToolXmlSchema.Track track) : base(track) { }
}

// No need to override WriteLine()