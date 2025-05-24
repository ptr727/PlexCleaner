using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PlexCleaner;

public class SelectMediaProps
{
    public SelectMediaProps(MediaProps mediaProps)
    {
        Selected = new MediaProps(mediaProps.Parser, mediaProps.FileName);
        NotSelected = new MediaProps(mediaProps.Parser, mediaProps.FileName);
    }

    public SelectMediaProps(MediaProps mediaProps, Func<TrackProps, bool> selectFunc)
    {
        Selected = new MediaProps(mediaProps.Parser, mediaProps.FileName);
        NotSelected = new MediaProps(mediaProps.Parser, mediaProps.FileName);
        Add(mediaProps, selectFunc);
    }

    public SelectMediaProps(MediaProps mediaProps, bool select)
    {
        Selected = new MediaProps(mediaProps.Parser, mediaProps.FileName);
        NotSelected = new MediaProps(mediaProps.Parser, mediaProps.FileName);
        Add(mediaProps, select);
    }

    public MediaProps Selected { get; private set; }
    public MediaProps NotSelected { get; private set; }

    private MediaProps Select(bool select) => select ? Selected : NotSelected;

    public void Add(MediaProps mediaProps, Func<TrackProps, bool> selectFunc)
    {
        Debug.Assert(mediaProps.Parser == Selected.Parser);
        Debug.Assert(mediaProps.Parser == NotSelected.Parser);
        Add(mediaProps.Video, selectFunc);
        Add(mediaProps.Audio, selectFunc);
        Add(mediaProps.Subtitle, selectFunc);
    }

    public void Add(MediaProps mediaProps, bool select)
    {
        Debug.Assert(mediaProps.Parser == Selected.Parser);
        Debug.Assert(mediaProps.Parser == NotSelected.Parser);
        Select(select).Video.AddRange(mediaProps.Video);
        Select(select).Audio.AddRange(mediaProps.Audio);
        Select(select).Subtitle.AddRange(mediaProps.Subtitle);
    }

    public void Add(IEnumerable<TrackProps> trackList, Func<TrackProps, bool> selectFunc)
    {
        foreach (TrackProps trackProps in trackList)
        {
            Add(trackProps, selectFunc(trackProps));
        }
    }

    public void Add(IEnumerable<TrackProps> trackList, bool select)
    {
        foreach (TrackProps trackProps in trackList)
        {
            Add(trackProps, select);
        }
    }

    public void Add(TrackProps trackProps, bool select)
    {
        switch (trackProps)
        {
            case VideoProps info:
                Select(select).Video.Add(info);
                break;
            case AudioProps info:
                Select(select).Audio.Add(info);
                break;
            case SubtitleProps info:
                Select(select).Subtitle.Add(info);
                break;
            default:
                throw new NotImplementedException();
        }
    }

    public void Move(IEnumerable<TrackProps> trackList, bool select)
    {
        foreach (TrackProps trackProps in trackList)
        {
            Move(trackProps, select);
        }
    }

    public void Move(TrackProps trackProps, bool select)
    {
        switch (trackProps)
        {
            case VideoProps videoProps:
                _ = Selected.Video.Remove(videoProps);
                _ = NotSelected.Video.Remove(videoProps);
                Select(select).Video.Add(videoProps);
                break;
            case AudioProps audioProps:
                _ = Selected.Audio.Remove(audioProps);
                _ = NotSelected.Audio.Remove(audioProps);
                Select(select).Audio.Add(audioProps);
                break;
            case SubtitleProps subtitleProps:
                _ = Selected.Subtitle.Remove(subtitleProps);
                _ = NotSelected.Subtitle.Remove(subtitleProps);
                Select(select).Subtitle.Add(subtitleProps);
                break;
            default:
                throw new NotImplementedException();
        }
    }

    public void SetState(TrackProps.StateType selectState, TrackProps.StateType notSelectState)
    {
        Selected.Video.ForEach(item => item.State = selectState);
        Selected.Audio.ForEach(item => item.State = selectState);
        Selected.Subtitle.ForEach(item => item.State = selectState);

        NotSelected.Video.ForEach(item => item.State = notSelectState);
        NotSelected.Audio.ForEach(item => item.State = notSelectState);
        NotSelected.Subtitle.ForEach(item => item.State = notSelectState);
    }

    public List<TrackProps> GetTrackList()
    {
        // Add all tracks to list
        List<TrackProps> trackLick = [];
        trackLick.AddRange(Selected.GetTrackList());
        trackLick.AddRange(NotSelected.GetTrackList());

        // There should be no track id duplicates
        Debug.Assert(trackLick.GroupBy(item => item.Id).All(group => group.Count() == 1));

        return [.. trackLick.OrderBy(item => item.Id)];
    }

    public void WriteLine(string selected, string notSelected)
    {
        Selected.WriteLine(selected);
        NotSelected.WriteLine(notSelected);
    }
}
