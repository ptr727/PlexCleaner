using System.ComponentModel.DataAnnotations;

namespace PlexCleaner;

public class ConvertOptions
{
    public bool EnableH265Encoder { get; set; }
    [Range(0, 51)]
    public int VideoEncodeQuality { get; set; }
    public string AudioEncodeCodec { get; set; } = "";

    public void SetDefaults()
    {
        EnableH265Encoder = true;
        VideoEncodeQuality = 20;
        AudioEncodeCodec = "ac3";
    }
}
