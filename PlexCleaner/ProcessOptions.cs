using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog;

namespace PlexCleaner;

// v2 : Added
public record VideoFormat
{
    public string Format;
    public string Codec;
    public string Profile;
}

// v1
public record ProcessOptions1
{
    protected const int Version = 1;

    public ProcessOptions1() { }

    // v2 : Removed
    // v1 -> v2 : CSV -> List<VideoFormat::Format>
    [Obsolete]
    public string ReEncodeVideoFormats { internal get; set; } = "";

    // v2 : Removed
    // v1 -> v2 : CSV -> List<VideoFormat::Codec>
    [Obsolete]
    public string ReEncodeVideoCodecs { internal get; set; } = "";

    // v2 : Removed
    // v1 -> v2 : CSV -> List<VideoFormat::Profile>
    [Obsolete]
    public string ReEncodeVideoProfiles { internal get; set; } = "";

    // v2 : Removed
    // v1 -> v2 : CSV -> HashSet<string>
    [Obsolete]
    public string ReEncodeAudioFormats { internal get; set; } = "";

    // v2 : Removed
    // v1 -> v2 : CSV -> HashSet<string>
    [Obsolete]
    public string ReMuxExtensions { internal get; set; } = "";

    // v2 : Removed
    // v1 -> v2 : CSV -> HashSet<string>
    [Obsolete]
    public string KeepExtensions { internal get; set; } = "";

    // v2 : Removed
    // v1 -> v2 : CSV -> HashSet<string>
    [Obsolete]
    public string KeepLanguages { internal get; set; } = "";

    // v2 : Removed
    // v1 -> v2 : CSV -> HashSet<string>
    [Obsolete]
    public string PreferredAudioFormats { internal get; set; } = "";

    [Required]
    public bool DeleteEmptyFolders { get; set; }

    [Required]
    public bool DeleteUnwantedExtensions { get; set; }

    [Required]
    public bool ReMux { get; set; }

    [Required]
    public bool DeInterlace { get; set; }

    [Required]
    public bool ReEncode { get; set; }

    [Required]
    public bool SetUnknownLanguage { get; set; }

