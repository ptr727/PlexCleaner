using System;
using System.Collections.Generic;
using System.Linq;
using InsaneGenius.Utilities;

namespace PlexCleaner
{
    public static class Info
    {
        public class TrackInfo
        {
            protected TrackInfo()
            {
                State = StateType.None;
            }

            protected TrackInfo(MkvTool.TrackJson track)
            {
                Format = track.Codec;
                Codec = track.Properties.CodecId;

                // MKVMerge sets the language to always be und or 3 letter ISO 639-2 code
                Language = track.Properties.Language;
                if (Language.Length != 3)
                    throw new ArgumentException($"Invalid Language Format : \"{Language}\"");
                
                // Take care to use id and number correctly in MKVMerge and MKVPropEdit
                Id = track.Id;
                Number = track.Properties.Number;
            }

            protected TrackInfo(FfMpegTool.StreamJson stream)
            {
                Format = stream.CodecName;
                Codec = stream.CodecLongName;
                Profile = stream.Profile;

                // TODO : FFProbe interprets the language tag instead of tag_language
                // Result is MediaInfo and MKVMerge say language is "eng", FFProbe says language is "und"
                // https://github.com/MediaArea/MediaAreaXml/issues/34

                // Set language if Tags is not null
                // TODO : Language in some sample files is "???", set to und
                Language = stream.Tags?.Language;
                if (String.IsNullOrEmpty(Language))
                    Language = "und";
                else if (Language.Equals("???"))
                    Language = "und";

                // FFProbe sets the language to always be und or 3 letter ISO 639-2 code
                if (Language.Length != 3)
                    throw new ArgumentException($"Invalid Language Format : \"{Language}\"");

                // Use index for number
                Id = stream.Index;
                Number = stream.Index;
            }

            protected TrackInfo(MediaInfoTool.TrackXml track)
            {
                Format = track.Format;
                Codec = track.CodecId;
                Profile = track.FormatProfile;

                Language = track.Language;
                if (!String.IsNullOrEmpty(track.Language))
                {
                    // MediaInfo uses ab or abc or ab-cd tags, we need to convert to ISO 639-2
                    // https://github.com/MediaArea/MediaAreaXml/issues/33
                    Iso6393 lang = Iso6393.FromString(track.Language, Program.Default.Iso6393List);
                    Language = lang != null ? lang.Part2B : "und";
                }
                else
                    Language = "und";

                // FFProbe and Matroksa use chi not zho
                // https://github.com/mbunkus/mkvtoolnix/issues/1149
                if (Language.Equals("zho", StringComparison.OrdinalIgnoreCase))
                    Language = "chi";


                // ID can be an integer or an integer-type, e.g. 3-CC1
                // https://github.com/MediaArea/MediaInfo/issues/201
                Id = Int32.Parse(track.Id.All(Char.IsDigit) ? track.Id : track.Id.Substring(0, track.Id.IndexOf('-')));

                // Use streamorder for number
                // StreamOrder is not always present
                if (!String.IsNullOrEmpty(track.StreamOrder))
                    Number = Int32.Parse(track.StreamOrder);
            }
            public string Format { get; set; }
            public string Codec { get; set; }
            public string Profile { get; set; }
            public string Language { get; set; }
            public int Id { get; set; }
            public int Number { get; set; }
            public enum StateType { None, Keep, Remove, ReMux, ReEncode }
            public StateType State { get; set; }
            public bool IsLanguageUnknown()
            {
                // Test for empty or "und" field values
                return String.IsNullOrEmpty(Language) ||
                       Language.Equals("und", StringComparison.OrdinalIgnoreCase);
            }
        }

