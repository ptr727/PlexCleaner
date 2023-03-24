using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace PlexCleaner;

public class VideoFormat
{
    public string Format;
    public string Codec;
    public string Profile;
}

// v1
[Obsolete]
public record ProcessOptions1
{
    [Obsolete]
    internal string ReEncodeVideoFormats { get; set; } = "";
    [Obsolete]
    internal string ReEncodeVideoCodecs { get; set; } = "";
    [Obsolete]
    internal string ReEncodeVideoProfiles { get; set; } = "";
    [Obsolete]
    internal string ReEncodeAudioFormats { get; set; } = "";
    [Obsolete]
    internal string KeepExtensions { get; set; } = "";
    [Obsolete]
    internal string KeepLanguages { get; set; } = "";
    [Obsolete]
    internal string PreferredAudioFormats { get; set; } = "";

    [Required]
    public bool DeleteEmptyFolders { get; set; }

    [Required]
    public bool DeleteUnwantedExtensions { get; set; }

    [Required]
    public bool ReMux { get; set; }

    [Required]
    public string ReMuxExtensions { get; set; } = "";

    [Required]
    public bool DeInterlace { get; set; }

    [Required]
    public bool ReEncode { get; set; }

    [Required]
    public bool SetUnknownLanguage { get; set; }

    [Required]
    public string DefaultLanguage { get; set; } = "";

    [Required]
    public bool RemoveUnwantedLanguageTracks { get; set; }

    [Required]
    public bool RemoveDuplicateTracks { get; set; }

    [Required]
    public bool RemoveTags { get; set; }

    [Required]
    public bool UseSidecarFiles { get; set; }

    [Required]
    public bool SidecarUpdateOnToolChange { get; set; }

    [Required]
    public bool Verify { get; set; }

    [Required]
    public bool RestoreFileTimestamp { get; set; }

    [Required]
    public HashSet<string> FileIgnoreList { get; protected set; } = new(StringComparer.OrdinalIgnoreCase);
}

// v2
[Obsolete]
public record ProcessOptions2 : ProcessOptions1
{
    public ProcessOptions2() { }

    // Copy from v1
    [Obsolete]
    public ProcessOptions2(ProcessOptions1 processOptions1) : base(processOptions1)
    {
        // Upgrade from v1
        Upgrade(processOptions1);
    }

    [Obsolete]
    protected void Upgrade(ProcessOptions1 processOptions1)
    {
        // Convert CSV to HashSet<string>
        if (!string.IsNullOrEmpty(processOptions1.KeepExtensions))
        {
            KeepExtensions.UnionWith(processOptions1.KeepExtensions.Split(','));
        }
        if (!string.IsNullOrEmpty(processOptions1.ReMuxExtensions))
        {
            ReMuxExtensions.UnionWith(processOptions1.ReMuxExtensions.Split(','));
        }
        if (!string.IsNullOrEmpty(processOptions1.ReEncodeAudioFormats))
        {
            ReEncodeAudioFormats.UnionWith(processOptions1.ReEncodeAudioFormats.Split(','));
        }
        if (!string.IsNullOrEmpty(processOptions1.KeepLanguages))
        {
            KeepLanguages.UnionWith(processOptions1.KeepLanguages.Split(','));
        }
        if (!string.IsNullOrEmpty(processOptions1.PreferredAudioFormats))
        {
            PreferredAudioFormats.UnionWith(processOptions1.PreferredAudioFormats.Split(','));
        }

        // Convert CSV to List<VideoFormat>
        if (!string.IsNullOrEmpty(processOptions1.ReEncodeVideoCodecs) &&
            !string.IsNullOrEmpty(processOptions1.ReEncodeVideoFormats) &&
            !string.IsNullOrEmpty(processOptions1.ReEncodeVideoProfiles))
        {
            var codecList = processOptions1.ReEncodeVideoCodecs.Split(',').ToList();
            var formatList = processOptions1.ReEncodeVideoFormats.Split(',').ToList();
            var profileList = processOptions1.ReEncodeVideoProfiles.Split(',').ToList();
            if (codecList.Count != formatList.Count || formatList.Count != profileList.Count)
            {
                // The number of arguments has to match
                throw new ArgumentException("ReEncodeVideo argument count mismatch");
            }

            for (int i = 0; i < codecList.Count; i++)
            {
                VideoFormat videoFormat = new()
                {
                    Codec = codecList.ElementAt(i),
                    Format = formatList.ElementAt(i),
                    Profile = profileList.ElementAt(i)
                };

                // Convert the * as wildcard to a null as any match
                if (videoFormat.Codec.Equals("*", StringComparison.OrdinalIgnoreCase))
                {
                    videoFormat.Codec = null;
                }
                if (videoFormat.Format.Equals("*", StringComparison.OrdinalIgnoreCase))
                {
                    videoFormat.Format = null;
                }
                if (videoFormat.Profile.Equals("*", StringComparison.OrdinalIgnoreCase))
                {
                    videoFormat.Profile = null;
                }

                ReEncodeVideo.Add(videoFormat);
            }
        }
    }

