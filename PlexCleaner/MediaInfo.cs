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

    public MediaInfo Clone()
    {
        // Shallow copy
        var clonedInfo = (MediaInfo)MemberwiseClone();

        // Create new collections containing the old items
        List<VideoInfo> newVideo = new();
        newVideo.AddRange(Video);
        clonedInfo.Video = newVideo;
        List<AudioInfo> newAudio = new();
        newAudio.AddRange(Audio);
        clonedInfo.Audio = newAudio;
        List<SubtitleInfo> newSubtitle = new();
        newSubtitle.AddRange(Subtitle);
        clonedInfo.Subtitle = newSubtitle;

        return clonedInfo;
    }

    // MkvMerge, FfProbe, MediaInfo
    public MediaTool.ToolType Parser { get; }

    public List<VideoInfo> Video { get; private set; } = new();
    public List<AudioInfo> Audio { get; private set; } = new();
    public List<SubtitleInfo> Subtitle { get; private set; } = new();

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
        // No video tracks nothing to do
        if (Video.Count == 0)
        {
            return;
        }

        // Find all tracks with cover art
        var coverArtTracks = Video.FindAll(item => item.MatchCoverArt());

        // Are all tracks cover art
        if (Video.Count == coverArtTracks.Count)
        {
            Log.Logger.Error("All video tracks are cover art : {Parser}", Parser);
        }

        // Remove all cover art tracks
        foreach (var item in coverArtTracks)
        {
            Log.Logger.Warning("Ignoring cover art video track : {Parser}:{Format}:{Codec}", Parser, item.Format, item.Codec);
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

    public bool VerifyTrackOrder(MediaInfo mediaInfo)
    {
        // Verify that this MediaInfo matches the presented MediaInfo
        // Used to verify that the track numbers for MkvMerge remains the same prior to and after ffmpeg and handbrake
        // This logic works for MkvMerge only
        Debug.Assert(Parser == MediaTool.ToolType.MkvMerge);
        Debug.Assert(mediaInfo.Parser == MediaTool.ToolType.MkvMerge);

        // Track counts
        if (Count != mediaInfo.Count)
        { 
            return false;
        }

        // Get track items as list
        var thisTrackList = GetTrackList();
        var thatTrackList = mediaInfo.GetTrackList();
        foreach (var thisItem in thisTrackList)
        {
            // Find the matching item by matroska header number
            var thatItem = thatTrackList.Find(item => item.Number == thisItem.Number);
            if (thatItem == null)
            {
                return false;
            }

            // The types have to match
            if (thisItem.GetType() != thatItem.GetType())
            {
                return false;
            }

            // The ISO693-3 language has to match
            if (!thisItem.Language.Equals(thatItem.Language))
            {
                return false;
            }

            // Other properties may have changed during encoding
        }

        // Done
        return true;
    }
}