        public class VideoInfo : TrackInfo
        {
            public VideoInfo() { }
            public VideoInfo(MkvTool.TrackJson track) : base(track)
            {
                // No profile information in MKVMerge
            }
            public VideoInfo(FfMpegTool.StreamJson stream) : base(stream)
            {
                // TODO : Find a better way to do this
                // https://trac.ffmpeg.org/ticket/2901
                // https://stackoverflow.com/questions/42619191/what-does-level-mean-in-ffprobe-output
                // https://www.ffmpeg.org/doxygen/3.2/nvEncodeAPI_8h_source.html#l00331
                // https://www.ffmpeg.org/doxygen/3.2/avcodec_8h_source.html#l03210
                // https://www.ffmpeg.org/doxygen/3.2/mpeg12enc_8c_source.html#l00138
                // https://en.wikipedia.org/wiki/H.264/MPEG-4_AVC#Levels
                if (!String.IsNullOrEmpty(stream.Profile) && !String.IsNullOrEmpty(stream.Level))
                    Profile = $"{stream.Profile}@{stream.Level}";
                else if (!String.IsNullOrEmpty(stream.Profile))
                    Profile = stream.Profile;
            }
            public VideoInfo(MediaInfoTool.TrackXml track) : base(track)
            {
                // TODO : Find a better way to do this
                if (!String.IsNullOrEmpty(track.FormatProfile) && !String.IsNullOrEmpty(track.FormatLevel))
                    Profile = $"{track.FormatProfile}@{track.FormatLevel}";
                else if (!String.IsNullOrEmpty(track.FormatProfile))
                    Profile = track.FormatProfile;
            }

            public bool CompareVideo(VideoInfo compare)
            {
                // Match the logic in Process
                // Compare the format
                if (!Format.Equals(compare.Format, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (String.IsNullOrEmpty(compare.Profile) || 
                    compare.Profile.Equals("*", StringComparison.OrdinalIgnoreCase) ||
                    Profile.Equals(compare.Profile, StringComparison.OrdinalIgnoreCase))
                    return true;
                return false;
            }

            public override string ToString()
            {
                return $"Video : Format : {Format}, Codec : {Codec}, Profile : {Profile}, Language : {Language}, Id : {Id}, Number : {Number}";
            }
        }

        public class AudioInfo : TrackInfo
        {
            public AudioInfo() { }
            public AudioInfo(MkvTool.TrackJson track) : base(track) { }
            public AudioInfo(FfMpegTool.StreamJson stream) : base(stream) { }
            public AudioInfo(MediaInfoTool.TrackXml track) : base(track) { }

            public override string ToString()
            {
                return $"Audio : Format : {Format}, Codec : {Codec}, Language : {Language}, Id : {Id}, Number : {Number}";
            }
        }

        public class SubtitleInfo : TrackInfo
        {
            public SubtitleInfo() { }
            public SubtitleInfo(MkvTool.TrackJson track) : base(track) { }
            public SubtitleInfo(FfMpegTool.StreamJson stream) : base(stream) { }
            public SubtitleInfo(MediaInfoTool.TrackXml track) : base(track)
            {
                MuxingMode = track.MuxingMode;
            }


            public string MuxingMode { get; set; }
            public override string ToString()
            {
                return $"Subtitle : Format : {Format}, Codec : {Codec}, MuxingMode : {MuxingMode}, Language : {Language}, Id : {Id}, Number : {Number}";
            }
        }

        public class MediaInfo
        {
            public MediaInfo()
            {
                Video = new List<VideoInfo>();
                Audio = new List<AudioInfo>();
                Subtitle = new List<SubtitleInfo>();
                Parser = ParserType.None;
            }
            public List<VideoInfo> Video { get; set; }
            public List<AudioInfo> Audio { get; set; }
            public List<SubtitleInfo> Subtitle { get; set; }
            public enum ParserType { None, MediaInfo, MkvMerge, FfProbe }
            public ParserType Parser { get; set; }

            public void WriteLine(string prefix)
            {
                foreach (VideoInfo info in Video)
                    ConsoleEx.WriteLine($"{prefix} : {info}");
                foreach (AudioInfo info in Audio)
                    ConsoleEx.WriteLine($"{prefix} : {info}");
                foreach (SubtitleInfo info in Subtitle)
                    ConsoleEx.WriteLine($"{prefix} : {info}");
            }

/*
            public void WriteLine()
            {
                foreach (VideoInfo info in Video)
                    Tools.WriteLine($"{info}");
                foreach (AudioInfo info in Audio)
                    Tools.WriteLine($"{info}");
                foreach (SubtitleInfo info in Subtitle)
                    Tools.WriteLine($"{info}");
            }
*/

            // Return a list with all tracks combined
            public List<TrackInfo> GetTrackList()
            {
                // Combine all tracks
                List<TrackInfo> combined = new List<TrackInfo>();
                combined.AddRange(Video);
                combined.AddRange(Audio);
                combined.AddRange(Subtitle);

                // Sort items by Id
                List<TrackInfo> ordered = combined.OrderBy(item => item.Id).ToList();

                return ordered;
            }

            // Find all tracks with an unknown language
            public bool FindUnknownLanguage(out MediaInfo known, out MediaInfo unknown)
            {
                known = new MediaInfo();
                unknown = new MediaInfo();

                // Video
                foreach (VideoInfo video in Video)
                    if (video.IsLanguageUnknown())
                        unknown.Video.Add(video);
                    else
                        known.Video.Add(video);

                // Audio
                foreach (AudioInfo audio in Audio)
                    if (audio.IsLanguageUnknown())
                        unknown.Audio.Add(audio);
                    else
                        known.Audio.Add(audio);

                // Subtitle
                foreach (SubtitleInfo subtitle in Subtitle)
                    if (subtitle.IsLanguageUnknown())
                        unknown.Subtitle.Add(subtitle);
                    else
                        known.Subtitle.Add(subtitle);

                // Return true on any match
                return unknown.Video.Count > 0 || unknown.Audio.Count > 0 || unknown.Subtitle.Count > 0;
            }

            // Find all tracks that need to be remuxed
            public bool FindNeedReMux(out MediaInfo keep, out MediaInfo remux)
            {
                keep = new MediaInfo();
                remux = new MediaInfo();

                // No filter for audio or video
                keep.Video = Video;
                keep.Audio = Audio;

                // We need MuxingMode for VOBSUB else Plex on Nvidia Shield TV has problems
                //  https://forums.plex.tv/discussion/290723/long-wait-time-before-playing-some-content-player-says-directplay-server-says-transcoding
                //  https://github.com/mbunkus/mkvtoolnix/issues/2131
                foreach (SubtitleInfo subtitle in Subtitle)
                    if (subtitle.Codec.Equals("S_VOBSUB", StringComparison.OrdinalIgnoreCase) && 
                        String.IsNullOrEmpty(subtitle.MuxingMode))
                        remux.Subtitle.Add(subtitle);
                    else
                        keep.Subtitle.Add(subtitle);

                // Set the correct state on all the objects
                remux.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.ReMux);
                keep.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.Keep);

                // Return true on any match
                return remux.Video.Count > 0 || remux.Audio.Count > 0 || remux.Subtitle.Count > 0;
            }

