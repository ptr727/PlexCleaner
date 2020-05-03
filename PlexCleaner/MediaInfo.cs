using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace PlexCleaner
{
    public class MediaInfo
    {
        public MediaInfo(ParserType parser)
        {
            Parser = parser;
        }

        public enum ParserType { MediaInfo, MkvMerge, FfProbe }
        public ParserType Parser { get; }

        public List<VideoInfo> Video { get; } = new List<VideoInfo>();
        public List<AudioInfo> Audio { get; } = new List<AudioInfo>();
        public List<SubtitleInfo> Subtitle { get; } = new List<SubtitleInfo>();

        public bool HasTags { get; set; }
        public bool HasErrors { get; set; }
        public TimeSpan Duration { get; set; }

        public void WriteLine(string prefix)
        {
            foreach (VideoInfo info in Video)
                Program.LogFile.LogConsole($"{prefix} : {info}");
            foreach (AudioInfo info in Audio)
                Program.LogFile.LogConsole($"{prefix} : {info}");
            foreach (SubtitleInfo info in Subtitle)
                Program.LogFile.LogConsole($"{prefix} : {info}");
        }

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

        public static List<TrackInfo> GetTrackList(List<VideoInfo> list)
        {
            List<TrackInfo> trackList = new List<TrackInfo>();
            trackList.AddRange(list);
            return trackList;
        }

        public static List<TrackInfo> GetTrackList(List<AudioInfo> list)
        {
            List<TrackInfo> trackList = new List<TrackInfo>();
            trackList.AddRange(list);
            return trackList;
        }

        public static List<TrackInfo> GetTrackList(List<SubtitleInfo> list)
        {
            List<TrackInfo> trackList = new List<TrackInfo>();
            trackList.AddRange(list);
            return trackList;
        }

        public int Count => Video.Count + Audio.Count + Subtitle.Count;

        public bool FindUnknownLanguage(out MediaInfo known, out MediaInfo unknown)
        {
            known = new MediaInfo(Parser);
            unknown = new MediaInfo(Parser);

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
            return unknown.Count > 0;
        }

        public bool FindNeedReMux(out MediaInfo keep, out MediaInfo remux)
        {
            keep = new MediaInfo(Parser);
            remux = new MediaInfo(Parser);

            // TODO: Add more granular logic to determine a general error vs. a remux correctable error

            // Video
            foreach (VideoInfo video in Video)
                if (video.HasErrors)
                    remux.Video.Add(video);
                else
                    keep.Video.Add(video);

            // Audio
            foreach (AudioInfo audio in Audio)
                if (audio.HasErrors)
                    remux.Audio.Add(audio);
                else
                    keep.Audio.Add(audio);

            // Subtitle
            foreach (SubtitleInfo subtitle in Subtitle)
                if (subtitle.HasErrors)
                    remux.Subtitle.Add(subtitle);
                else
                    keep.Subtitle.Add(subtitle);

            // Set the correct state on all the objects
            remux.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.ReMux);
            keep.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.Keep);

            // Return true on any match
            return remux.Count > 0;
        }

        public bool FindNeedDeInterlace(out MediaInfo keep, out MediaInfo deinterlace)
        {
            keep = new MediaInfo(Parser);
            deinterlace = new MediaInfo(Parser);

            // No filter for audio or subtitle
            keep.Subtitle.Clear();
            keep.Subtitle.AddRange(Subtitle);
            keep.Audio.Clear();
            keep.Audio.AddRange(Audio);

            foreach (VideoInfo video in Video)
                if (video.Interlaced)
                    deinterlace.Video.Add(video);
                else
                    keep.Video.Add(video);

            // Set the correct state on all the objects
            deinterlace.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.ReMux);
            keep.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.Keep);

            // Return true on any match
            return deinterlace.Count > 0;
        }

        public bool FindUnwantedLanguage(HashSet<string> languages, out MediaInfo keep, out MediaInfo remove)
        {
            if (languages == null)
                throw new ArgumentNullException(nameof(languages));

            keep = new MediaInfo(Parser);
            remove = new MediaInfo(Parser);

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
            return remove.Count > 0;
        }

        public bool FindNeedReEncode(List<VideoInfo> reencodevideo, HashSet<string> reencodeaudio, out MediaInfo keep, out MediaInfo reencode)
        {
            if (reencodevideo == null)
                throw new ArgumentNullException(nameof(reencodevideo));
            if (reencodeaudio == null)
                throw new ArgumentNullException(nameof(reencodeaudio));

            // Filter logic values are based FFprobe attributes
            Debug.Assert(Parser == ParserType.FfProbe);

            keep = new MediaInfo(Parser);
            reencode = new MediaInfo(Parser);

            // Match video codecs from the reencode list
            foreach (VideoInfo video in Video)
            {
                // See if the video track matches any of the re-encode specs
                if (reencodevideo.Any(item => video.CompareVideo(item)))
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

            // If we are encoding audio, the video track must be h264 or h265 else we get ffmpeg encoding errors
            // [matroska @ 00000195b3585c80] Timestamps are unset in a packet for stream 0.
            // [matroska @ 00000195b3585c80] Can't write packet with unknown timestamp
            // av_interleaved_write_frame(): Invalid argument
            if (reencode.Audio.Count > 0 &&
                keep.Video.Any(item => 
                    !item.Format.Equals("h264", StringComparison.OrdinalIgnoreCase) &&
                    !item.Format.Equals("hevc", StringComparison.OrdinalIgnoreCase)))
            {
                // Add video to the reencode list
                reencode.Video.AddRange(keep.Video);
                keep.Video.Clear();
            }

            // Keep all subtitle tracks
            keep.Subtitle.Clear();
            keep.Subtitle.AddRange(Subtitle);

            // Set the correct state on all the objects
            reencode.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.ReEncode);
            keep.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.Keep);

            // Return true on any match
            return reencode.Count > 0;
        }

        public bool FindDuplicateTracks(List<string> codecs, out MediaInfo keep, out MediaInfo remove)
        {
            if (codecs == null)
                throw new ArgumentNullException(nameof(codecs));

            // If we have one of each track type then keep all tracks
            keep = null;
            remove = null;
            if (Video.Count <= 1 && Audio.Count <= 1 && Subtitle.Count <= 1)
            {
                return false;
            }

            // Find duplicates
            keep = new MediaInfo(Parser);
            remove = new MediaInfo(Parser);
            FindDuplicateVideoTracks(keep, remove);
            FindDuplicateSubtitleTracks(keep, remove);
            FindDuplicateAudioTracks(codecs, keep, remove);

            // Set the correct state on all the objects
            remove.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.Remove);
            keep.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.Keep);

            // Return true on any match
            return remove.Count > 0;
        }

        private void FindDuplicateVideoTracks(MediaInfo keep, MediaInfo remove)
        {
            // Skip if just one track
            if (Video.Count <= 1)
            {
                keep.Video.AddRange(Video);
                return;
            }

            // Create a list of all the languages
            HashSet<string> languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (VideoInfo video in Video)
            {
                languages.Add(video.Language);
            }

            // We have nothing to do if the track count equals the language count
            if (Video.Count == languages.Count)
            {
                keep.Video.AddRange(Video);
                return;
            }

            // Keep one instance of each language
            foreach (string language in languages)
            {
                // Use the first default track
                VideoInfo video = Video.Find(item =>
                    item.Language.Equals(language, StringComparison.OrdinalIgnoreCase) &&
                    item.Default);

                // If nothing found, use the first track
                video ??= Video.Find(item => item.Language.Equals(language, StringComparison.OrdinalIgnoreCase));

                // Keep it
                keep.Video.Add(video);
            }
            Debug.Assert(keep.Video.Count > 0);

            // Add all other items to the remove list
            remove.Video.AddRange(Video.Except(keep.Video));
        }

        private void FindDuplicateSubtitleTracks(MediaInfo keep, MediaInfo remove)
        {
            // Skip if just one track
            if (Subtitle.Count <= 1)
            {
                keep.Subtitle.AddRange(Subtitle);
                return;
            }

            // Create a list of all the languages
            HashSet<string> languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SubtitleInfo subtitle in Subtitle)
            {
                languages.Add(subtitle.Language);
            }

            // We have nothing to do if the track count equals the language count
            if (Subtitle.Count == languages.Count)
            {
                keep.Subtitle.AddRange(Subtitle);
                return;
            }

            // Keep one normal and one forced instance of each language 
            // TODO : Add a SDH processing option
            foreach (string language in languages)
            {
                // Find non-forced track

                // Use the first default, non-SDH, non-forced track
                SubtitleInfo subtitle = Subtitle.Find(item =>
                    item.Language.Equals(language, StringComparison.OrdinalIgnoreCase) &&
                    item.Default &&
                    !item.Title.Contains("SDH", StringComparison.OrdinalIgnoreCase) &&
                    !(item.Forced || item.Title.Contains("Forced", StringComparison.OrdinalIgnoreCase)));

                // If nothing found, first non-SDH, non-forced track
                subtitle ??= Subtitle.Find(item =>
                    item.Language.Equals(language, StringComparison.OrdinalIgnoreCase) &&
                    !item.Title.Contains("SDH", StringComparison.OrdinalIgnoreCase) &&
                    !(item.Forced || item.Title.Contains("Forced", StringComparison.OrdinalIgnoreCase)));

                // If nothing found, non-forced track
                subtitle ??= Subtitle.Find(item =>
                    item.Language.Equals(language, StringComparison.OrdinalIgnoreCase) &&
                    !(item.Forced || item.Title.Contains("Forced", StringComparison.OrdinalIgnoreCase)));

                // Add non-forced track
                if (subtitle != null)
                    keep.Subtitle.Add(subtitle);

                // Find forced track

                // Use the first default, non-SDH, forced track
                subtitle = Subtitle.Find(item =>
                    item.Language.Equals(language, StringComparison.OrdinalIgnoreCase) &&
                    item.Default &&
                    !item.Title.Contains("SDH", StringComparison.OrdinalIgnoreCase) &&
                    (item.Forced || item.Title.Contains("Forced", StringComparison.OrdinalIgnoreCase)));

                // If nothing found, first non-SDH, forced track
                subtitle ??= Subtitle.Find(item =>
                    item.Language.Equals(language, StringComparison.OrdinalIgnoreCase) &&
                    !item.Title.Contains("SDH", StringComparison.OrdinalIgnoreCase) &&
                    (item.Forced || item.Title.Contains("Forced", StringComparison.OrdinalIgnoreCase)));

                // If nothing found, forced track
                subtitle ??= Subtitle.Find(item =>
                    item.Language.Equals(language, StringComparison.OrdinalIgnoreCase) &&
                    (item.Forced || item.Title.Contains("Forced", StringComparison.OrdinalIgnoreCase)));

                // Add forced track
                if (subtitle != null)
                    keep.Subtitle.Add(subtitle);
            }
            Debug.Assert(keep.Subtitle.Count > 0);

            // Add all other items to the remove list
            remove.Subtitle.AddRange(Subtitle.Except(keep.Subtitle));
        }

        private void FindDuplicateAudioTracks(List<string> codecs, MediaInfo keep, MediaInfo remove)
        {
            // Skip if just one track
            if (Audio.Count <= 1)
            {
                keep.Audio.AddRange(Audio);
                return;
            }

            // Create a list of all the languages
            HashSet<string> languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AudioInfo audio in Audio)
            {
                languages.Add(audio.Language);
            }

            // We have nothing to do if the track count equals the language count
            if (Audio.Count == languages.Count)
            {
                keep.Audio.AddRange(Audio);
                return;
            }

            // Keep one instance of each language
            foreach (string language in languages)
            {
                // Use the first default non-commentary track
                AudioInfo audio = Audio.Find(item =>
                    item.Language.Equals(language, StringComparison.OrdinalIgnoreCase) &&
                    item.Default &&
                    !item.Title.Contains("Commentary", StringComparison.OrdinalIgnoreCase));

                // If nothing found, use the first default track
                audio ??= Audio.Find(item =>
                    item.Language.Equals(language, StringComparison.OrdinalIgnoreCase) &&
                    item.Default);

                // If nothing found, use the preferred codec track
                AudioInfo preferred = FindPreferredAudio(codecs, language);
                audio ??= preferred;

                // If nothing found, use the first track
                audio ??= Audio.Find(item => item.Language.Equals(language, StringComparison.OrdinalIgnoreCase));

                // The default track is not always the best track
                if (preferred != null && 
                    !audio.Format.Equals(preferred.Format, StringComparison.OrdinalIgnoreCase))
                {
                    audio = preferred;
                }

                // Keep it
                keep.Audio.Add(audio);
            }
            Debug.Assert(keep.Audio.Count > 0);

            // Add all other items to the remove list
            remove.Audio.AddRange(Audio.Except(keep.Audio));
        }

        private AudioInfo FindPreferredAudio(List<string> codecs, string language)
        {
            // Iterate through the codecs in order of preference
            AudioInfo audio = null;
            foreach (string codec in codecs)
            {
                // Match by language and format
                audio = Audio.Find(item =>
                    item.Language.Equals(language, StringComparison.OrdinalIgnoreCase) &&
                    item.Format.Equals(codec, StringComparison.OrdinalIgnoreCase));
                if (audio != null)
                    break;
            }
            return audio;
        }

        public void RemoveTracks(MediaInfo removeTracks)
        {
            if (removeTracks == null)
                throw new ArgumentNullException(nameof(removeTracks));

            // Only between same types else value comparison logic does not work
            Debug.Assert(Parser == removeTracks.Parser);

            // Remove all matching items by value
            Video.RemoveAll(item => removeTracks.Video.Contains(item));
            Audio.RemoveAll(item => removeTracks.Audio.Contains(item));
            Subtitle.RemoveAll(item => removeTracks.Subtitle.Contains(item));
        }

        public void AddTracks(MediaInfo addTracks)
        {
            if (addTracks == null)
                throw new ArgumentNullException(nameof(addTracks));

            // Only between same types else value comparison logic does not work
            Debug.Assert(Parser == addTracks.Parser);

            // Add all items that are not already in the collection by value
            Video.AddRange(addTracks.Video.Where(item => !Video.Contains(item)));
            Audio.AddRange(addTracks.Audio.Where(item => !Audio.Contains(item)));
            Subtitle.AddRange(addTracks.Subtitle.Where(item => !Subtitle.Contains(item)));
        }

        public static bool GetMediaInfo(FileInfo fileinfo, out MediaInfo ffprobe, out MediaInfo mkvmerge, out MediaInfo mediainfo)
        {
            ffprobe = null;
            mkvmerge = null;
            mediainfo = null;

            return GetMediaInfo(fileinfo, ParserType.FfProbe, out ffprobe) &&
                   GetMediaInfo(fileinfo, ParserType.MkvMerge, out mkvmerge) &&
                   GetMediaInfo(fileinfo, ParserType.MediaInfo, out mediainfo);
        }

        public static bool GetMediaInfo(FileInfo fileinfo, ParserType parser, out MediaInfo mediainfo)
        {
            if (fileinfo == null)
                throw new ArgumentNullException(nameof(fileinfo));

            // Use the specified stream parser tool
            return parser switch
            {
                ParserType.MediaInfo => MediaInfoTool.GetMediaInfo(fileinfo.FullName, out mediainfo),
                ParserType.MkvMerge => MkvTool.GetMkvInfo(fileinfo.FullName, out mediainfo),
                ParserType.FfProbe => FfMpegTool.GetFfProbeInfo(fileinfo.FullName, out mediainfo),
                _ => throw new NotImplementedException()
            };
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (VideoInfo info in Video) sb.AppendLine(info.ToString());
            foreach (AudioInfo info in Audio) sb.AppendLine(info.ToString());
            foreach (SubtitleInfo info in Subtitle) sb.AppendLine(info.ToString());
            return sb.ToString();
        }
    }
}