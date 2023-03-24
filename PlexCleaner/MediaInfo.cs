using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Serilog;

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
        Video.ForEach(item => item.WriteLine(prefix));
        Audio.ForEach(item => item.WriteLine(prefix));
        Subtitle.ForEach(item => item.WriteLine(prefix));
    }

    public List<TrackInfo> GetTrackList()
    {
        // Combine all tracks
        List<TrackInfo> trackLick = new();
        trackLick.AddRange(Video);
        trackLick.AddRange(Audio);
        trackLick.AddRange(Subtitle);

        // Sort items by Id
        return trackLick.OrderBy(item => item.Id).ToList();
    }

    // Combined track count
    public int Count => Video.Count + Audio.Count + Subtitle.Count;

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
    public void RemoveCoverArt()
    {
        // Video tracks only
        if (Video.Count == 0)
        {
            return;
        }

        // TODO: Find a more deterministic way to identify cover art and thumbnail clips
        // Some media includes a thumbnail preview and main video tracks
        // Some media includes static cover art

        // Find all tracks with cover art
        var coverArtTracks = Video.FindAll(item => TrackInfo.MatchCoverArt(item.Codec));

        // Are all tracks cover art
        if (Video.Count == coverArtTracks.Count)
        {
            Log.Logger.Error("All video tracks are cover art");
            return;
        }

        // Remove all cover art tracks
        foreach (var item in coverArtTracks)
        {
            Log.Logger.Warning("Ignoring cover art video track : {Codec}", item.Codec);
            Video.Remove(item);
        }
    }

    public List<TrackInfo> MatchMediaInfoToMkvMerge(List<TrackInfo> mediaInfoTrackList)
    {
        // This only works for MkvMerge
        // TODO: Convert to more generic function, but for now only MediaInfo to MkvMerge is required
        Debug.Assert(Parser == MediaTool.ToolType.MkvMerge);

        // Get a MkvMerge track list
        var mkvMergeTrackList = GetTrackList();

        // Match by MediaInfo.Number == MkvMerge.Number
        List<TrackInfo> matchedTrackList = new();
        mediaInfoTrackList.ForEach(mediaInfoItem => matchedTrackList.Add(mkvMergeTrackList.Find(mkvMergeItem => mkvMergeItem.Number == mediaInfoItem.Number)));

        // Make sure all items matched
        Debug.Assert(mediaInfoTrackList.Count == matchedTrackList.Count);

        return matchedTrackList;
    }
}