            // Find all the tracks that need to be removed
            public bool FindNeedRemove(HashSet<string> languages, out MediaInfo keep, out MediaInfo remove)
            {
                keep = new MediaInfo();
                remove = new MediaInfo();

                // TODO : Remove duplicate audio tracks, e.g. DTS and AC3 both English, remove AC3
                // TODO : Remove unwanted subtitle tracks, e.g. bitmap based subtitles like PGS

                // We keep all the video tracks that match our language
                foreach (VideoInfo video in Video)
                    if (languages.Contains(video.Language))
                        keep.Video.Add(video);
                    else
                        remove.Video.Add(video);

                // If we have no video tracks matching our language, e.g. a foreign film, we keep the first video track
                if (keep.Video.Count == 0 && Video.Count > 0)
                {
                    keep.Video.Add(Video.First());
                    remove.Video.Remove(Video.First());
                }

                // We keep all the audio tracks that match our language
                foreach (AudioInfo audio in Audio)
                    if (languages.Contains(audio.Language))
                        keep.Audio.Add(audio);
                    else
                        remove.Audio.Add(audio);

                // If we have no audio tracks matching our language, e.g. a foreign film, we keep the first audio track
                if (keep.Audio.Count == 0 && Audio.Count > 0)
                {
                    keep.Audio.Add(Audio.First());
                    remove.Audio.Remove(Audio.First());
                }

                // We keep all the subtitle tracks that match our language
                foreach (SubtitleInfo subtitle in Subtitle)
                    if (languages.Contains(subtitle.Language))
                        keep.Subtitle.Add(subtitle);
                    else
                        remove.Subtitle.Add(subtitle);

                // Set the correct state on all the objects
                remove.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.Remove);
                keep.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.Keep);

