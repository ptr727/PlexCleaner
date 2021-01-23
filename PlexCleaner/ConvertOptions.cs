namespace PlexCleaner
{
    public class ConvertOptions
    {
        public bool EnableH265Encoder { get; set; } = true;
        public int VideoEncodeQuality { get; set; } = 20;
        public string AudioEncodeCodec { get; set; } = "ac3";
    }
}
