using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace PlexCleaner;

public class TagMapSet
{
    private Dictionary<string, TagMap> Video { get; } = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, TagMap> Audio { get; } = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, TagMap> Subtitle { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void Add(MediaProps prime, MediaProps sec1, MediaProps sec2)
    {
        if (!DoTracksMatch(prime, sec1, sec2))
        {
            return;
        }

        // Video
        Add(prime.Video, prime.Parser, sec1.Video, sec1.Parser, sec2.Video, sec2.Parser, Video);

        // Audio
        Add(prime.Audio, prime.Parser, sec1.Audio, sec1.Parser, sec2.Audio, sec2.Parser, Audio);

        // Subtitle
        Add(
            prime.Subtitle,
            prime.Parser,
            sec1.Subtitle,
            sec1.Parser,
            sec2.Subtitle,
            sec2.Parser,
            Subtitle
        );
    }

    private static void Add(
        IReadOnlyCollection<TrackProps> prime,
        MediaTool.ToolType primeType,
        IReadOnlyCollection<TrackProps> sec1,
        MediaTool.ToolType sec1Type,
        IReadOnlyCollection<TrackProps> sec2,
        MediaTool.ToolType sec2Type,
        Dictionary<string, TagMap> dictionary
    )
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
                    Count = 1,
                };
                dictionary.Add(key, tagmap);
            }
        }
    }

    private static bool DoTracksMatch(MediaProps mediaInfo, MediaProps mkvMerge, MediaProps ffProbe)
    {
        // Verify the track counts match
        if (
            mediaInfo.Video.Count != mkvMerge.Video.Count
            || mediaInfo.Video.Count != ffProbe.Video.Count
            || mediaInfo.Audio.Count != mkvMerge.Audio.Count
            || mediaInfo.Audio.Count != ffProbe.Audio.Count
            || mediaInfo.Subtitle.Count != mkvMerge.Subtitle.Count
            || mediaInfo.Subtitle.Count != ffProbe.Subtitle.Count
        )
        {
            Log.Error("Track counts do not match:");
            mediaInfo.WriteLine();
            mkvMerge.WriteLine();
            ffProbe.WriteLine();
            return false;
        }

        // TODO: Verify the track languages match

        return true;
    }

    public void WriteLine()
    {
        foreach ((_, TagMap value) in Video)
        {
            Log.Information(
                "Video, {PrimaryTool}, {Primary}, {SecondaryTool}, {Secondary}, {TertiaryTool}, {Tertiary}, {Count}",
                value.PrimaryTool,
                value.Primary,
                value.SecondaryTool,
                value.Secondary,
                value.TertiaryTool,
                value.Tertiary,
                value.Count
            );
        }

        foreach ((_, TagMap value) in Audio)
        {
            Log.Information(
                "Audio, {PrimaryTool}, {Primary}, {SecondaryTool}, {Secondary}, {TertiaryTool}, {Tertiary}, {Count}",
                value.PrimaryTool,
                value.Primary,
                value.SecondaryTool,
                value.Secondary,
                value.TertiaryTool,
                value.Tertiary,
                value.Count
            );
        }

        foreach ((_, TagMap value) in Subtitle)
        {
            Log.Information(
                "Subtitle, {PrimaryTool}, {Primary}, {SecondaryTool}, {Secondary}, {TertiaryTool}, {Tertiary}, {Count}",
                value.PrimaryTool,
                value.Primary,
                value.SecondaryTool,
                value.Secondary,
                value.TertiaryTool,
                value.Tertiary,
                value.Count
            );
        }
    }
}
