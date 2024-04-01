using System;
using System.ComponentModel.DataAnnotations;
using System.Reflection.Metadata.Ecma335;
using Serilog;

namespace PlexCleaner;

// v2 : Added
public record HandBrakeOptions
{
    [Required]
    // Do not include --encoder
    public string Video { get; set; } = "";

    [Required]
    // Do not include --aencoder
    public string Audio { get; set; } = "";
}

// v2 : Added
public record FfMpegOptions
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

// v1
public record ConvertOptions1
{
    protected const int Version = 1;

    public ConvertOptions1() { }

    // v2 : Replaced with FfMpegOptions and HandBrakeOptions
    [Obsolete]
    public bool EnableH265Encoder { internal get; set; }
    [Obsolete]
    public int VideoEncodeQuality { internal get; set; }
    [Obsolete]
    public string AudioEncodeCodec { internal get; set; } = "";
}

// v2
public record ConvertOptions2 : ConvertOptions1
{
    protected new const int Version = 2;

    public ConvertOptions2() { }
    public ConvertOptions2(ConvertOptions1 convertOptions1) : base(convertOptions1)
    { 
        Upgrade(ConvertOptions1.Version);
    }

    // v2 : Added
    [Required]
    public FfMpegOptions FfMpegOptions { get; set; } = new();

    // v2 : Added
    [Required]
    public HandBrakeOptions HandBrakeOptions { get; set; } = new();

#pragma warning disable CS0612 // Type or member is obsolete
    private void Upgrade(int version)
    {
        // v1
        if (version == ConvertOptions1.Version)
        {
            // Get v1 schema
            ConvertOptions1 convertOptions1 = this;

            // v1 -> v2 : Replaced with FfMpegOptions and HandBrakeOptions

            // Convert discrete options to encode string options
            FfMpegOptions.Audio = convertOptions1.AudioEncodeCodec;
            FfMpegOptions.Video = $"{(convertOptions1.EnableH265Encoder ? "libx265" : "libx264")} -crf {convertOptions1.VideoEncodeQuality} -preset medium";

            HandBrakeOptions.Audio = $"copy --audio-fallback {convertOptions1.AudioEncodeCodec}";
            HandBrakeOptions.Video = $"{(convertOptions1.EnableH265Encoder ? "x265" : "x264")} --quality {convertOptions1.VideoEncodeQuality} --encoder-preset medium";
        }

        // v2
    }
#pragma warning restore CS0612 // Type or member is obsolete

    public void SetDefaults()
    {
        FfMpegOptions.Video = "libx264 -crf 22 -preset medium";
        FfMpegOptions.Audio = "ac3";
        FfMpegOptions.Global = "-analyzeduration 2147483647 -probesize 2147483647";
        FfMpegOptions.Output = "-max_muxing_queue_size 1024 -abort_on empty_output";

        HandBrakeOptions.Video = "x264 --quality 22 --encoder-preset medium";
        HandBrakeOptions.Audio = "copy --audio-fallback ac3";
    }

    public bool VerifyValues()
    {
        // All values must be set
        if (string.IsNullOrEmpty(FfMpegOptions.Video) ||
            string.IsNullOrEmpty(FfMpegOptions.Audio) ||
            string.IsNullOrEmpty(FfMpegOptions.Global) ||
            string.IsNullOrEmpty(FfMpegOptions.Output))
        {
            Log.Logger.Error("ConvertOptions:FfMpegOptions all values must be set");
            return false;
        }
        if (string.IsNullOrEmpty(HandBrakeOptions.Video) ||
            string.IsNullOrEmpty(HandBrakeOptions.Audio))
        {
            Log.Logger.Error("ConvertOptions:HandBrakeOptions all values must be set");
            return false;
        }

        // Required parameters
        if (!FfMpegOptions.Output.Contains("-abort_on empty_output"))
        {
            Log.Logger.Error("ConvertOptions:FfMpegOptions.Output must contain '-abort_on empty_output'");
            return false;
        }

        return true;
    }
}
