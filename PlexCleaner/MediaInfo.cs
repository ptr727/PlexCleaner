using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Serilog;

namespace PlexCleaner;

public class MediaInfo(MediaTool.ToolType parser)
{
    public MediaInfo Clone()
    {
        // Shallow copy
        var clonedInfo = (MediaInfo)MemberwiseClone();

        // Create new collections containing the old items
        List<VideoInfo> newVideo = [];
        newVideo.AddRange(Video);
        clonedInfo.Video = newVideo;
        List<AudioInfo> newAudio = [];
        newAudio.AddRange(Audio);
        clonedInfo.Audio = newAudio;
        List<SubtitleInfo> newSubtitle = [];
        newSubtitle.AddRange(Subtitle);
        clonedInfo.Subtitle = newSubtitle;

        return clonedInfo;
    }

    // MkvMerge, FfProbe, MediaInfo
    public MediaTool.ToolType Parser { get; } = parser;

    public List<VideoInfo> Video { get; private set; } = [];
    public List<AudioInfo> Audio { get; private set; } = [];
    public List<SubtitleInfo> Subtitle { get; private set; } = [];

    public bool HasTags { get; set; }
    public bool AnyTags =>
        HasTags || Video.Any(item => item.HasTags) || Audio.Any(item => item.HasTags) || Subtitle.Any(item => item.HasTags);
    public bool HasErrors { get; set; }
    public bool AnyErrors =>
        HasErrors || Video.Any(item => item.HasErrors) || Audio.Any(item => item.HasErrors) || Subtitle.Any(item => item.HasErrors);
    public bool Unsupported =>
        Video.Any(item => item.State == TrackInfo.StateType.Unsupported) ||
        Audio.Any(item => item.State == TrackInfo.StateType.Unsupported) ||
        Subtitle.Any(item => item.State == TrackInfo.StateType.Unsupported);
    public TimeSpan Duration { get; set; }
    public string Container { get; set; }
    public int Attachments { get; set; }
    public int Chapters { get; set; }
    public bool HasCovertArt => Video.Any(item => item.IsCoverArt);

    public void WriteLine(string prefix)
    {
        Video.ForEach(item => item.WriteLine(prefix));
        Audio.ForEach(item => item.WriteLine(prefix));
        Subtitle.ForEach(item => item.WriteLine(prefix));
    }

    public List<TrackInfo> GetTrackList()
    {
        // Combine all tracks
        List<TrackInfo> trackLick = [];
        trackLick.AddRange(Video);
        trackLick.AddRange(Audio);
        trackLick.AddRange(Subtitle);

        // Sort items by Id
        return [.. trackLick.OrderBy(item => item.Id)];
    }

    // Combined track count
    public int Count => Video.Count + Audio.Count + Subtitle.Count;

    public static bool GetMediaInfo(FileInfo fileInfo, out MediaInfo ffProbe, out MediaInfo mkvMerge, out MediaInfo mediaInfo)
    {
        mkvMerge = null;
        mediaInfo = null;

        return GetMediaInfo(fileInfo, MediaTool.ToolType.FfProbe, out ffProbe) &&
               GetMediaInfo(fileInfo, MediaTool.ToolType.MkvMerge, out mkvMerge) &&
               GetMediaInfo(fileInfo, MediaTool.ToolType.MediaInfo, out mediaInfo);
    }

    public static bool GetMediaInfo(FileInfo fileInfo, MediaTool.ToolType parser, out MediaInfo mediaInfo) =>
        // Use the specified stream parser tool
        parser switch
        {
            MediaTool.ToolType.MediaInfo => Tools.MediaInfo.GetMediaInfo(fileInfo.FullName, out mediaInfo),
            MediaTool.ToolType.MkvMerge => Tools.MkvMerge.GetMkvInfo(fileInfo.FullName, out mediaInfo),
            MediaTool.ToolType.FfProbe => Tools.FfProbe.GetFfProbeInfo(fileInfo.FullName, out mediaInfo),
            MediaTool.ToolType.None => throw new NotImplementedException(),
            MediaTool.ToolType.FfMpeg => throw new NotImplementedException(),
            MediaTool.ToolType.HandBrake => throw new NotImplementedException(),
            MediaTool.ToolType.MkvPropEdit => throw new NotImplementedException(),
            MediaTool.ToolType.SevenZip => throw new NotImplementedException(),
            MediaTool.ToolType.MkvExtract => throw new NotImplementedException(),
            _ => throw new NotImplementedException()
        };
    public void RemoveCoverArt()
    {
        // No video tracks nothing to do
        if (Video.Count == 0)
        {
            return;
        }

        // Find all tracks with cover art
        List<VideoInfo> coverArtTracks = Video.FindAll(item => item.IsCoverArt);

        // Are all tracks cover art
        if (Video.Count == coverArtTracks.Count)
        {
            Log.Logger.Error("All video tracks are cover art : {Parser}", Parser);
        }

        // Remove all cover art tracks
        foreach (VideoInfo item in coverArtTracks)
        {
            Log.Logger.Warning("Ignoring cover art video track : {Parser}:{Format}:{Codec}", Parser, item.Format, item.Codec);
            _ = Video.Remove(item);
        }
    }

    public List<TrackInfo> MatchMediaInfoToMkvMerge(List<TrackInfo> mediaInfoTrackList)
    {
        // This only works for MkvMerge
        // TODO: Convert to more generic function, but for now only MediaInfo to MkvMerge is required
        Debug.Assert(Parser == MediaTool.ToolType.MkvMerge);

        // Get a MkvMerge track list
        List<TrackInfo> mkvMergeTrackList = GetTrackList();

        // Match by MediaInfo.Number == MkvMerge.Number
        List<TrackInfo> matchedTrackList = [];
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
        List<TrackInfo> thisTrackList = GetTrackList();
        List<TrackInfo> thatTrackList = mediaInfo.GetTrackList();
        foreach (TrackInfo thisItem in thisTrackList)
        {
            // Find the matching item by matroska header number
            TrackInfo thatItem = thatTrackList.Find(item => item.Number == thisItem.Number);
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
            if (!thisItem.Language.Equals(thatItem.Language, StringComparison.Ordinal))
            {
                return false;
            }

            // Other properties may have changed during encoding
        }

        // Done
        return true;
    }
}
