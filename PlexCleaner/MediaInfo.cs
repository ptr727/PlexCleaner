using System;
using System.Collections.Generic;
using System.Linq;
using InsaneGenius.Utilities;

namespace PlexCleaner
{
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
                    string.IsNullOrEmpty(subtitle.MuxingMode))
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
            if (languages == null)
                throw new ArgumentNullException(nameof(languages));

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
            if (reencodevideo == null)
                throw new ArgumentNullException(nameof(reencodevideo));
            if (reencodeaudio == null)
                throw new ArgumentNullException(nameof(reencodeaudio));

            keep = new MediaInfo();
            reencode = new MediaInfo();

            // Match video codecs from the reencode list
            foreach (VideoInfo video in Video)
            {
                // See if the track matches any of the re-encode specs
                bool match = false;
                foreach (VideoInfo compare in reencodevideo.Where(compare => video.CompareVideo(compare)))
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

        public bool IsVideoInterlaced()
        {
            return Video.Any(video => video.IsInterlaced());
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
                Parser = ParserType.MediaInfo
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
                Parser = ParserType.MkvMerge
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
                Parser = ParserType.FfProbe
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
                        if (!string.IsNullOrEmpty(stream.Tags?.MimeType))
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