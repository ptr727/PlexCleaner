namespace PlexCleaner
{
    public static partial class Info
    {
        public class AudioInfo : TrackInfo
        {
            public AudioInfo() { }
            internal AudioInfo(MkvTool.TrackJson track) : base(track) { }
            internal AudioInfo(FfMpegTool.StreamJson stream) : base(stream) { }
            internal AudioInfo(MediaInfoTool.TrackXml track) : base(track) { }

            public override string ToString()
            {
                return $"Audio : Format : {Format}, Codec : {Codec}, Language : {Language}, Id : {Id}, Number : {Number}";
            }
        }
    }
}