    // v3 : Changed ISO 639-2 to RFC 5646 language tags
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
    public HashSet<string> FileIgnoreList { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

// v2
public record ProcessOptions2 : ProcessOptions1
{
    protected new const int Version = 2;

    public ProcessOptions2() { }
    public ProcessOptions2(ProcessOptions1 processOptions1) : base(processOptions1) { }

    // v2 : Added
    // v1 -> v2 : CSV -> HashSet<string>
    [Obsolete]
    public new HashSet<string> KeepExtensions { internal get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // v2 : Added
    // v1 -> v2 : CSV -> HashSet<string>
    [Required]
    public new HashSet<string> ReMuxExtensions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // v2 : Added
    // v1 -> v2 : CSV -> List<VideoFormat>
    [Required]
    public List<VideoFormat> ReEncodeVideo { get; set; } = [];

    // v2 : Added
    // v1 -> v2 : CSV -> HashSet<string>
    [Required]
    public new HashSet<string> ReEncodeAudioFormats { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // v3 : Changed ISO 639-2 to RFC 5646 language tags
    // v2 : Added
    // v1 -> v2 : CSV -> HashSet<string>
    [Required]
    public new HashSet<string> KeepLanguages { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // v2 : Added
    // v1 -> v2 : CSV -> HashSet<string>
    [Required]
    public new HashSet<string> PreferredAudioFormats { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

// v3
public record ProcessOptions3 : ProcessOptions2
{
    protected new const int Version = 3;

    public ProcessOptions3() { }
    public ProcessOptions3(ProcessOptions1 processOptions1) : base(processOptions1) { }
    public ProcessOptions3(ProcessOptions2 processOptions2) : base(processOptions2) { }

    // v3 : Added
    [Required]
    public bool KeepOriginalLanguage { get; set; }

    // v3 : Added
    [Required]
    public bool RemoveClosedCaptions { get; set; }

    // v3 : Added
    [Required]
    public bool SetIetfLanguageTags { get; set; }

    // v3 : Added
    [Required]
    public bool SetTrackFlags { get; set; }
}

// v4
public record ProcessOptions4 : ProcessOptions3
{
    protected new const int Version = 4;

    public ProcessOptions4() { }
    public ProcessOptions4(ProcessOptions1 processOptions1) : base(processOptions1) 
    {
        Upgrade(ProcessOptions1.Version);
    }
    public ProcessOptions4(ProcessOptions2 processOptions2) : base(processOptions2)
    {
        Upgrade(ProcessOptions2.Version);
    }
    public ProcessOptions4(ProcessOptions3 processOptions3) : base(processOptions3)
    {
        Upgrade(ProcessOptions3.Version);
    }

    // v4 : Added
    [Required]
    public HashSet<string> FileIgnoreMasks { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    private List<Regex> FileIgnoreRegExList = [];

    private void FileIgnoreMaskToRegex()
    {
        foreach (var item in FileIgnoreMasks)
        {
            FileIgnoreRegExList.Add(MaskToRegex(item));
        }
    }

    public static Regex MaskToRegex(string mask)
    {
        // Convert * and ? wildcards to regex
        var regexString = "^" + Regex.Escape(mask);
        regexString = regexString.Replace("\\*", ".*");
        regexString = regexString.Replace("\\?", ".") + "$";
        return new Regex(regexString, RegexOptions.IgnoreCase);
    }

    public bool IsFileIgnoreMatch(string fileName)
    {
        return FileIgnoreRegExList.Any(item => item.IsMatch(fileName));
    }

#pragma warning disable CS0612 // Type or member is obsolete
    protected void Upgrade(int version)
    {
        // v1
        if (version <= ProcessOptions1.Version)
        {
            // Get v1 schema
            ProcessOptions1 processOptions1 = this;

            // v1 -> v2 : Convert CSV to HashSet<string>
            if (!string.IsNullOrEmpty(processOptions1.KeepExtensions))
            {
                KeepExtensions.UnionWith(processOptions1.KeepExtensions.Split(','));
                processOptions1.KeepExtensions = null;
            }
            if (!string.IsNullOrEmpty(processOptions1.ReMuxExtensions))
            {
                ReMuxExtensions.UnionWith(processOptions1.ReMuxExtensions.Split(','));
                processOptions1.ReMuxExtensions = null;
            }
            if (!string.IsNullOrEmpty(processOptions1.ReEncodeAudioFormats))
            {
                ReEncodeAudioFormats.UnionWith(processOptions1.ReEncodeAudioFormats.Split(','));
                processOptions1.ReEncodeAudioFormats = null;
            }
            if (!string.IsNullOrEmpty(processOptions1.KeepLanguages))
            {
                KeepLanguages.UnionWith(processOptions1.KeepLanguages.Split(','));
                processOptions1.KeepLanguages = null;
            }
            if (!string.IsNullOrEmpty(processOptions1.PreferredAudioFormats))
            {
                PreferredAudioFormats.UnionWith(processOptions1.PreferredAudioFormats.Split(','));
                processOptions1.PreferredAudioFormats = null;
            }

            // v1 -> v2 : Convert CSV to List<VideoFormat>
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
            processOptions1.ReEncodeVideoCodecs = null;
            processOptions1.ReEncodeVideoFormats = null;
            processOptions1.ReEncodeVideoProfiles = null;
        }

        // v2
        if (version <= ProcessOptions2.Version)
        {
            // Get v2 schema
            // ProcessOptions2 processOptions2 = this;

            // v2 -> v3 : Convert ISO 639-2 to RFC 5646 language tags
            DefaultLanguage = Language.Singleton.GetIetfTag(DefaultLanguage, true) ?? Language.English;
            List<string> oldList = KeepLanguages.ToList();
            KeepLanguages.Clear();
            oldList.ForEach(item => KeepLanguages.Add(Language.Singleton.GetIetfTag(item, true) ?? Language.English));

            // v2 -> v3 : Defaults
            KeepOriginalLanguage = true;
            RemoveClosedCaptions = true;
            SetIetfLanguageTags = true;
            SetTrackFlags = true;
        }

        // v3
        if (version <= ProcessOptions3.Version)
        {
            // Get v3 schema
            ProcessOptions3 processOptions3 = this;

            // v3 -> v4 : Convert KeepExtensions to IgnoreFiles
            foreach (var item in processOptions3.KeepExtensions)
            {
                // Convert ext to *.ext
                FileIgnoreMasks.Add($"*{item}");
            }
            processOptions3.KeepExtensions.Clear();
        }

        // v4
    }
#pragma warning restore CS0612 // Type or member is obsolete

    public void SetDefaults()
    {
        DefaultLanguage = "en";
        DeInterlace = true;
        DeleteEmptyFolders = true;
        DeleteUnwantedExtensions = true;
        FileIgnoreList.Clear();
        KeepOriginalLanguage = true;
        ReEncode = true;
        RemoveClosedCaptions = true;
        RemoveDuplicateTracks = false;
        RemoveTags = true;
        RemoveUnwantedLanguageTracks = false;
        ReMux = true;
        RestoreFileTimestamp = false;
        SetIetfLanguageTags = true;
        SetTrackFlags = true;
        SetUnknownLanguage = true;
        SidecarUpdateOnToolChange = false;
        UseSidecarFiles = true;
        Verify = true;
        FileIgnoreMasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "*.ass",
            "*.fuse_hidden*",
            "*.jpg",
            "*.nfo",
            "*.partial~",
            "*.sample.*",
            "*.sample",
            "*.smi",
            "*.srt",
            "*.ssa",
            "*.vtt"
        };
        KeepLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "af",
            "en",
            "in",
            "ja",
            "ko",
            "zh"
        };
        ReEncodeVideo =
        [
            new() { Format = "h264", Profile = "Constrained Baseline@30" },
            new() { Format = "indeo5" },
            new() { Format = "mpeg2video" },
            new() { Format = "mpeg4", Codec = "dx50" },
            new() { Format = "msmpeg4v2", Codec = "mp42" },
            new() { Format = "msmpeg4v3", Codec = "div3" },
            new() { Format = "msrle" },
            new() { Format = "rawvideo" },
            new() { Format = "vc1" },
            new() { Format = "wmv3" }
        ];
        ReEncodeAudioFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "adpcm_ms",
            "flac",
            "mp2",
            "opus",
            "pcm_s16le",
            "pcm_u8",
            "vorbis",
            "wmapro",
            "wmav2"
        };
        ReMuxExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".asf",
            ".avi",
            ".dv",
            ".m2ts",
            ".m4v",
            ".mp4",
            ".ts",
            ".vob",
            ".wmv"
        };
        PreferredAudioFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ac-3",
            "dts-hd high resolution audio",
            "dts-hd master audio",
            "dts",
            "e-ac-3",
            "truehd atmos",
            "truehd"
        };
    }

    public bool VerifyValues()
    {
        // Some values must be set
        if (string.IsNullOrEmpty(DefaultLanguage))
        {
            Log.Logger.Error("ProcessOptions:DefaultLanguage must be set");
            return false;
        }

        // Default to English if language not set
        if (string.IsNullOrEmpty(DefaultLanguage))
        {
            DefaultLanguage = Language.English;
        }

        // Always keep no linguistic content (zxx), undefined (und), and the default language
        KeepLanguages.Add(Language.None);
        KeepLanguages.Add(Language.Undefined);
        KeepLanguages.Add(DefaultLanguage);

        // Convert wildcards to regex
        FileIgnoreMaskToRegex();

        return true;
    }
}
