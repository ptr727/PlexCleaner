namespace PlexCleaner
{
    public class SubtitleInfo : TrackInfo
    {
        public SubtitleInfo() { }
        internal SubtitleInfo(MkvTool.TrackJson track) : base(track) { }
        internal SubtitleInfo(FfMpegTool.StreamJson stream) : base(stream) { }

        internal SubtitleInfo(MediaInfoTool.TrackXml track) : base(track)
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