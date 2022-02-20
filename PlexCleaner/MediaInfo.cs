using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PlexCleaner;

public class MediaInfo
{
    public MediaInfo(MediaTool.ToolType parser)
    {
        Parser = parser;
    }

    // MkvMerge, FfProbe, MediaInfo
    public MediaTool.ToolType Parser { get; }

    public List<VideoInfo> Video { get; } = new();
    public List<AudioInfo> Audio { get; } = new();
    public List<SubtitleInfo> Subtitle { get; } = new();

    public bool HasTags { get; set; }
    public bool HasErrors { get; set; }
    public TimeSpan Duration { get; set; }
    public string Container { get; set; }
    public int Attachments { get; set; }
    public int Chapters { get; set; }


    public void WriteLine(string prefix)
    {
        foreach (VideoInfo info in Video)
        {
            info.WriteLine(prefix);
        }

        foreach (AudioInfo info in Audio)
        {
            info.WriteLine(prefix);
        }

        foreach (SubtitleInfo info in Subtitle)
        {
            info.WriteLine(prefix);
        }
    }

    public List<TrackInfo> GetTrackList()
    {
        // Combine all tracks
        List<TrackInfo> combined = new();
        combined.AddRange(Video);
        combined.AddRange(Audio);
        combined.AddRange(Subtitle);

        // Sort items by Id
        List<TrackInfo> ordered = combined.OrderBy(item => item.Id).ToList();

        return ordered;
    }

    public static List<TrackInfo> GetTrackList(IEnumerable<VideoInfo> list)
    {
        List<TrackInfo> trackList = new();
        trackList.AddRange(list);
        return trackList;
    }

    public static List<TrackInfo> GetTrackList(IEnumerable<AudioInfo> list)
    {
        List<TrackInfo> trackList = new();
        trackList.AddRange(list);
        return trackList;
    }

