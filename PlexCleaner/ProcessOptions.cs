using System.Collections.Generic;

namespace PlexCleaner
{
    public class ProcessOptions
    {
        public bool DeleteEmptyFolders { get; set; } = true;
        public bool DeleteUnwantedExtensions { get; set; } = true;
        public string KeepExtensions { get; set; } = ".partial~,.nfo,.jpg,.srt,.smi,.ssa,.ass,.vtt";
        public bool ReMux { get; set; } = true;
        public string ReMuxExtensions { get; set; } = ".avi,.m2ts,.ts,.vob,.mp4,.m4v,.asf,.wmv";
        public bool DeInterlace { get; set; } = true;
        public bool ReEncode { get; set; } = true;
        public string ReEncodeVideoFormats { get; set; } = "mpeg2video,mpeg4,msmpeg4v3,msmpeg4v2,vc1,h264,wmv3";
        public string ReEncodeVideoCodecs { get; set; } = "*,dx50,div3,mp42,*,*,*";
        public string ReEncodeVideoProfiles { get; set; } = "*,*,*,*,*,Constrained Baseline@30,*";
        public string ReEncodeAudioFormats { get; set; } = "flac,mp2,vorbis,wmapro,pcm_s16le,opus,wmav2";
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
}
