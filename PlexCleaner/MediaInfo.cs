using System;
using System.Collections.Generic;
using System.Linq;
using InsaneGenius.Utilities;

namespace PlexCleaner
{
    public static partial class Info
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

            public bool IsVideoInterlaced()
            {
                foreach (VideoInfo video in Video)
                    if (video.IsInterlaced())
                        return true;
                return false;
            }
        }
    }
}