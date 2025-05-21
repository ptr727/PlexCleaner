using System;
using Serilog;

namespace PlexCleaner;

public class AudioProps : TrackProps
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor")]
    public AudioProps(MediaTool.ToolType parser, string fileName)
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
                track.CodecName = track.CodecTagString;
            }
        }

        // Call base
        return base.Create(track);
    }

    public static AudioProps Create(string fileName, FfMpegToolJsonSchema.Track track)
    {
        AudioProps audioProps = new(MediaTool.ToolType.FfProbe, fileName);
        return audioProps.Create(track) ? audioProps : throw new NotSupportedException();
    }

    public static AudioProps Create(string fileName, MediaInfoToolXmlSchema.Track track)
    {
        AudioProps audioProps = new(MediaTool.ToolType.MediaInfo, fileName);
        return audioProps.Create(track) ? audioProps : throw new NotSupportedException();
    }

    public static AudioProps Create(string fileName, MkvToolJsonSchema.Track track)
    {
        AudioProps audioProps = new(MediaTool.ToolType.MkvMerge, fileName);
        return audioProps.Create(track) ? audioProps : throw new NotSupportedException();
    }
}
