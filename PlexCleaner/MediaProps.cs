using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Serilog;

namespace PlexCleaner;

public class MediaProps(MediaTool.ToolType parser)
{
    public MediaProps Clone()
    {
        // Shallow copy
        MediaProps clone = (MediaProps)MemberwiseClone();

        // Create new collections containing the old items
        List<VideoProps> newVideo = [];
        newVideo.AddRange(Video);
        clone.Video = newVideo;
        List<AudioProps> newAudio = [];
        newAudio.AddRange(Audio);
        clone.Audio = newAudio;
        List<SubtitleProps> newSubtitle = [];
        newSubtitle.AddRange(Subtitle);
        clone.Subtitle = newSubtitle;

        return clone;
    }

    // MkvMerge, FfProbe, MediaInfo
    public MediaTool.ToolType Parser { get; } = parser;

    public List<VideoProps> Video { get; private set; } = [];
    public List<AudioProps> Audio { get; private set; } = [];
    public List<SubtitleProps> Subtitle { get; private set; } = [];

    public bool HasTags { get; set; }
    public bool AnyTags =>
        HasTags
        || Video.Any(item => item.HasTags)
        || Audio.Any(item => item.HasTags)
        || Subtitle.Any(item => item.HasTags);
    public bool HasErrors { get; set; }
    public bool AnyErrors =>
        HasErrors
        || Video.Any(item => item.HasErrors)
        || Audio.Any(item => item.HasErrors)
        || Subtitle.Any(item => item.HasErrors);
    public bool Unsupported =>
        Video.Any(item => item.State == TrackProps.StateType.Unsupported)
        || Audio.Any(item => item.State == TrackProps.StateType.Unsupported)
        || Subtitle.Any(item => item.State == TrackProps.StateType.Unsupported);
    public TimeSpan Duration { get; set; }
    public string Container { get; set; }
    public int Attachments { get; set; }
    public int Chapters { get; set; }
    public bool HasCovertArt => Video.Any(item => item.IsCoverArt);

    public void WriteLine()
    {
        Video.ForEach(item => item.WriteLine());
        Audio.ForEach(item => item.WriteLine());
        Subtitle.ForEach(item => item.WriteLine());
    }

    public void WriteLine(string prefix)
    {
        Video.ForEach(item => item.WriteLine(prefix));
        Audio.ForEach(item => item.WriteLine(prefix));
        Subtitle.ForEach(item => item.WriteLine(prefix));
    }

    public List<TrackProps> GetTrackList()
    {
        // Combine all tracks
        List<TrackProps> trackLick = [];
        trackLick.AddRange(Video);
        trackLick.AddRange(Audio);
        trackLick.AddRange(Subtitle);

        // Sort items by Id
        return [.. trackLick.OrderBy(item => item.Id)];
    }

    // Combined track count
    public int Count => Video.Count + Audio.Count + Subtitle.Count;

    public static bool GetMediaProps(
        FileInfo fileInfo,
        out MediaProps ffProbe,
        out MediaProps mkvMerge,
        out MediaProps mediaInfo
    )
    {
        mkvMerge = null;
        mediaInfo = null;
        return GetMediaProps(fileInfo, MediaTool.ToolType.FfProbe, out ffProbe)
            && GetMediaProps(fileInfo, MediaTool.ToolType.MkvMerge, out mkvMerge)
            && GetMediaProps(fileInfo, MediaTool.ToolType.MediaInfo, out mediaInfo);
    }

    public static bool GetMediaProps(
        FileInfo fileInfo,
        MediaTool.ToolType parser,
        out MediaProps mediaProps
    ) =>
        // Use the specified stream parser tool
        parser switch
        {
            MediaTool.ToolType.MediaInfo => Tools.MediaInfo.GetMediaProps(
                fileInfo.FullName,
                out mediaProps
            ),
            MediaTool.ToolType.MkvMerge => Tools.MkvMerge.GetMediaProps(
                fileInfo.FullName,
                out mediaProps
            ),
            MediaTool.ToolType.FfProbe => Tools.FfProbe.GetMediaProps(
                fileInfo.FullName,
                out mediaProps
            ),
            MediaTool.ToolType.None => throw new NotImplementedException(),
            MediaTool.ToolType.FfMpeg => throw new NotImplementedException(),
            MediaTool.ToolType.HandBrake => throw new NotImplementedException(),
            MediaTool.ToolType.MkvPropEdit => throw new NotImplementedException(),
            MediaTool.ToolType.SevenZip => throw new NotImplementedException(),
            MediaTool.ToolType.MkvExtract => throw new NotImplementedException(),
            _ => throw new NotImplementedException(),
        };

    public void RemoveCoverArt()
    {
        // No video tracks nothing to do
        if (Video.Count == 0)
        {
            return;
        }

        // Find all tracks with cover art
        List<VideoProps> coverArtTracks = Video.FindAll(item => item.IsCoverArt);

        // Are all tracks cover art
        if (Video.Count == coverArtTracks.Count)
        {
            Log.Error("All video tracks are cover art : {Parser}", Parser);
        }

        // Remove all cover art tracks
        foreach (VideoProps item in coverArtTracks)
        {
            Log.Warning(
                "Ignoring cover art video track : {Parser}:{Format}:{Codec}",
                Parser,
                item.Format,
                item.Codec
            );
            _ = Video.Remove(item);
        }
    }

    public List<TrackProps> MatchMediaInfoToMkvMerge(List<TrackProps> mediaInfoTrackList)
    {
        // This only works for MkvMerge
        // TODO: Convert to more generic function, but for now only MediaInfo to MkvMerge is required
        Debug.Assert(Parser == MediaTool.ToolType.MkvMerge);

        // Get a MkvMerge track list
        List<TrackProps> mkvMergeTrackList = GetTrackList();

        // Match by MediaInfo.Number == MkvMerge.Number
        List<TrackProps> matchedTrackList = [];
        mediaInfoTrackList.ForEach(mediaInfoItem =>
            matchedTrackList.Add(
                mkvMergeTrackList.Find(mkvMergeItem => mkvMergeItem.Number == mediaInfoItem.Number)
            )
        );

        // Make sure all items matched
        Debug.Assert(mediaInfoTrackList.Count == matchedTrackList.Count);

        return matchedTrackList;
    }

    public bool VerifyTrackOrder(MediaProps mediaProps)
    {
        // Verify that this MediaProps matches the presented MediaProps
        // Used to verify that the track numbers for MkvMerge remains the same prior to and after ffmpeg and handbrake
        // This logic works for MkvMerge only
        Debug.Assert(Parser == MediaTool.ToolType.MkvMerge);
        Debug.Assert(mediaProps.Parser == MediaTool.ToolType.MkvMerge);

        // Track counts
        if (Count != mediaProps.Count)
        {
            return false;
        }

        // Get track items as list
        List<TrackProps> thisTrackList = GetTrackList();
        List<TrackProps> thatTrackList = mediaProps.GetTrackList();
        foreach (TrackProps thisItem in thisTrackList)
        {
            // Find the matching item by matroska header number
            TrackProps thatItem = thatTrackList.Find(item => item.Number == thisItem.Number);
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
