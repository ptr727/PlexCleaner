using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PlexCleaner
{
    public class TagMapDictionary
    {
        public Dictionary<string, TagMap> Video { get; } = new Dictionary<string, TagMap>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, TagMap> Audio { get; } = new Dictionary<string, TagMap>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, TagMap> Subtitle { get; } = new Dictionary<string, TagMap>(StringComparer.OrdinalIgnoreCase);

        public void Add(MediaInfo prime, MediaInfo sec1, MediaInfo sec2)
        {
            if (prime == null)
                throw new ArgumentNullException(nameof(prime));
            if (sec1 == null)
                throw new ArgumentNullException(nameof(sec1));
            if (sec2 == null)
                throw new ArgumentNullException(nameof(sec2));

            // Make sure we can do matching
            Debug.Assert(DoTracksMatch(prime, sec1, sec2));

            // Video
            Add(MediaInfo.GetTrackList(prime.Video), prime.Parser,
                MediaInfo.GetTrackList(sec1.Video), sec1.Parser,
                MediaInfo.GetTrackList(sec2.Video), sec2.Parser, 
                Video);

            // Audio
            Add(MediaInfo.GetTrackList(prime.Audio), prime.Parser,
                MediaInfo.GetTrackList(sec1.Audio), sec1.Parser,
                MediaInfo.GetTrackList(sec2.Audio), sec2.Parser,
                Audio);

            // Subtitle
            Add(MediaInfo.GetTrackList(prime.Subtitle), prime.Parser,
                MediaInfo.GetTrackList(sec1.Subtitle), sec1.Parser,
                MediaInfo.GetTrackList(sec2.Subtitle), sec2.Parser,
                Subtitle);
        }

        private static void Add(List<TrackInfo> prime, MediaTool.ToolType primeType,
                                List<TrackInfo> sec1, MediaTool.ToolType sec1Type,
                                List<TrackInfo> sec2, MediaTool.ToolType sec2Type,
                                Dictionary<string, TagMap> dictionary) 
        {
            for (int i = 0; i < prime.Count; i++)
            {
                // Look for an existing entry
                string key = prime.ElementAt(i).Format;
                if (dictionary.TryGetValue(key, out TagMap tagmap))
                {
                    // Increment the usage count
                    tagmap.Count++;
                }
                else
                {
                    // Add the tagmap
                    tagmap = new TagMap
                    {
                        Primary = key,
                        PrimaryTool = primeType,
                        Secondary = sec1.ElementAt(i).Format,
                        SecondaryTool = sec1Type,
                        Tertiary = sec2.ElementAt(i).Format,
                        TertiaryTool = sec2Type,
                        Count = 1
                    };
                    dictionary.Add(key, tagmap);
                }
            }
        }

        public static bool DoTracksMatch(MediaInfo mediainfo, MediaInfo mkvmerge, MediaInfo ffprobe)
        {
            if (mediainfo == null || mkvmerge == null || ffprobe == null)
                return false;

            // Verify the track counts match
            if (mediainfo.Video.Count != mkvmerge.Video.Count || mediainfo.Video.Count != ffprobe.Video.Count ||
                mediainfo.Audio.Count != mkvmerge.Audio.Count || mediainfo.Audio.Count != ffprobe.Audio.Count ||
                mediainfo.Subtitle.Count != mkvmerge.Subtitle.Count || mediainfo.Subtitle.Count != ffprobe.Subtitle.Count)
                return false;

            // Verify the track languages match
            // FFprobe has bugs with language vs. tag_language, try removing the tags
            if (ffprobe.Video.Where((t, i) =>
                !t.Language.Equals(mediainfo.Video[i].Language, StringComparison.OrdinalIgnoreCase) ||
                !t.Language.Equals(mkvmerge.Video[i].Language, StringComparison.OrdinalIgnoreCase)).Any())
                return false;

            if (ffprobe.Audio.Where((t, i) =>
                !t.Language.Equals(mediainfo.Audio[i].Language, StringComparison.OrdinalIgnoreCase) ||
                !t.Language.Equals(mkvmerge.Audio[i].Language, StringComparison.OrdinalIgnoreCase)).Any())
                return false;

            if (ffprobe.Subtitle.Where((t, i) =>
                !t.Language.Equals(mediainfo.Subtitle[i].Language, StringComparison.OrdinalIgnoreCase) ||
                !t.Language.Equals(mkvmerge.Subtitle[i].Language, StringComparison.OrdinalIgnoreCase)).Any())
                return false;

            return true;
        }

        public void WriteLine()
        {
            foreach ((_, TagMap value) in Video)
                Program.LogFile.LogConsole($"Video, {value.PrimaryTool}, {value.Primary}, {value.SecondaryTool}, {value.Secondary}, {value.TertiaryTool}, {value.Tertiary}, {value.Count}");
            foreach ((_, TagMap value) in Audio)
                Program.LogFile.LogConsole($"Audio, {value.PrimaryTool}, {value.Primary}, {value.SecondaryTool}, {value.Secondary}, {value.TertiaryTool}, {value.Tertiary}, {value.Count}");
            foreach ((_, TagMap value) in Subtitle)
                Program.LogFile.LogConsole($"Subtitle, {value.PrimaryTool}, {value.Primary}, {value.SecondaryTool}, {value.Secondary}, {value.TertiaryTool}, {value.Tertiary}, {value.Count}");
        }
    }
}