    public static List<TrackInfo> GetTrackList(IEnumerable<SubtitleInfo> list)
    {
        List<TrackInfo> trackList = new();
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
        {
            if (video.IsLanguageUnknown())
            {
                unknown.Video.Add(video);
            }
            else
            {
                known.Video.Add(video);
            }
        }

        // Audio
        foreach (AudioInfo audio in Audio)
        {
            if (audio.IsLanguageUnknown())
            {
                unknown.Audio.Add(audio);
            }
            else
            {
                known.Audio.Add(audio);
            }
        }

        // Subtitle
        foreach (SubtitleInfo subtitle in Subtitle)
        {
            if (subtitle.IsLanguageUnknown())
            {
                unknown.Subtitle.Add(subtitle);
            }
            else
            {
                known.Subtitle.Add(subtitle);
            }
        }

        // Return true on any match
        return unknown.Count > 0;
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

        // Add all tracks with the interlaced flag set
        foreach (VideoInfo video in Video)
        {
            if (video.Interlaced)
            {
                deinterlace.Video.Add(video);
            }
            else
            {
                keep.Video.Add(video);
            }
        }

        // Set the correct state on all the objects
        deinterlace.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.DeInterlace);
        keep.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.Keep);

        // Return true on any match
        return deinterlace.Count > 0;
    }

    public bool FindUnwantedLanguage(HashSet<string> languages, List<string> preferredAudioFormats, out MediaInfo keep, out MediaInfo remove)
    {
        if (languages == null)
        {
            throw new ArgumentNullException(nameof(languages));
        }

        keep = new MediaInfo(Parser);
        remove = new MediaInfo(Parser);

        // Note, foreign films may not have any matching language tracks

        // Keep all video tracks that match a language
        HashSet<string> videoLanguages = new();
        foreach (VideoInfo video in Video)
        {
            // Available languages
            videoLanguages.Add(video.Language);

            // Keep or remove
            if (languages.Contains(video.Language))
            {
                keep.Video.Add(video);
            }
            else
            {
                remove.Video.Add(video);
            }
        }

        // No language matching video tracks
        if (keep.Video.Count == 0 && Video.Count > 0)
        {
            // Use the first track
            VideoInfo info = Video.First();

            Log.Logger.Warning("No video track matching requested language : {Available} != {Languages}, {Selected}", videoLanguages, languages, info.Language);
            keep.Video.Add(info);
            remove.Video.Remove(info);
        }

        // Keep all audio tracks that match a language
        HashSet<string> audioLanguages = new();
        foreach (AudioInfo audio in Audio)
        {
            // Available languages
            audioLanguages.Add(audio.Language);

            // Keep or remove
            if (languages.Contains(audio.Language))
            {
                keep.Audio.Add(audio);
            }
            else
            {
                remove.Audio.Add(audio);
            }
        }

        // No language matching audio tracks
        bool audioMatch = false;
        if (keep.Audio.Count == 0 && Audio.Count > 0)
        {
            // Use the preferred audio codec track or the first track
            AudioInfo info = FindPreferredAudio(preferredAudioFormats) ?? Audio.First();

            Log.Logger.Warning("No audio track matching requested language : {Available} != {Languages}, {Selected}", audioLanguages, languages, info.Language);
            keep.Audio.Add(info);
            remove.Audio.Remove(info);
        }
        else
        {
            // One or more audio tracks matched
            audioMatch = true;
        }

        // Keep all subtitle tracks that match a language
        HashSet<string> subtitleLanguages = new();
        foreach (SubtitleInfo subtitle in Subtitle)
        {
            // Available languages
            subtitleLanguages.Add(subtitle.Language);

            // Keep or remove
            if (languages.Contains(subtitle.Language))
            {
                keep.Subtitle.Add(subtitle);
            }
            else
            {
                remove.Subtitle.Add(subtitle);
            }
        }

        // No language matching subtitle tracks
        if (keep.Subtitle.Count == 0 && Subtitle.Count > 0)
        {
            Log.Logger.Warning("No subtitle track matching requested language : {Available} != {Languages}", subtitleLanguages, languages);
        }

        // No audio match and no subtitle match, foreign film with no matching subtitles
        if (keep.Subtitle.Count == 0 && !audioMatch)
        {
            Log.Logger.Warning("No audio or subtitle track matching requested language : {Available} != {Languages}", audioLanguages, languages);
        }

        // Set the correct state on all the objects
        remove.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.Remove);
        keep.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.Keep);

        // Return true on any match
        return remove.Count > 0;
    }

    public bool FindNeedReEncode(List<VideoInfo> reencodevideo, HashSet<string> reencodeaudio, out MediaInfo keep, out MediaInfo reencode)
    {
        if (reencodevideo == null)
        {
            throw new ArgumentNullException(nameof(reencodevideo));
        }

        if (reencodeaudio == null)
        {
            throw new ArgumentNullException(nameof(reencodeaudio));
        }

        // Filter logic values are based FFprobe attributes
        Debug.Assert(Parser == MediaTool.ToolType.FfProbe);

        keep = new MediaInfo(Parser);
        reencode = new MediaInfo(Parser);

        // Match video codecs from the reencode list
        foreach (VideoInfo video in Video)
        {
            // See if the video track matches any of the re-encode specs
            if (reencodevideo.Any(item => video.CompareVideo(item)))
            {
                reencode.Video.Add(video);
            }
            else
            {
                keep.Video.Add(video);
            }
        }

        // Match audio codecs from the reencode list
        foreach (AudioInfo audio in Audio)
        {
            // Re-encode or passthrough
            // TODO: Add wildcard support for e.g. *pcm matching any pcm format 
            if (reencodeaudio.Contains(audio.Format))
            {
                reencode.Audio.Add(audio);
            }
            else
            {
                keep.Audio.Add(audio);
            }
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
        {
            throw new ArgumentNullException(nameof(codecs));
        }

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
        HashSet<string> languages = new(StringComparer.OrdinalIgnoreCase);
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
        foreach (VideoInfo video in from language in languages
                                    let video = Video.Find(item => item.Language.Equals(language, StringComparison.OrdinalIgnoreCase) && item.Default)
                                    select video ?? Video.Find(item => item.Language.Equals(language, StringComparison.OrdinalIgnoreCase)))
        {
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
        HashSet<string> languages = new(StringComparer.OrdinalIgnoreCase);
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
            {
                keep.Subtitle.Add(subtitle);
            }

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
            {
                keep.Subtitle.Add(subtitle);
            }
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
        HashSet<string> languages = new(StringComparer.OrdinalIgnoreCase);
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
            Debug.Assert(audio != null);
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
            {
                break;
            }
        }
        return audio;
    }

    private AudioInfo FindPreferredAudio(List<string> codecs)
    {
        // Iterate through the codecs in order of preference
        AudioInfo audio = null;
        foreach (string codec in codecs)
        {
            // Match by format
            audio = Audio.Find(item => item.Format.Equals(codec, StringComparison.OrdinalIgnoreCase));
            if (audio != null)
            {
                break;
            }
        }
        return audio;
    }

    public void RemoveTracks(MediaInfo removeTracks)
    {
        if (removeTracks == null)
        {
            throw new ArgumentNullException(nameof(removeTracks));
        }

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
        {
            throw new ArgumentNullException(nameof(addTracks));
        }

        // Only between same types else value comparison logic does not work
        Debug.Assert(Parser == addTracks.Parser);

        // Add all items that are not already in the collection by value
        Video.AddRange(addTracks.Video.Where(item => !Video.Contains(item)));
        Audio.AddRange(addTracks.Audio.Where(item => !Audio.Contains(item)));
        Subtitle.AddRange(addTracks.Subtitle.Where(item => !Subtitle.Contains(item)));
    }

    public static bool GetMediaInfo(FileInfo fileInfo, out MediaInfo ffprobe, out MediaInfo mkvmerge, out MediaInfo mediainfo)
    {
        ffprobe = null;
        mkvmerge = null;
        mediainfo = null;

        return GetMediaInfo(fileInfo, MediaTool.ToolType.FfProbe, out ffprobe) &&
               GetMediaInfo(fileInfo, MediaTool.ToolType.MkvMerge, out mkvmerge) &&
               GetMediaInfo(fileInfo, MediaTool.ToolType.MediaInfo, out mediainfo);
    }

    public static bool GetMediaInfo(FileInfo fileInfo, MediaTool.ToolType parser, out MediaInfo mediainfo)
    {
        if (fileInfo == null)
        {
            throw new ArgumentNullException(nameof(fileInfo));
        }

        // Use the specified stream parser tool
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        return parser switch
        {
            MediaTool.ToolType.MediaInfo => Tools.MediaInfo.GetMediaInfo(fileInfo.FullName, out mediainfo),
            MediaTool.ToolType.MkvMerge => Tools.MkvMerge.GetMkvInfo(fileInfo.FullName, out mediainfo),
            MediaTool.ToolType.FfProbe => Tools.FfProbe.GetFfProbeInfo(fileInfo.FullName, out mediainfo),
            _ => throw new NotImplementedException()
        };
    }

    public static bool IsUsefulTrackTitle(string title)
    {
        // Does the track have a useful title
        return UsefulTitles.Any(useful => title.Equals(useful, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsTagTitle(string title)
    {
        // Empty is not a tag
        if (string.IsNullOrEmpty(title))
        {
            return false;
        }

        // Useful is not a tag
        return !IsUsefulTrackTitle(title);
    }

    public static void RemoveCoverArt(MediaInfo mediaInfo)
    {
        // TODO: Find a more deterministic way to identify cover art and thumbnail clips
        // Some media includes a thumbnail preview and main video tracks
        // Some media includes static cover art

        // If there is none or one video track leave it as is
        if (mediaInfo.Video.Count == 0)
        {
            return;
        }

        if (mediaInfo.Video.Count == 1)
        {
            // Warn if the only video track is possibly cover art
            if (MatchCoverArt(mediaInfo.Video.First().Codec))
            {
                Log.Logger.Warning("Covert art match on only video track : {Codec}", mediaInfo.Video.First().Codec);
            }

            return;
        }

        // Keep only the non cover art tracks
        List<VideoInfo> keepVideo = new();
        foreach (VideoInfo videoInfo in mediaInfo.Video)
        {
            // Skip cover tracks
            if (MatchCoverArt(videoInfo.Codec))
            {
                Log.Logger.Warning("Ignoring covert art video track : {Codec}", videoInfo.Codec);
                continue;
            }

            // Keep the track
            keepVideo.Add(videoInfo);
        }

        // If all the tracks are cover art leave them as is
        if (keepVideo.Count == 0)
        {
            Log.Logger.Warning("All video tracks are cover art");
            return;
        }

        // Keep only the non cover art tracks
        mediaInfo.Video.Clear();
        mediaInfo.Video.AddRange(keepVideo);
    }

    private static bool MatchCoverArt(string codec)
    {
        return CoverArtFormat.Any(cover => codec.Contains(cover, StringComparison.OrdinalIgnoreCase));
    }

    // Cover art and thumbnail formats
    private static readonly string[] CoverArtFormat = { "jpg", "jpeg", "mjpeg", "png" };
    // Not so useful track titles
    private static readonly string[] UsefulTitles = { "SDH", "Commentary", "Forced" };
}