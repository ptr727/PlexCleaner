using System;
using Serilog;

namespace PlexCleaner;

public class AudioInfo : TrackInfo
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor")]
    public AudioInfo(MediaTool.ToolType parser, string fileName)
        : base(parser, fileName) { }

    public override bool Create(FfMpegToolJsonSchema.Track track)
    {
        // Fixup before calling base
        if (string.IsNullOrEmpty(track.CodecName) || string.IsNullOrEmpty(track.CodecLongName))
        {
            // DRM tracks, e.g. QuickTime audio report no codec information
            // "codec_tag_string": "enca"
            // "codec_tag": "0x61636e65"
            if (!string.IsNullOrEmpty(track.CodecTagString))
            {
                Log.Warning(
                    "FfMpegToolJsonSchema : Overriding unknown audio codec : Format: {Format}, Codec: {Codec}, CodecTagString: {CodecTagString} : {FileName}",
                    track.CodecLongName,
                    track.CodecName,
                    track.CodecTagString,
                    FileName
                );
                track.CodecLongName = track.CodecTagString;
            }
        }

        // Call base
        return base.Create(track);
    }

    public static AudioInfo Create(string fileName, FfMpegToolJsonSchema.Track track)
    {
        AudioInfo audioInfo = new(MediaTool.ToolType.FfProbe, fileName);
        return audioInfo.Create(track) ? audioInfo : throw new NotSupportedException();
    }

    public static AudioInfo Create(string fileName, MediaInfoToolXmlSchema.Track track)
    {
        AudioInfo audioInfo = new(MediaTool.ToolType.MediaInfo, fileName);
        return audioInfo.Create(track) ? audioInfo : throw new NotSupportedException();
    }

    public static AudioInfo Create(string fileName, MkvToolJsonSchema.Track track)
    {
        AudioInfo audioInfo = new(MediaTool.ToolType.MkvMerge, fileName);
        return audioInfo.Create(track) ? audioInfo : throw new NotSupportedException();
    }
}
