using System;
using System.Collections.Generic;
using System.Linq;

namespace PlexCleaner;

[Obsolete("Replaced in Schema v2", false)]
public class ProcessOptions1
{
    public bool DeleteEmptyFolders { get; set; }
    public bool DeleteUnwantedExtensions { get; set; }
    public string KeepExtensions { get; set; } = "";
    public bool ReMux { get; set; }
    public string ReMuxExtensions { get; set; } = "";
    public bool DeInterlace { get; set; }
    public bool ReEncode { get; set; }
    public string ReEncodeVideoFormats { get; set; } = "";
    public string ReEncodeVideoCodecs { get; set; } = "";
    public string ReEncodeVideoProfiles { get; set; } = "";
    public string ReEncodeAudioFormats { get; set; } = "";
    public bool SetUnknownLanguage { get; set; }
    public string DefaultLanguage { get; set; } = "";
    public bool RemoveUnwantedLanguageTracks { get; set; }
    public string KeepLanguages { get; set; } = "";
    public bool RemoveDuplicateTracks { get; set; }
    public string PreferredAudioFormats { get; set; } = "";
    public bool RemoveTags { get; set; }
    public bool UseSidecarFiles { get; set; }
    public bool SidecarUpdateOnToolChange { get; set; }
    public bool Verify { get; set; }
    public bool RestoreFileTimestamp { get; set; }
    public List<string> FileIgnoreList { get; set; } = new();
}

public class ProcessOptions
{
    public class VideoFormat
    {
        public string Format;
        public string Codec;
        public string Profile;
    }

    public ProcessOptions() { }

#pragma warning disable CS0618 // Type or member is obsolete
    public ProcessOptions(ProcessOptions1 processOptions1)
#pragma warning restore CS0618 // Type or member is obsolete
    {
        // Assign same values
        DeleteEmptyFolders = processOptions1.DeleteEmptyFolders;
        DeleteUnwantedExtensions = processOptions1.DeleteUnwantedExtensions;
        ReMux = processOptions1.ReMux;
        DeInterlace = processOptions1.DeInterlace;
        ReEncode = processOptions1.ReEncode;
        SetUnknownLanguage = processOptions1.SetUnknownLanguage;
        RemoveUnwantedLanguageTracks = processOptions1.RemoveUnwantedLanguageTracks;
        RemoveDuplicateTracks = processOptions1.RemoveDuplicateTracks;
        RemoveTags = processOptions1.RemoveTags;
        UseSidecarFiles = processOptions1.UseSidecarFiles;
        SidecarUpdateOnToolChange = processOptions1.SidecarUpdateOnToolChange;
        Verify = processOptions1.Verify;
        RestoreFileTimestamp = processOptions1.RestoreFileTimestamp;

        DefaultLanguage = processOptions1.DefaultLanguage ?? "";
        FileIgnoreList = processOptions1.FileIgnoreList ?? new List<string>();

        // Convert CSV to List<string>
        KeepExtensions = !string.IsNullOrEmpty(processOptions1.KeepExtensions) ? processOptions1.KeepExtensions.Split(',').ToList() : new List<string>();
        ReMuxExtensions = !string.IsNullOrEmpty(processOptions1.ReMuxExtensions) ? processOptions1.ReMuxExtensions.Split(',').ToList() : new List<string>();
        ReEncodeAudioFormats = !string.IsNullOrEmpty(processOptions1.ReEncodeAudioFormats) ? processOptions1.ReEncodeAudioFormats.Split(',').ToList() : new List<string>();
        KeepLanguages = !string.IsNullOrEmpty(processOptions1.KeepLanguages) ? processOptions1.KeepLanguages.Split(',').ToList() : new List<string>();
        PreferredAudioFormats = !string.IsNullOrEmpty(processOptions1.PreferredAudioFormats) ? processOptions1.PreferredAudioFormats.Split(',').ToList() : new List<string>();

        // Convert to List<VideoFormat>
        ReEncodeVideo = new List<VideoFormat>();
        if (!string.IsNullOrEmpty(processOptions1.ReEncodeVideoCodecs) &&
            !string.IsNullOrEmpty(processOptions1.ReEncodeVideoFormats) &&
            !string.IsNullOrEmpty(processOptions1.ReEncodeVideoProfiles))
        {
            List<string> codecList = processOptions1.ReEncodeVideoCodecs.Split(',').ToList();
            List<string> formatList = processOptions1.ReEncodeVideoFormats.Split(',').ToList();
            List<string> profileList = processOptions1.ReEncodeVideoProfiles.Split(',').ToList();
            if (codecList.Count != formatList.Count || formatList.Count != profileList.Count)
                // The number of arguments has to match
                throw new ArgumentException("ReEncodeVideo argument count mismath");
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
                    videoFormat.Codec = null;
                if (videoFormat.Format.Equals("*", StringComparison.OrdinalIgnoreCase))
                    videoFormat.Format = null;
                if (videoFormat.Profile.Equals("*", StringComparison.OrdinalIgnoreCase))
                    videoFormat.Profile = null;
                ReEncodeVideo.Add(videoFormat);
            }
        }
    }

    public bool DeleteEmptyFolders { get; set; }
    public bool DeleteUnwantedExtensions { get; set; }
    public List<string> KeepExtensions { get; set; }
    public bool ReMux { get; set; }
    public List<string> ReMuxExtensions { get; set; }
    public bool DeInterlace { get; set; }
    public bool ReEncode { get; set; }
    public List<VideoFormat> ReEncodeVideo { get; set; } = new();
    public List<string> ReEncodeAudioFormats { get; set; } = new();
    public bool SetUnknownLanguage { get; set; }
    public string DefaultLanguage { get; set; } = "";
    public bool RemoveUnwantedLanguageTracks { get; set; }
    public List<string> KeepLanguages { get; set; } = new();
    public bool RemoveDuplicateTracks { get; set; }
    public List<string> PreferredAudioFormats { get; set; } = new();
    public bool RemoveTags { get; set; }
    public bool UseSidecarFiles { get; set; }
    public bool SidecarUpdateOnToolChange { get; set; }
    public bool Verify { get; set; }
    public bool RestoreFileTimestamp { get; set; }
    public List<string> FileIgnoreList { get; set; } = new();

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
        RemoveDuplicateTracks = true;
        FileIgnoreList = new List<string>();
        DefaultLanguage = "eng";
        KeepExtensions = new List<string>
        {
            // TODO : Add UnRaid FUSE files e.g. .fuse_hidden191817c5000c5ee7, will need wildcard support
            ".partial~", 
            ".nfo", 
            ".jpg", 
            ".srt", 
            ".smi", 
            ".ssa", 
            ".ass", 
            ".vtt" 
        };
        ReMuxExtensions = new List<string>
        { 
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
            new() { Format = "msmpeg4v2", Codec = "mp42" },
            new() { Format = "msmpeg4v3", Codec = "div3" }
        };
        ReEncodeAudioFormats = new List<string>
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
        KeepLanguages = new List<string>
        { 
            "eng", 
            "afr", 
            "chi", 
            "ind" 
        };
        PreferredAudioFormats = new List<string>
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
