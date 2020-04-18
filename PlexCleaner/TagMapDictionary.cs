using System;
using System.Collections.Generic;
using System.Linq;

namespace PlexCleaner
{
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
            if (prime == null)
                throw new ArgumentNullException(nameof(prime));
            if (sec1 == null)
                throw new ArgumentNullException(nameof(sec1));
            if (sec2 == null)
                throw new ArgumentNullException(nameof(sec2));

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

                tag.Primary ??= "null";


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

                tag.Primary ??= "null";

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

                tag.Primary ??= "null";

                if (!Subtitle.ContainsKey(tag.Primary))
                    Subtitle.Add(tag.Primary, tag);
            }
        }

        public void WriteLine()
        {
            foreach ((_, TagMap value) in Video)
                Console.WriteLine($"Video, {value.PrimaryTool}, {value.Primary}, {value.SecondaryTool}, {value.Secondary}, {value.TertiaryTool}, {value.Tertiary}");
            foreach ((_, TagMap value) in Audio)
                Console.WriteLine($"Audio, {value.PrimaryTool}, {value.Primary}, {value.SecondaryTool}, {value.Secondary}, {value.TertiaryTool}, {value.Tertiary}");
            foreach ((_, TagMap value) in Subtitle)
                Console.WriteLine($"Subtitle, {value.PrimaryTool}, {value.Primary}, {value.SecondaryTool}, {value.Secondary}, {value.TertiaryTool}, {value.Tertiary}");
        }
    }
}