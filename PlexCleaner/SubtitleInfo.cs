namespace PlexCleaner
{
    public class SubtitleInfo : TrackInfo
    {
        public SubtitleInfo() { }
        internal SubtitleInfo(MkvToolJsonSchema.Track track) : base(track) 
        { 
            // Forced missing
            // MuxingMode missing
        }
        internal SubtitleInfo(FfMpegToolJsonSchema.Stream stream) : base(stream) 
        {
            // MuxingMode missing

            Forced = stream.Disposition.Forced;
        }

        internal SubtitleInfo(MediaInfoToolXmlSchema.Track track) : base(track)
        {
            MuxingMode = track.MuxingMode;
            Forced = track.Forced;
        }

        public string MuxingMode { get; set; } = "";
        public bool Forced { get; set; } = false;

        public override string ToString()
        {
            return $"Subtitle : MuxingMode : {MuxingMode}, Forced : {Forced}, {base.ToString()}";
        }
    }
}