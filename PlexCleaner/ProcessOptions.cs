namespace PlexCleaner
{
    public class ProcessOptions
    {
        public bool TestNoModify { get; set; } = false;
        public bool DeleteEmptyFolders { get; set; } = true;
        public bool DeleteFailedFiles { get; set; } = true;
        public bool DeleteUnwantedExtensions { get; set; } = true;
        public string KeepExtensions { get; set; } = "";
        public bool ReMux { get; set; } = true;
        public string ReMuxExtensions { get; set; } = ".avi,.m2ts,.ts,.vob,.mp4,.m4v,.asf,.wmv";
        public bool DeInterlace { get; set; } = true;
        public bool ReEncode { get; set; } = true;
        public string ReEncodeVideoCodecs { get; set; } = "mpeg2video,msmpeg4v3,h264";
        public string ReEncodeVideoProfiles { get; set; } = "*,*,Constrained Baseline@30";
        public string ReEncodeAudioCodecs { get; set; } = "flac,mp2,vorbis,wmapro";
        public bool SetUnknownLanguage { get; set; } = true;
        public string DefaultLanguage { get; set; } = "eng";
        public bool RemoveUnwantedTracks { get; set; } = true;
        public string KeepLanguages { get; set; } = "eng,afr,chi,ind";
        public bool UseSidecarFiles { get; set; } = true;
    }
}
