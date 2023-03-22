using System;
using System.ComponentModel.DataAnnotations;

namespace PlexCleaner;

// v1, v2
public record ConvertOptions1
{
    [Obsolete]
    public bool EnableH265Encoder { get; set; }
    [Obsolete]
    public int VideoEncodeQuality { get; set; }
    [Obsolete]
    public string AudioEncodeCodec { get; set; } = "";
}

// v3
public record ConvertOptions : ConvertOptions1
{
    public ConvertOptions() { }

    [Obsolete]
    public ConvertOptions(ConvertOptions1 convertOptions1) : base(convertOptions1)
    {
        // Upgrade
        Upgrade(convertOptions1);
    }

    [Obsolete]
    protected void Upgrade(ConvertOptions1 convertOptions1)
    {
        // Convert discrete options to encode string options
        FfMpegOptions.Audio = convertOptions1.AudioEncodeCodec;
        FfMpegOptions.Video = $"{(convertOptions1.EnableH265Encoder ? "libx265" : "libx264")} -crf {convertOptions1.VideoEncodeQuality} -preset medium";

        HandBrakeOptions.Audio = $"copy --audio-fallback {convertOptions1.AudioEncodeCodec}";
        HandBrakeOptions.Video = $"{(convertOptions1.EnableH265Encoder ? "x265" : "x264")} --quality {convertOptions1.VideoEncodeQuality} --encoder-preset medium";
    }

    [Required]
    public FfMpegOptions FfMpegOptions { get; protected set; } = new();
    
    [Required]
    public HandBrakeOptions HandBrakeOptions { get; protected set; } = new();

    public void SetDefaults()
    {
        FfMpegOptions.Video = "libx264 -crf 20 -preset medium";
        FfMpegOptions.Audio = "ac3";
        FfMpegOptions.Global = "-analyzeduration 2147483647 -probesize 2147483647";
        FfMpegOptions.Output = "-max_muxing_queue_size 1024 -abort_on empty_output";

        HandBrakeOptions.Video = "x264 --quality 20 --encoder-preset medium";
        HandBrakeOptions.Audio = "copy --audio-fallback ac3";
    }
}

public class HandBrakeOptions
{
    [Required]
    // Do not include --encoder
    public string Video { get; set; } = "";

    [Required]
    // Do not include --aencoder
    public string Audio { get; set; } = "";
}

public class FfMpegOptions
{
    [Required]
    // Do not include -c:v
    public string Video { get; set; } = "";

    [Required]
    // Do not include -c:a 
    public string Audio { get; set; } = "";

    [Required]
    public string Global { get; set; } = "";
    
    [Required]
    public string Output { get; set; } = "";
}
