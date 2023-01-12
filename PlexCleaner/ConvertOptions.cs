using System;
using System.ComponentModel.DataAnnotations;

namespace PlexCleaner;

[Obsolete("Replaced in Schema v3", false)]
public class ConvertOptions1
{
    public bool EnableH265Encoder { get; set; }
    public int VideoEncodeQuality { get; set; }
    public string AudioEncodeCodec { get; set; } = "";
}

public class ConvertOptions
{
    // Inner classes do not allow same named properties as type due to type already existing
    // Remove "s" from options :(

    public ConvertOptions() { }

#pragma warning disable CS0618 // Type or member is obsolete
    public ConvertOptions(ConvertOptions1 convertOptions1)
#pragma warning restore CS0618 // Type or member is obsolete
    {
        // Convert discrete options to encode string options
        FfMpegOptions.Audio = convertOptions1.AudioEncodeCodec;
        FfMpegOptions.Video = $"{(convertOptions1.EnableH265Encoder ? "libx265" : "libx264")} -crf {convertOptions1.VideoEncodeQuality} -preset medium";
        
        HandBrakeOptions.Audio = $"copy --audio-fallback {convertOptions1.AudioEncodeCodec}";
        HandBrakeOptions.Video = $"{(convertOptions1.EnableH265Encoder ? "x265" : "x264")} --quality {convertOptions1.VideoEncodeQuality} --encoder-preset medium";
    }

    [Required]
    public FfMpegOptions FfMpegOptions = new();
    [Required]
    public HandBrakeOptions HandBrakeOptions = new();

    public void SetDefaults()
    {
        // Use default constructor defaults
        FfMpegOptions = new FfMpegOptions();
        HandBrakeOptions = new HandBrakeOptions();
    }
}

public class HandBrakeOptions
{
    [Required]
    // Do not include --encoder
    public string Video { get; set; } = "x264 --quality 20 --encoder-preset medium";
    [Required]
    // Do not include --aencoder
    public string Audio { get; set; } = "copy --audio-fallback ac3";
}

public class FfMpegOptions
{
    [Required]
    // Do not include -c:v
    public string Video { get; set; } = "libx264 -crf 20 -preset medium";
    [Required]
    // Do not include -c:a 
    public string Audio { get; set; } = "ac3";
    [Required]
    public string Global { get; set; } = "-analyzeduration 2147483647 -probesize 2147483647";
    [Required]
    public string Output { get; set; } = "-max_muxing_queue_size 1024 -abort_on empty_output";
}
