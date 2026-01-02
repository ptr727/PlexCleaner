using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sandbox;

public class TestJson
{
    public class MediaInfo
    {
        public Creatinglibrary CreatingLibrary { get; set; }
        public Media Media { get; set; }
    }

    public class Creatinglibrary
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Url { get; set; }
    }

    public class Media
    {
        public string Ref { get; set; }
        public Track[] Track { get; set; }
    }

    public class Track
    {
        public string Type { get; set; }
        public string UniqueId { get; set; }
        public string VideoCount { get; set; }
        public string AudioCount { get; set; }
        public string FileExtension { get; set; }
        public string Format { get; set; }
        public string FormatVersion { get; set; }
        public string FileSize { get; set; }
        public string Duration { get; set; }
        public string OverallBitRate { get; set; }
        public string FrameRate { get; set; }
        public string FrameCount { get; set; }
        public string StreamSize { get; set; }
        public string IsStreamable { get; set; }
        public string EncodedDate { get; set; }
        public string FileCreatedDate { get; set; }
        public string FileCreatedDateLocal { get; set; }
        public string FileModifiedDate { get; set; }
        public string FileModifiedDateLocal { get; set; }
        public string EncodedApplication { get; set; }
        public string EncodedApplicationName { get; set; }
        public string EncodedApplicationVersion { get; set; }
        public string EncodedLibrary { get; set; }
        public string StreamOrder { get; set; }
        public string Id { get; set; }
        public string FormatProfile { get; set; }
        public string FormatLevel { get; set; }
        public string FormatSettingsCabac { get; set; }
        public string FormatSettingsRefFrames { get; set; }
        public string CodecId { get; set; }
        public string BitRate { get; set; }
        public string Width { get; set; }
        public string Height { get; set; }
        public string SampledWidth { get; set; }
        public string SampledHeight { get; set; }
        public string PixelAspectRatio { get; set; }
        public string DisplayAspectRatio { get; set; }
        public string FrameRateMode { get; set; }
        public string FrameRateModeOriginal { get; set; }
        public string FrameRateNum { get; set; }
        public string FrameRateDen { get; set; }
        public string ColorSpace { get; set; }
        public string ChromaSubsampling { get; set; }
        public string BitDepth { get; set; }
        public string ScanType { get; set; }
        public string Delay { get; set; }
        public string DelaySource { get; set; }
        public string EncodedLibraryName { get; set; }
        public string EncodedLibraryVersion { get; set; }
        public string EncodedLibrarySettings { get; set; }
        public string Language { get; set; }
        public string Default { get; set; }
        public string Forced { get; set; }
        public string ColourDescriptionPresent { get; set; }
        public string ColourDescriptionPresentSource { get; set; }
        public string ColourRange { get; set; }
        public string ColourRangeSource { get; set; }
        public string FormatCommercialIfAny { get; set; }
        public string FormatSettingsEndianness { get; set; }
        public string BitRateMode { get; set; }
        public string Channels { get; set; }
        public string ChannelPositions { get; set; }
        public string ChannelLayout { get; set; }
        public string SamplesPerFrame { get; set; }
        public string SamplingRate { get; set; }
        public string SamplingCount { get; set; }
        public string CompressionMode { get; set; }
        public string VideoDelay { get; set; }
        public string ServiceKind { get; set; }
        public Extra Extra { get; set; }
    }

    public class Extra
    {
        public string Bsid { get; set; }
        public string Dialnorm { get; set; }
        public string Acmod { get; set; }
        public string Lfeon { get; set; }
        public string Cmixlev { get; set; }
        public string Surmixlev { get; set; }
        public string DialnormAverage { get; set; }
        public string DialnormMinimum { get; set; }
    }
}

[JsonSourceGenerationOptions(
    AllowTrailingCommas = true,
    IncludeFields = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
    ReadCommentHandling = JsonCommentHandling.Skip
)]
[JsonSerializable(typeof(TestJson.MediaInfo), TypeInfoPropertyName = "TestJsonRootobject")]
public partial class TestJsonContext : JsonSerializerContext;