                // Return true on any match
                return remove.Video.Count > 0 || remove.Audio.Count > 0 || remove.Subtitle.Count > 0;
            }

            // find all tracks that need to be re-encoded
            public bool FindNeedReEncode(List<VideoInfo> reencodevideo, HashSet<string> reencodeaudio, out MediaInfo keep, out MediaInfo reencode)
            {
                keep = new MediaInfo();
                reencode = new MediaInfo();

                // Match video codecs from the reencode list
                foreach (VideoInfo video in Video)
                {
                    // See if the track matches any of the re-encode specs
                    bool match = false;
                    foreach (VideoInfo compare in reencodevideo)
                        if (video.CompareVideo(compare))
                            match = true;

                    // Re-encode or passthrough
                    if (match)
                        reencode.Video.Add(video);
                    else
                        keep.Video.Add(video);
                }

                // Match audio codecs from the reencode list
                foreach (AudioInfo audio in Audio)
                {
                    // Re-encode or passthrough
                    if (reencodeaudio.Contains(audio.Format))
                        reencode.Audio.Add(audio);
                    else
                        keep.Audio.Add(audio);
                }

                // Keep all subtitle tracks
                keep.Subtitle = Subtitle;

                // Set the correct state on all the objects
                reencode.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.ReEncode);
                keep.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.Keep);

                // Return true on any match
                return reencode.Video.Count > 0 || reencode.Audio.Count > 0 || reencode.Subtitle.Count > 0;
            }
        }

        // Get stream info using MediaInfo
        public static bool GetMediaInfo(string filename, out MediaInfo mediainfo)
        {
            mediainfo = null;
            return GetMediaInfoXml(filename, out string xml) && GetMediaInfoFromXml(xml, out mediainfo);
        }

        public static bool GetMediaInfoXml(string filename, out string xml)
        {
            // Create the MediaInfo commandline and execute
            // http://manpages.ubuntu.com/manpages/zesty/man1/mediainfo.1.html
            string commandline = $"--Output=XML \"{filename}\"";
            ConsoleEx.WriteLine($"Getting stream info : \"{filename}\"");
            int exitcode = MediaInfoTool.MediaInfo(commandline, out xml);
            // TODO : No error is returned when the file does not exist
            // https://sourceforge.net/p/mediainfo/bugs/1052/
            // Empty XML files are around 86 bytes
            // Match size check with ProcessSidecarFile()
            if (exitcode == 0 && xml.Length >= 100)
                return true;

            ConsoleEx.WriteLineError($"Error getting stream info : \"{filename}\"");
            return false;
        }

        public static bool GetMediaInfoFromXml(string xml, out MediaInfo mediainfo)
        {
            // Populate the MediaInfo object from the XML string
            mediainfo = new MediaInfo
            {
                Parser = MediaInfo.ParserType.MediaInfo
            };
            try
            {
                MediaInfoTool.MediaInfoXml xmlinfo = MediaInfoTool.MediaInfoXml.FromXml(xml);
                MediaInfoTool.MediaXml xmlmedia = xmlinfo.Media;
                if (xmlmedia.Track.Count == 0)
                {
                    ConsoleEx.WriteLineError("Error getting stream info");
                    return false;
                }


                foreach (MediaInfoTool.TrackXml track in xmlmedia.Track)
                {
                    if (track.Type.Equals("Video", StringComparison.OrdinalIgnoreCase))
                    {
                        VideoInfo info = new VideoInfo(track);
                        mediainfo.Video.Add(info);
                    }
                    else if (track.Type.Equals("Audio", StringComparison.OrdinalIgnoreCase))
                    {
                        AudioInfo info = new AudioInfo(track);
                        mediainfo.Audio.Add(info);
                    }
                    else if (track.Type.Equals("text", StringComparison.OrdinalIgnoreCase))
                    {
                        SubtitleInfo info = new SubtitleInfo(track);
                        mediainfo.Subtitle.Add(info);
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLineError(e);
                return false;
            }

            return true;
        }

        // Get stream info using MKVMerge
        public static bool GetMkvInfo(string filename, out MediaInfo mediainfo)
        {
            mediainfo = null;
            return GetMkvInfoJson(filename, out string json) && GetMkvInfoFromJson(json, out mediainfo);
        }

        public static bool GetMkvInfoJson(string filename, out string json)
        {
            // Create the MKVMerge commandline and execute
            // Note the correct usage of "id" or "number" in the MKV tools
            // https://mkvtoolnix.download/doc/mkvmerge.html#mkvmerge.description.identify
            // https://mkvtoolnix.download/doc/mkvpropedit.html
            // https://mkvtoolnix.download/doc/mkvmerge.html
            string commandline = $"--identify \"{filename}\" --identification-format json";
            ConsoleEx.WriteLine($"Getting stream info : \"{filename}\"");
            int exitcode = MkvTool.MkvMerge(commandline, out json);
            if (exitcode == 0)
                return true;

            ConsoleEx.WriteLineError($"Error getting stream info : \"{filename}\"");
            return false;
        }

        public static bool GetMkvInfoFromJson(string json, out MediaInfo mediainfo)
        {
            // Populate the MediaInfo object from the JSON string
            mediainfo = new MediaInfo
            {
                Parser = MediaInfo.ParserType.MkvMerge
            };
            try
            {
                MkvTool.MkvMergeJson mkvmerge = MkvTool.MkvMergeJson.FromJson(json);
                if (mkvmerge.Tracks.Count == 0)
                {
                    ConsoleEx.WriteLineError("Error getting stream info");
                    return false;
                }
                foreach (MkvTool.TrackJson track in mkvmerge.Tracks)
                {
                    if (track.Type.Equals("video", StringComparison.OrdinalIgnoreCase))
                    {
                        VideoInfo info = new VideoInfo(track);
                        mediainfo.Video.Add(info);
                    }
                    else if (track.Type.Equals("audio", StringComparison.OrdinalIgnoreCase))
                    {
                        AudioInfo info = new AudioInfo(track);
                        mediainfo.Audio.Add(info);
                    }
                    else if (track.Type.Equals("subtitles", StringComparison.OrdinalIgnoreCase))
                    {
                        SubtitleInfo info = new SubtitleInfo(track);
                        mediainfo.Subtitle.Add(info);
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLineError(e);
                return false;
            }

            return true;
        }

        // Get stream info using FFProbe
        public static bool GetFfProbeInfo(string filename, out MediaInfo mediainfo)
        {
            mediainfo = null;
            return GetFfProbeInfoJson(filename, out string json) && GetFfProbeInfoFromJson(json, out mediainfo);
        }

        public static bool GetFfProbeInfoJson(string filename, out string json)
        {
            // Create the FFProbe commandline and execute
            // https://ffmpeg.org/ffprobe.html
            string commandline = $"-v quiet -show_streams -print_format json \"{filename}\"";
            ConsoleEx.WriteLine($"Getting stream info : \"{filename}\"");
            int exitcode = FfMpegTool.FfProbe(commandline, out json, out string error);
            // TODO : Verify that FFProbe returns an error when it fails
            if (exitcode == 0 && error.Length <= 0)
                return true;

            ConsoleEx.WriteLineError($"Error getting stream info : \"{filename}\"");
            return false;
        }

        public static bool GetFfProbeInfoFromJson(string json, out MediaInfo mediainfo)
        {
            // Populate the MediaInfo object from the JSON string
            mediainfo = new MediaInfo
            {
                Parser = MediaInfo.ParserType.FfProbe
            };
            try
            {
                FfMpegTool.FfProbeJson ffprobe = FfMpegTool.FfProbeJson.FromJson(json);
                if (ffprobe.Streams.Count == 0)
                {
                    ConsoleEx.WriteLineError("Error getting stream info");
                    return false;
                }
                foreach (FfMpegTool.StreamJson stream in ffprobe.Streams)
                {
                    if (stream.CodecType.Equals("video", StringComparison.OrdinalIgnoreCase))
                    {
                        // We need to exclude cover art, look for mimetype in tags
                        // TODO : Find a more reliable way of identifying cover art
                        if (!String.IsNullOrEmpty(stream.Tags?.MimeType))
                            continue;

                        VideoInfo info = new VideoInfo(stream);
                        mediainfo.Video.Add(info);
                    }
                    else if (stream.CodecType.Equals("audio", StringComparison.OrdinalIgnoreCase))
                    {
                        AudioInfo info = new AudioInfo(stream);
                        mediainfo.Audio.Add(info);
                    }
                    else if (stream.CodecType.Equals("subtitle", StringComparison.OrdinalIgnoreCase))
                    {
                        SubtitleInfo info = new SubtitleInfo(stream);
                        mediainfo.Subtitle.Add(info);
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleEx.WriteLineError(e);
                return false;
            }

            return true;
        }

        public static bool SetMkvTrackLanguage(string filename, int track, string language)
        {
            // Test
            if (AppOptions.Default.TestNoModify)
                return true;

            // Create the MKVPropEdit commandline and execute
            // The track number is reported by MKVMerge --identify using the track.properties.number value
            // https://mkvtoolnix.download/doc/mkvpropedit.html
            string commandline = $"\"{filename}\" --edit track:@{track} --set language={language}";
            ConsoleEx.WriteLine($"Setting track language : \"{filename}\" {track} {language}");
            ConsoleEx.WriteLine("");
            int exitcode = MkvTool.MkvPropEdit(commandline);
            ConsoleEx.WriteLine("");
            if (exitcode == 0)
                return true;

            ConsoleEx.WriteLineError($"Error setting track language : \"{filename}\"");
            return false;
        }

/*
        public static bool VerifyMedia(string inputname)
        {
            // Create the FFmpeg commandline and execute
            // https://ffmpeg.org/ffmpeg.html
            string commandline = $"-v warning -i \"{inputname}\" -f null -";
            Tools.WriteLine($"Verifying media : \"{inputname}\"");
            int exitcode = FfMpegTool.FfMpeg(commandline, out string _, out string error);
            if (exitcode != 0 || error.Length > 0)
            {
                Tools.WriteLineError($"Error verifying media : \"{inputname}\"");
                return false;
            }
            return true;
        }
*/

        public class TagMap
        {
            public string Primary { get; set; }
            public MediaInfo.ParserType PrimaryTool { get; set; }
            public string Secondary { get; set; }
            public MediaInfo.ParserType SecondaryTool { get; set; }
            public string Tertiary { get; set; }
            public MediaInfo.ParserType TertiaryTool { get; set; }
        }

        public class TagMapDictionary
        {
            public TagMapDictionary()
            {
                Video = new Dictionary<string, TagMap>(StringComparer.OrdinalIgnoreCase);
                Audio = new Dictionary<string, TagMap>(StringComparer.OrdinalIgnoreCase);
                Subtitle = new Dictionary<string, TagMap>(StringComparer.OrdinalIgnoreCase);
            }
            public Dictionary<string, TagMap> Video { get; set; }
            public Dictionary<string, TagMap> Audio { get; set; }
            public Dictionary<string, TagMap> Subtitle { get; set; }
            public void Add(MediaInfo prime, MediaInfo sec1, MediaInfo sec2)
            {
                for (int i = 0; i < prime.Video.Count; i++)
                {
                    TagMap tag = new TagMap
                    {
                        Primary = prime.Video.ElementAt(i).Format,
                        Secondary = sec1.Video.ElementAt(i).Format,
                        Tertiary = sec2.Video.ElementAt(i).Format,
                        PrimaryTool = prime.Parser,
                        SecondaryTool = sec1.Parser,
                        TertiaryTool = sec2.Parser
                    };
                    if (!Video.ContainsKey(tag.Primary))
                        Video.Add(tag.Primary, tag);
                }
                for (int i = 0; i < prime.Audio.Count; i++)
                {
                    TagMap tag = new TagMap
                    {
                        Primary = prime.Audio.ElementAt(i).Format,
                        Secondary = sec1.Audio.ElementAt(i).Format,
                        Tertiary = sec2.Audio.ElementAt(i).Format,
                        PrimaryTool = prime.Parser,
                        SecondaryTool = sec1.Parser,
                        TertiaryTool = sec2.Parser
                    };
                    if (!Audio.ContainsKey(tag.Primary))
                        Audio.Add(tag.Primary, tag);
                }
                for (int i = 0; i < prime.Subtitle.Count; i++)
                {
                    TagMap tag = new TagMap
                    {
                        Primary = prime.Subtitle.ElementAt(i).Format,
                        Secondary = sec1.Subtitle.ElementAt(i).Format,
                        Tertiary = sec2.Subtitle.ElementAt(i).Format,
                        PrimaryTool = prime.Parser,
                        SecondaryTool = sec1.Parser,
                        TertiaryTool = sec2.Parser
                    };
                    if (!Subtitle.ContainsKey(tag.Primary))
                        Subtitle.Add(tag.Primary, tag);
                }
            }

            public void WriteLine()
            {
                foreach (KeyValuePair<string, TagMap> tag in Video)
                    Console.WriteLine($"Video, {tag.Value.PrimaryTool}, {tag.Value.Primary}, {tag.Value.SecondaryTool}, {tag.Value.Secondary}, {tag.Value.TertiaryTool}, {tag.Value.Tertiary}");
                foreach (KeyValuePair<string, TagMap> tag in Audio)
                    Console.WriteLine($"Audio, {tag.Value.PrimaryTool}, {tag.Value.Primary}, {tag.Value.SecondaryTool}, {tag.Value.Secondary}, {tag.Value.TertiaryTool}, {tag.Value.Tertiary}");
                foreach (KeyValuePair<string, TagMap> tag in Subtitle)
                    Console.WriteLine($"Subtitle, {tag.Value.PrimaryTool}, {tag.Value.Primary}, {tag.Value.SecondaryTool}, {tag.Value.Secondary}, {tag.Value.TertiaryTool}, {tag.Value.Tertiary}");
            }
        }

        public static bool MatchTracks(MediaInfo mediainfo, MediaInfo mkvmerge, MediaInfo ffprobe)
        {
            if (mediainfo == null || mkvmerge == null || ffprobe == null)
                return false;

            // Verify the track counts match
            if (mediainfo.Video.Count != mkvmerge.Video.Count || mediainfo.Video.Count != ffprobe.Video.Count || 
                mediainfo.Audio.Count != mkvmerge.Audio.Count || mediainfo.Audio.Count != ffprobe.Audio.Count || 
                mediainfo.Subtitle.Count != mkvmerge.Subtitle.Count || mediainfo.Subtitle.Count != ffprobe.Subtitle.Count)
                return false;

            // Verify the track languages match
            if (ffprobe.Video.Where((t, i) => !t.Language.Equals(mediainfo.Video[i].Language, StringComparison.OrdinalIgnoreCase) ||
                                              !t.Language.Equals(mkvmerge.Video[i].Language, StringComparison.OrdinalIgnoreCase)).Any())
            {
                return false;
            }
            if (ffprobe.Audio.Where((t, i) => !t.Language.Equals(mediainfo.Audio[i].Language, StringComparison.OrdinalIgnoreCase) ||
                                              !t.Language.Equals(mkvmerge.Audio[i].Language, StringComparison.OrdinalIgnoreCase)).Any())
            {
                return false;
            }
            return !ffprobe.Subtitle.Where((t, i) => !t.Language.Equals(mediainfo.Subtitle[i].Language, StringComparison.OrdinalIgnoreCase) || 
                                                     !t.Language.Equals(mkvmerge.Subtitle[i].Language, StringComparison.OrdinalIgnoreCase)).Any();
        }
    }
}
