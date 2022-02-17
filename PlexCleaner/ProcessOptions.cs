using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PlexCleaner;

[Obsolete("Replaced in Schema v2", false)]
public class ProcessOptions1
{
    public bool DeleteEmptyFolders { get; set; } = true;
    public bool DeleteUnwantedExtensions { get; set; } = true;
    public string KeepExtensions { get; set; } = ".partial~,.nfo,.jpg,.srt,.smi,.ssa,.ass,.vtt";
    public bool ReMux { get; set; } = true;
    public string ReMuxExtensions { get; set; } = ".avi,.m2ts,.ts,.vob,.mp4,.m4v,.asf,.wmv";
    public bool DeInterlace { get; set; } = true;
    public bool ReEncode { get; set; } = true;
    public string ReEncodeVideoFormats { get; set; } = "mpeg2video,mpeg4,msmpeg4v3,msmpeg4v2,vc1,h264,wmv3,msrle,rawvideo,indeo5";
    public string ReEncodeVideoCodecs { get; set; } = "*,dx50,div3,mp42,*,*,*,*,*,*";
    public string ReEncodeVideoProfiles { get; set; } = "*,*,*,*,*,Constrained Baseline@30,*,*,*,*";
    public string ReEncodeAudioFormats { get; set; } = "flac,mp2,vorbis,wmapro,pcm_s16le,opus,wmav2,pcm_u8,adpcm_ms";
    public bool SetUnknownLanguage { get; set; } = true;
    public string DefaultLanguage { get; set; } = "eng";
    public bool RemoveUnwantedLanguageTracks { get; set; } = true;
    public string KeepLanguages { get; set; } = "eng,afr,chi,ind";
    public bool RemoveDuplicateTracks { get; set; } = true;
    public string PreferredAudioFormats { get; set; } = "truehd atmos,truehd,dts-hd master audio,dts-hd high resolution audio,dts,e-ac-3,ac-3";
    public bool RemoveTags { get; set; } = true;
    public bool UseSidecarFiles { get; set; } = true;
    public bool SidecarUpdateOnToolChange { get; set; }
    public bool Verify { get; set; } = true;
    public bool RestoreFileTimestamp { get; set; } = false;
    public List<string> FileIgnoreList { get; } = new();
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
        DefaultLanguage = processOptions1.DefaultLanguage;
        RemoveUnwantedLanguageTracks = processOptions1.RemoveUnwantedLanguageTracks;
        RemoveDuplicateTracks = processOptions1.RemoveDuplicateTracks;
        RemoveTags = processOptions1.RemoveTags;
        UseSidecarFiles = processOptions1.UseSidecarFiles;
        SidecarUpdateOnToolChange = processOptions1.SidecarUpdateOnToolChange;
        Verify = processOptions1.Verify;
        RestoreFileTimestamp = processOptions1.RestoreFileTimestamp;
        FileIgnoreList = processOptions1.FileIgnoreList;

        // Convert CSV to List<string>
        KeepExtensions = processOptions1.KeepExtensions.Split(',').ToList();
        ReMuxExtensions = processOptions1.ReMuxExtensions.Split(',').ToList();
        ReEncodeAudioFormats = processOptions1.ReEncodeAudioFormats.Split(',').ToList();
        KeepLanguages = processOptions1.KeepLanguages.Split(',').ToList();
        PreferredAudioFormats = processOptions1.PreferredAudioFormats.Split(',').ToList();

        // Convert to List<VideoFormat>
        List<string> codecList = processOptions1.ReEncodeVideoCodecs.Split(',').ToList();
        List<string> formatList = processOptions1.ReEncodeVideoFormats.Split(',').ToList();
        List<string> profileList = processOptions1.ReEncodeVideoProfiles.Split(',').ToList();
        if (codecList.Count != formatList.Count || formatList.Count != profileList.Count)
            // The number of arguments has to match
            throw new ArgumentException("ReEncodeVideo argument count mismath");
        ReEncodeVideo = new();
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

    public bool DeleteEmptyFolders { get; set; } = true;
    public bool DeleteUnwantedExtensions { get; set; } = true;
    // TODO : Add support for ignoring FUSE files e.g. .fuse_hidden191817c5000c5ee7, will need wildcard support
    public List<string> KeepExtensions { get; } = new() { ".partial~", ".nfo", ".jpg", ".srt", ".smi", ".ssa", ".ass", ".vtt" };
    public bool ReMux { get; set; } = true;
    public List<string> ReMuxExtensions { get; } = new() { ".avi", ".m2ts", ".ts", ".vob", ".mp4", ".m4v", ".asf", ".wmv" };
    public bool DeInterlace { get; set; } = true;
    public bool ReEncode { get; set; } = true;
    public List<VideoFormat> ReEncodeVideo { get; } = new()
    {
        new() { Format = "mpeg2video" },
        new() { Format = "mpeg4", Codec = "dx50" },
        new() { Format = "msmpeg4v3", Codec = "div3" },
        new() { Format = "msmpeg4v2", Codec = "mp42" },
        new() { Format = "vc1" },
        new() { Format = "h264", Profile = "Constrained Baseline@30"},
        new() { Format = "wmv3" },
        new() { Format = "msrle" },
        new() { Format = "rawvideo" },
        new() { Format = "indeo5" }
        };
    public List<string> ReEncodeAudioFormats { get; } = new() { "flac", "mp2", "vorbis", "wmapro", "pcm_s16le", "opus", "wmav2", "pcm_u8", "adpcm_ms" };
    public bool SetUnknownLanguage { get; set; } = true;
    public string DefaultLanguage { get; set; } = "eng";
    public bool RemoveUnwantedLanguageTracks { get; set; } = true;
    public List<string> KeepLanguages { get; } = new() { "eng", "afr", "chi", "ind" };
    public bool RemoveDuplicateTracks { get; set; } = true;
    public List<string> PreferredAudioFormats { get; } = new() { "truehd atmos", "truehd,dts-hd master audio", "dts-hd high resolution audio", "dts", "e-ac-3", "ac-3" };
    public bool RemoveTags { get; set; } = true;
    public bool UseSidecarFiles { get; set; } = true;
    public bool SidecarUpdateOnToolChange { get; set; }
    public bool Verify { get; set; } = true;
    public bool RestoreFileTimestamp { get; set; }
    public List<string> FileIgnoreList { get; } = new();
}