    [Required]
    public new HashSet<string> KeepExtensions { get; protected set; } = new(StringComparer.OrdinalIgnoreCase);

    [Required]
    public new HashSet<string> ReMuxExtensions { get; protected set; } = new(StringComparer.OrdinalIgnoreCase);

    [Required]
    public List<VideoFormat> ReEncodeVideo { get; protected set; } = new();

    [Required]
    public new HashSet<string> ReEncodeAudioFormats { get; protected set; } = new(StringComparer.OrdinalIgnoreCase);

    [Required]
    public new HashSet<string> KeepLanguages { get; protected set; } = new(StringComparer.OrdinalIgnoreCase);

    [Required]
    public new HashSet<string> PreferredAudioFormats { get; protected set; } = new(StringComparer.OrdinalIgnoreCase);

    [Required]
    public new HashSet<string> FileIgnoreList { get; protected set; } = new(StringComparer.OrdinalIgnoreCase);
}

// v3
#pragma warning disable CS0612 // Type or member is obsolete
public record ProcessOptions : ProcessOptions2
#pragma warning restore CS0612 // Type or member is obsolete
{
    public ProcessOptions() { }

    // Copy from v1
    [Obsolete]
    public ProcessOptions(ProcessOptions1 processOptions1) : base(processOptions1)
    {
        // Upgrade from v1
        Upgrade(processOptions1);
    }

    // Copy from v2
    [Obsolete]
    public ProcessOptions(ProcessOptions2 processOptions2) : base(processOptions2)
    {
        // Upgrade from v2
        Upgrade(processOptions2);
    }

    [Obsolete]
    protected void Upgrade(ProcessOptions2 processOptions2)
    {
        // Upgrade from v1
        Upgrade(processOptions2 as ProcessOptions1);

        // Default
        KeepOriginalLanguage = true;
        RemoveClosedCaptions = true;

        // Convert ISO 639-2 to RFC 5646 language tags
        if (!string.IsNullOrEmpty(DefaultLanguage))
        {
            // Not found, default to English
            DefaultLanguage = Language.GetIetfTag(DefaultLanguage, true) ?? Language.English;
        }
        List<string> oldList = KeepLanguages.ToList();
        KeepLanguages.Clear();
        oldList.ForEach(item => 
        {
            var ietfLanguage = Language.GetIetfTag(item, true);
            if (ietfLanguage != null)
            { 
                KeepLanguages.Add(ietfLanguage); 
            }
        });
    }

    [Required]
    public bool KeepOriginalLanguage { get; set; }

    [Required]
    public bool RemoveClosedCaptions { get; set; }

    public void SetDefaults()
    {
        DeleteEmptyFolders = true;
        DeleteUnwantedExtensions = true;
        ReMux = true;
        DeInterlace = true;
        ReEncode = true;
        SetUnknownLanguage = true;
        RemoveUnwantedLanguageTracks = true;
        RemoveTags = true;
        UseSidecarFiles = true;
        SidecarUpdateOnToolChange = false;
        Verify = true;
        RestoreFileTimestamp = false;
        RemoveDuplicateTracks = false;
        RemoveClosedCaptions = true;
        FileIgnoreList.Clear();
        DefaultLanguage = "en";
        KeepExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // TODO: Add UnRaid FUSE files e.g. .fuse_hidden191817c5000c5ee7, will need wildcard support
            ".partial~",
            ".nfo",
            ".jpg",
            ".srt",
            ".smi",
            ".ssa",
            ".ass",
            ".vtt"
        };
        ReMuxExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // TODO: Just a few, but many more
            ".avi",
            ".m2ts",
            ".ts",
            ".vob",
            ".mp4",
            ".m4v",
            ".asf",
            ".wmv"
        };
        ReEncodeVideo = new List<VideoFormat>
        {
            new() { Format = "mpeg2video" },
            new() { Format = "vc1" },
            new() { Format = "wmv3" },
            new() { Format = "msrle" },
            new() { Format = "rawvideo" },
            new() { Format = "indeo5" },
            new() { Format = "h264", Profile = "Constrained Baseline@30" },
            new() { Format = "mpeg4", Codec = "dx50" },
            new() { Format = "mpeg4", Codec = "xvid" },
            new() { Format = "msmpeg4v2", Codec = "mp42" },
            new() { Format = "msmpeg4v3", Codec = "div3" }
        };
        ReEncodeAudioFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "flac",
            "mp2",
            "vorbis",
            "wmapro",
            "opus",
            "wmav2",
            "adpcm_ms",
            "pcm_u8",
            "pcm_s16le"
        };
        KeepLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "en",
            "af",
            "zh-Hans",
            "in"
        };
        KeepOriginalLanguage = true;
        PreferredAudioFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "truehd atmos",
            "truehd",
            "dts-hd master audio",
            "dts-hd high resolution audio",
            "dts",
            "e-ac-3",
            "ac-3"
        };
    }
}
