namespace PlexCleaner
{
    public class AudioInfo : TrackInfo
    {
        public AudioInfo() { }
        public AudioInfo(MkvTool.TrackJson track) : base(track) { }
        public AudioInfo(FfMpegTool.StreamJson stream) : base(stream) { }
        public AudioInfo(MediaInfoTool.TrackXml track) : base(track) { }

        public override string ToString()
        {
            return $"Audio : Format : {Format}, Codec : {Codec}, Language : {Language}, Id : {Id}, Number : {Number}";
        }
    }
}