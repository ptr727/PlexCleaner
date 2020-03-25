namespace PlexCleaner
{
    public class SubtitleInfo : TrackInfo
    {
        public SubtitleInfo() { }
        public SubtitleInfo(MkvTool.TrackJson track) : base(track) { }
        public SubtitleInfo(FfMpegTool.StreamJson stream) : base(stream) { }

        public SubtitleInfo(MediaInfoTool.TrackXml track) : base(track)
        {
            MuxingMode = track.MuxingMode;
        }

        public string MuxingMode { get; set; }

        public override string ToString()
        {
            return $"Subtitle : Format : {Format}, Codec : {Codec}, MuxingMode : {MuxingMode}, Language : {Language}, Id : {Id}, Number : {Number}";
        }
    }
}