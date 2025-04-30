using System;
using System.Text.Json.Serialization;
using Serilog;

namespace PlexCleaner;

// v2 : Added
public record HandBrakeOptions
{
    [JsonRequired]
    public string Video { get; set; } = "";

    [JsonRequired]
    public string Audio { get; set; } = "";
}

// v2 : Added
public record FfMpegOptions
{
    [JsonRequired]
    public string Video { get; set; } = "";

    [JsonRequired]
    public string Audio { get; set; } = "";

    // v3 : Value no longer needs defaults
    [JsonRequired]
    public string Global { get; set; } = "";

    // v3 : Removed
    [Obsolete("Removed in v3")]
    [Json.Schema.Generation.JsonExclude]
    public string Output { get; set; } = "";
}

// v1
public record ConvertOptions1
{
    protected const int Version = 1;

    // v2 : Replaced with FfMpegOptions and HandBrakeOptions
    [Obsolete("Replaced in v2 with FfMpegOptions and HandBrakeOptions")]
    [Json.Schema.Generation.JsonExclude]
    public bool EnableH265Encoder { get; set; }

    [Obsolete("Replaced in v2 with FfMpegOptions and HandBrakeOptions")]
    [Json.Schema.Generation.JsonExclude]
    public int VideoEncodeQuality { get; set; }

    [Obsolete("Replaced in v2 with FfMpegOptions and HandBrakeOptions")]
    [Json.Schema.Generation.JsonExclude]
    public string AudioEncodeCodec { get; set; } = "";
}

// v2
public record ConvertOptions2 : ConvertOptions1
{
    protected new const int Version = 2;

    public ConvertOptions2() { }

    public ConvertOptions2(ConvertOptions1 convertOptions1)
        : base(convertOptions1) { }

    // v2 : Added
    [JsonRequired]
    public FfMpegOptions FfMpegOptions { get; set; } = new();

    // v2 : Added
    [JsonRequired]
    public HandBrakeOptions HandBrakeOptions { get; set; } = new();
}

// v3
public record ConvertOptions3 : ConvertOptions2
{
    protected new const int Version = 3;

    public ConvertOptions3() { }

    public ConvertOptions3(ConvertOptions1 convertOptions1)
        : base(convertOptions1) => Upgrade(ConvertOptions1.Version);

    public ConvertOptions3(ConvertOptions2 convertOptions2)
        : base(convertOptions2) => Upgrade(ConvertOptions2.Version);

    // v3 : Removed FfMpegOptions.Output
    // v3 : Removed defaults from FfMpegOptions.Global

    private void Upgrade(int version)
    {
        // v1
        if (version == ConvertOptions1.Version)
        {
            // Get v1 schema
            ConvertOptions1 convertOptions1 = this;

            // v1 -> v2 : Replaced with FfMpegOptions and HandBrakeOptions

            // Convert discrete options to encode string options
#pragma warning disable CS0618 // Type or member is obsolete
            FfMpegOptions.Audio = convertOptions1.AudioEncodeCodec;
            FfMpegOptions.Video =
                $"{(convertOptions1.EnableH265Encoder ? "libx265" : "libx264")} -crf {convertOptions1.VideoEncodeQuality} -preset medium";

            HandBrakeOptions.Audio = $"copy --audio-fallback {convertOptions1.AudioEncodeCodec}";
            HandBrakeOptions.Video =
                $"{(convertOptions1.EnableH265Encoder ? "x265" : "x264")} --quality {convertOptions1.VideoEncodeQuality} --encoder-preset medium";
#pragma warning restore CS0618 // Type or member is obsolete
        }

        // v2
        if (version == ConvertOptions2.Version)
        {
            // Get v2 schema
            ConvertOptions2 convertOptions2 = this;

            // v2 -> v3 :
            // Removed FfMpegOptions.Output
            // Removed defaults from FfMpegOptions.Global

            // Obsolete
#pragma warning disable CS0618 // Type or member is obsolete
            convertOptions2.FfMpegOptions.Output = "";
#pragma warning restore CS0618 // Type or member is obsolete

            // Remove old default global options
            FfMpegOptions.Global = FfMpegOptions
                .Global.Replace("-analyzeduration 2147483647 -probesize 2147483647", null)
                .Trim();
        }

        // v3
    }

    public void SetDefaults()
    {
        FfMpegOptions.Video = FfMpegTool.DefaultVideoOptions;
        FfMpegOptions.Audio = FfMpegTool.DefaultAudioOptions;
        FfMpegOptions.Global = "";

        HandBrakeOptions.Video = HandBrakeTool.DefaultVideoOptions;
        HandBrakeOptions.Audio = HandBrakeTool.DefaultAudioOptions;
    }

    public bool VerifyValues()
    {
        // Values must be set
        if (string.IsNullOrEmpty(FfMpegOptions.Video) || string.IsNullOrEmpty(FfMpegOptions.Audio))
        {
            Log.Error("ConvertOptions:FfMpegOptions Video and Audio values must be set");
            return false;
        }
        if (
            string.IsNullOrEmpty(HandBrakeOptions.Video)
            || string.IsNullOrEmpty(HandBrakeOptions.Audio)
        )
        {
            Log.Error("ConvertOptions:HandBrakeOptions Video and Audio values must be set");
            return false;
        }

        return true;
    }
}
