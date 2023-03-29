using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using InsaneGenius.Utilities;
using PlexCleaner.FfMpegToolJsonSchema;
using Serilog;

namespace PlexCleaner;

public class ProcessFile
{
    public ProcessFile(string mediaFile)
    {
        FileInfo = new FileInfo(mediaFile);
        SidecarFile = new SidecarFile(FileInfo);
    }

    public bool DeleteMismatchedSidecarFile(ref bool modified)
    {
        // Is this a sidecar file
        if (!SidecarFile.IsSidecarFile(FileInfo))
        {
            // Nothing to do
            return true;
        }

        // Get the matching MKV file
        string mediaFile = Path.ChangeExtension(FileInfo.FullName, ".mkv");

        // Does the media file exist
        if (File.Exists(mediaFile))
        {
            // File exists, nothing more to do
            return true;
        }

        // Media file does not exists, delete this sidecar file
        Log.Logger.Information("Deleting sidecar file with no matching MKV file : {FileName}", FileInfo.Name);

        // Delete the file
        if (!Program.Options.TestNoModify &&
            !FileEx.DeleteFile(FileInfo.FullName))
        {
            // Error
            return false;
        }

        // File deleted, do not continue processing
        modified = true;
        return false;
    }

    public bool DeleteNonMkvFile(ref bool modified)
    {
        // If MKV file nothing to do
        if (MkvMergeTool.IsMkvFile(FileInfo))
        {
            return true;
        }

        // Only delete if the option is enabled else just skip
        if (!Program.Config.ProcessOptions.DeleteUnwantedExtensions)
        {
            Log.Logger.Warning("Skipping non-MKV file : {FileName}", FileInfo.Name);
            return false;
        }

        // Non-MKV file, delete
        Log.Logger.Warning("Deleting non-MKV file : {FileName}", FileInfo.Name);

        // Delete the file
        if (!Program.Options.TestNoModify &&
            !FileEx.DeleteFile(FileInfo.FullName))
        {
            // Error
            return false;
        }

        // File deleted, do not continue processing
        modified = true;
        SidecarFile.State |= SidecarFile.StatesType.FileDeleted;
        return false;
    }

    public bool MakeExtensionLowercase(ref bool modified)
    {
        // Is the extension lowercase
        string lowerExtension = FileInfo.Extension.ToLower(CultureInfo.InvariantCulture);
        if (FileInfo.Extension.Equals(lowerExtension, StringComparison.Ordinal))
        {
            return true;
        }

        // Make the extension lowercase
        Log.Logger.Information("Making file extension lowercase : {FileName}", FileInfo.Name);

        // Rename the file
        // Windows is case insensitive, so we need to rename in two steps
        string tempName = Path.ChangeExtension(FileInfo.FullName, ".tmp");
        string lowerName = Path.ChangeExtension(FileInfo.FullName, lowerExtension);
        if (!FileEx.RenameFile(FileInfo.FullName, tempName) ||
            !FileEx.RenameFile(tempName, lowerName))
        {
            // TODO: Chance of partial failure
            return false;
        }

        // Modified filename
        modified = true;
        return Refresh(lowerName);
    }

    public bool IsWriteable()
    {
        // Media file must exist and be writeable
        // TODO: FileEx.IsFileReadWriteable(FileInfo) slows down processing
        return FileInfo.Exists && !FileInfo.IsReadOnly;
    }

    public bool IsSidecarAvailable()
    {
        return SidecarFile.Exists();
    }

    public bool IsSidecarWriteable()
    {
        // Sidecar file must exist and be writeable
        return SidecarFile.IsWriteable();
    }

    public bool RemuxByExtensions(ref bool modified)
    {
        // Optional
        if (!Program.Config.ProcessOptions.ReMux)
        {
            return true;
        }

        // Does the extension match
        if (!Program.Config.ProcessOptions.ReMuxExtensions.Contains(FileInfo.Extension))
        {
            // Nothing to do
            return true;
        }

        // ReMux the file
        Log.Logger.Information("ReMux file matched by extension : {FileName}", FileInfo.Name);

        // Remux the file, use the new filename
        if (!Convert.ReMuxToMkv(FileInfo.FullName, out string outputName))
        {
            // Error
            return false;
        }

        // In test mode the file will not be remuxed to MKV so abort
        if (Program.Options.TestNoModify)
        {
            return false;
        }

        // Set remuxed state
        SidecarFile.State |= SidecarFile.StatesType.ReMuxed;

        // Extension may have changed
        // Refresh
        modified = true;
        return Refresh(outputName);
    }

    public bool RemuxNonMkvContainer(ref bool modified)
    {
        // Optional
        if (!Program.Config.ProcessOptions.ReMux)
        {
            return true;
        }

        // Make sure that MKV named files are Matroska containers
        if (MkvMergeTool.IsMkvContainer(MkvMergeInfo))
        {
            // Nothing to do
            return true;
        }

        // ReMux the file
        Log.Logger.Information("ReMux {Container} container : {FileName}", MkvMergeInfo.Container, FileInfo.Name);

        // Remux the file, use the new filename
        if (!Convert.ReMuxToMkv(FileInfo.FullName, out string outputName))
        {
            // Error
            return false;
        }

        // In test mode the file will not be remuxed to MKV so abort
        if (Program.Options.TestNoModify)
        {
            return false;
        }

        // Set remuxed state
        SidecarFile.State |= SidecarFile.StatesType.ReMuxed;

        // Extension may have changed
        // Refresh
        modified = true;
        return Refresh(outputName);
    }

    public bool HasMediaInfoErrors()
    {
        return FfProbeInfo.HasErrors || MkvMergeInfo.HasErrors || MediaInfoInfo.HasErrors;
    }

    public bool HasRemuxMediaInfoErrors()
    {
        return FfProbeInfo.GetTrackList().Any(item => item.State == TrackInfo.StateType.ReMux) ||
               MkvMergeInfo.GetTrackList().Any(item => item.State == TrackInfo.StateType.ReMux) ||
               MediaInfoInfo.GetTrackList().Any(item => item.State == TrackInfo.StateType.ReMux);
    }

    public bool HasRemoveMediaInfoErrors()
    {
        return FfProbeInfo.GetTrackList().Any(item => item.State == TrackInfo.StateType.Remove) ||
               MkvMergeInfo.GetTrackList().Any(item => item.State == TrackInfo.StateType.Remove) ||
               MediaInfoInfo.GetTrackList().Any(item => item.State == TrackInfo.StateType.Remove);
    }

    public void ClearMediaInfoErrors()
    {
        // Clear all the error flags
        FfProbeInfo.HasErrors = false;
        MkvMergeInfo.HasErrors = false;
        MediaInfoInfo.HasErrors = false;
        FfProbeInfo.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.None);
        MkvMergeInfo.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.None);
        MediaInfoInfo.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.None);
    }

    public bool RepairMediaInfoErrors(ref bool modified)
    {
        // Any errors
        if (!HasMediaInfoErrors())
        {
            return true;
        }

        // Already ReMuxed, something is wrong with detection or removal logic
        if (SidecarFile.State.HasFlag(SidecarFile.StatesType.ReMuxed))
        {
            Log.Logger.Warning("Media errors re-detected after remuxing : {FileName}", FileInfo.Name);

            // TODO: FfMpeg does not honor IETF language tags and removes them, always repeat cleanup
            /*
            // Conditional re-process
            if (!Process.CanReProcess(Process.Tasks.ClearTags))
            {
                // Done
                return true;
            }
            */
        }

        // Conditional
        if (!Program.Config.VerifyOptions.AutoRepair)
        {
            // If repair is not enabled just clear error state
            ClearMediaInfoErrors();
            return true;
        }

        // Start with keeping all tracks
        // Selected is Keep
        // NotSelected is Remove
        SelectMediaInfo selectMediaInfo = new(MkvMergeInfo, true);

        // Any tracks to remove
        if (HasRemoveMediaInfoErrors())
        { 
            // TODO: Remove is currently only set by MediaInfo for subtitle tracks that need to be removed

            // Get a list of all the MediaInfo tracks to be removed
            var mediaInfoRemoveList = MediaInfoInfo.GetTrackList().FindAll(item => item.State == TrackInfo.StateType.Remove);
            Debug.Assert(mediaInfoRemoveList.Count > 0);

            // Mapping of track Id's are non-trivial, use the Matroska header track number to find the corresponding tracks
            var mkvMergeRemoveList = MkvMergeInfo.MatchMediaInfoToMkvMerge(mediaInfoRemoveList);
            Debug.Assert(mediaInfoRemoveList.Count == mkvMergeRemoveList.Count);

            // Unselect the to be removed tracks
            selectMediaInfo.Move(mkvMergeRemoveList, false);
        }
        Debug.Assert(selectMediaInfo.Selected.Count > 0);
        selectMediaInfo.SetState(TrackInfo.StateType.Keep, TrackInfo.StateType.Remove);

        // ReMux the file
        Log.Logger.Information("ReMux to repair metadata errors : {FileName}", FileInfo.Name);
        selectMediaInfo.WriteLine("Keep", "Remove");

        // Conditional with tracks or all tracks
        if (!Convert.ReMuxToMkv(FileInfo.FullName,
                selectMediaInfo.NotSelected.Count > 0 ? selectMediaInfo : null, 
                out string outputName))
        {
            // Error
            return false;
        }

        // In test mode the file will not be remuxed to MKV so abort
        if (Program.Options.TestNoModify)
        {
            return false;
        }

        // Set remuxed state
        SidecarFile.State |= SidecarFile.StatesType.ReMuxed;

        // Refresh
        modified = true;
        return Refresh(outputName);
    }

    public bool HasTags()
    {
        return MkvMergeInfo.HasTags || FfProbeInfo.HasTags || MediaInfoInfo.HasTags;
    }

    public bool RemoveTags(ref bool modified)
    {
        // Optional
        if (!Program.Config.ProcessOptions.RemoveTags)
        {
            return true;
        }

        // Does the file have tags
        if (!HasTags())
        {
            // No tags
            return true;
        }

        // Something is wrong with tag detection or removal
        if (SidecarFile.State.HasFlag(SidecarFile.StatesType.ClearedTags))
        {
            Log.Logger.Warning("Tags re-detected after clearing : {FileName}", FileInfo.Name);

            // Conditional re-process
            if (!Process.CanReProcess(Process.Tasks.ClearTags))
            {
                // Done
                return true;
            }
        }

        // Remove tags
        Log.Logger.Information("Clearing all tags from media file : {FileName}", FileInfo.Name);

        // Delete the tags
        if (!Program.Options.TestNoModify &&
            !Tools.MkvPropEdit.ClearTags(FileInfo.FullName, MkvMergeInfo))
        {
            // Error
            return false;
        }

        // Set modified state
        SidecarFile.State |= SidecarFile.StatesType.ClearedTags;

        // Refresh
        modified = true;
        return Refresh(true);
    }

    public bool HasAttachments()
    {
        // Use MkvMerge info
        return MkvMergeInfo.Attachments > 0;
    }

    public bool RemoveAttachments(ref bool modified)
    {
        // Optional
        if (!Program.Config.ProcessOptions.RemoveTags)
        {
            return true;
        }

        // Does the file have attachments
        if (!HasAttachments())
        {
            // No attachments
            return true;
        }

        // Something is wrong with attachment detection or removal
        if (SidecarFile.State.HasFlag(SidecarFile.StatesType.ClearedAttachments))
        {
            Log.Logger.Error("Attachments re-detected after clearing : {FileName}", FileInfo.Name);

            // Conditional re-process
            if (!Process.CanReProcess(Process.Tasks.ClearAttachments))
            {
                // Done
                return true;
            }
        }

        // Remove attachments
        Log.Logger.Information("Clearing all attachments from media file : {FileName}", FileInfo.Name);

        // Delete the attachments
        if (!Program.Options.TestNoModify &&
            !Tools.MkvPropEdit.ClearAttachments(FileInfo.FullName, MkvMergeInfo))
        {
            // Error
            return false;
        }

        // Set modified state
        SidecarFile.State |= SidecarFile.StatesType.ClearedAttachments;

        // Refresh
        modified = true;
        return Refresh(true);
    }

    public bool SetUnknownLanguageTracks(ref bool modified)
    {
        // Optional
        if (!Program.Config.ProcessOptions.SetUnknownLanguage)
        {
            return true;
        }

        // Select all tracks with unknown languages
        // Use MkvMerge for IETF language tags
        // Selected is Unknown
        // NotSelected is Known
        var selectMediaInfo = FindUnknownLanguageTracks();
        if (selectMediaInfo.Selected.Count == 0)
        {
            // Nothing to do
            return true;
        }

        Log.Logger.Information("Setting unknown language tracks to {DefaultLanguage} : {FileName}", Program.Config.ProcessOptions.DefaultLanguage, FileInfo.Name);
        selectMediaInfo.WriteLine("Unknown", "Known");

        // Set the track language to the default language
        if (!Program.Options.TestNoModify &&
            !Tools.MkvPropEdit.SetTrackLanguage(FileInfo.FullName, selectMediaInfo.Selected, Program.Config.ProcessOptions.DefaultLanguage))
        {
            // Error
            return false;
        }

        // Set modified state
        SidecarFile.State |= SidecarFile.StatesType.SetLanguage;

        // Refresh
        modified = true;
        return Refresh(true);
    }

    public bool RemoveUnwantedLanguageTracks(ref bool modified)
    {
        // Optional
        if (!Program.Config.ProcessOptions.RemoveUnwantedLanguageTracks)
        {
            return true;
        }

        // Use MkvMerge for IETF language tags
        // Selected is Keep
        // NotSelected is Remove
        var selectMediaInfo = FindUnwantedLanguageTracks();
        if (selectMediaInfo.NotSelected.Count == 0)
        {
            // Done
            return true;
        }

        // There must be something left to keep
        Debug.Assert(selectMediaInfo.Selected.Count > 0);

        Log.Logger.Information("Removing unwanted language tracks : {FileName}", FileInfo.Name);
        selectMediaInfo.WriteLine("Keep", "Remove");

        // ReMux and only keep the selected tracks
        if (!Convert.ReMuxToMkv(FileInfo.FullName, selectMediaInfo, out string outputName))
        {
            // Error
            return false;
        }

        // Update state
        SidecarFile.State |= SidecarFile.StatesType.ReMuxed;

        // Refresh
        modified = true;
        return Refresh(outputName);
    }

    public bool RemoveDuplicateTracks(ref bool modified)
    {
        // Optional
        if (!Program.Config.ProcessOptions.RemoveDuplicateTracks)
        {
            return true;
        }

        // Use MkvMerge logic
        // Selected is Keep
        // NotSelected is Remove
        var selectMediaInfo = FindDuplicateTracks();
        if (selectMediaInfo.NotSelected.Count == 0)
        {
            // Done
            return true;
        }

        // There must be something left to keep
        Debug.Assert(selectMediaInfo.Selected.Count > 0);

        Log.Logger.Information("Removing duplicate tracks : {FileName}", FileInfo.Name);
        selectMediaInfo.WriteLine("Keep", "Remove");

        // ReMux and only keep the specified tracks
        if (!Convert.ReMuxToMkv(FileInfo.FullName, selectMediaInfo, out string outputName))
        {
            // Error
            return false;
        }

        // Update state
        SidecarFile.State |= SidecarFile.StatesType.ReMuxed;

        // Refresh
        modified = true;
        return Refresh(outputName);
    }

    private bool FindInterlacedTracks(out VideoInfo videoInfo)
    {
        // Using FfProbeInfo logic
        // Only works with 1 video track
        Debug.Assert(FfProbeInfo.Video.Count <= 1);

        // Init
        videoInfo = null;

        // Are any interlaced attributes set
        videoInfo ??= FfProbeInfo.Video.Find(item => item.Interlaced);
        videoInfo ??= MediaInfoInfo.Video.Find(item => item.Interlaced);
        videoInfo ??= MkvMergeInfo.Video.Find(item => item.Interlaced);
        if (videoInfo != null)
        {
            // Interlaced attribute set
            return true;
        }

        // Running idet is expensive, skip if already verified or already deinterlaced
        if (State.HasFlag(SidecarFile.StatesType.Verified) ||
            State.HasFlag(SidecarFile.StatesType.VerifyFailed) ||
            State.HasFlag(SidecarFile.StatesType.DeInterlaced))
        {
            // Conditional re-process
            if (!Process.CanReProcess(Process.Tasks.IdetFilter))
            {
                // Not interlaced
                return false;
            }
        }

        // Count the frame types using the idet filter, expensive
        if (!GetIdetInfo(out FfMpegIdetInfo idetInfo))
        {
            // Error
            return false;
        }

        // Idet result
        if (!idetInfo.IsInterlaced())
        {
            // Not interlaced
            return false;
        }

        // Idet says yes metadata said no
        Log.Logger.Warning("Idet reported interlaced, metadata reported not interlaced : {FileName}", FileInfo.Name);

        // Use the first video track from FfProbe
        videoInfo = FfProbeInfo.Video.First();
        videoInfo.Interlaced = true;
        return true;
    }

    private bool FindClosedCaptionTracks(out VideoInfo videoInfo)
    {
        // Using FfProbeInfo logic
        // Only works with 1 video track
        Debug.Assert(FfProbeInfo.Video.Count <= 1);

        // Init
        videoInfo = null;

        // Are any CC attributes set
        videoInfo ??= FfProbeInfo.Video.Find(item => item.ClosedCaptions);
        videoInfo ??= MediaInfoInfo.Video.Find(item => item.ClosedCaptions);
        videoInfo ??= MkvMergeInfo.Video.Find(item => item.ClosedCaptions);
        if (videoInfo != null)
        {
            // CC attribute set
            return true;
        }

        // TODO: Detecting CC's using ffprobe JSON output is broken, run ffprobe in normal output mode
        // https://github.com/ptr727/PlexCleaner/issues/94

        // Running ffprobe is not free, skip if already verified or CC's already removed
        if (State.HasFlag(SidecarFile.StatesType.Verified) ||
            State.HasFlag(SidecarFile.StatesType.VerifyFailed) ||
            State.HasFlag(SidecarFile.StatesType.ClearedCaptions))
        {
            // Conditional re-process
            if (!Process.CanReProcess(Process.Tasks.FindClosedCaptions))
            {
                // Not found
                return false;
            }
        }

        // Get ffprobe text output
        Log.Logger.Information("Finding Closed Captions in video stream : {FileName}", FileInfo.Name);
        if (!Tools.FfProbe.GetFfProbeInfoText(FileInfo.FullName, out string ffProbe))
        {
            // Error
            return false;
        }

        // Look for the "Closed Captions" in the video stream line
        // Stream #0:0(eng): Video: h264 (High), yuv420p(tv, bt709, progressive), 1280x720, Closed Captions, SAR 1:1 DAR 16:9, 29.97 fps, 29.97 tbr, 1k tbn (default)
        using StringReader lineReader = new(ffProbe);
        while (lineReader.ReadLine() is { } line)
        {
            // Line should start with "Stream #", and contain "Video" and contain "Closed Captions"
            line = line.Trim();
            if (line.StartsWith("Stream #", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("Video", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("Closed Captions", StringComparison.OrdinalIgnoreCase))
            {
                // Use the first video track from FfProbe
                videoInfo = FfProbeInfo.Video.First();
                videoInfo.ClosedCaptions = true;
                return true;
            }
        }

        // Not found
        return false;
    }

    public bool DeInterlace(ref bool modified)
    {
        // Optional
        if (!Program.Config.ProcessOptions.DeInterlace)
        {
            return true;
        }

        // Do we have any interlaced video
        if (!FindInterlacedTracks(out VideoInfo videoInfo))
        {
            return true;
        }

        // Already deinterlaced and still interlaced
        if (State.HasFlag(SidecarFile.StatesType.DeInterlaced))
        {
            Log.Logger.Error("Interlacing re-detected after deinterlacing : {FileName}", FileInfo.Name);
        }

        Log.Logger.Information("Deinterlacing interlaced video : {FileName}", FileInfo.Name);
        videoInfo.State = TrackInfo.StateType.DeInterlace;
        videoInfo.WriteLine("Interlaced");

        // TODO: HandBrake will convert closed captions and subtitle tracks to ASS format
        // To work around this we will deinterlace without subtitles then add the subtitles back
        // https://github.com/ptr727/PlexCleaner/issues/95

        // Test
        if (Program.Options.TestNoModify)
        {
            return true;
        }

        // Create a temp filename for the deinterlaced output
        string deintName = Path.ChangeExtension(FileInfo.FullName, ".tmp1");

        // Deinterlace using HandBrake and ignore subtitles and closed captions
        FileEx.DeleteFile(deintName);
        if (!Tools.HandBrake.ConvertToMkv(FileInfo.FullName, deintName, false, true))
        {
            // Error
            FileEx.DeleteFile(deintName);
            return false;
        }

        // If there are subtitles in the original file merge them back
        // Deinterlacing used ffProbe, remuxing use MkvMerge
        if (MkvMergeInfo.Subtitle.Count == 0)
        {
            // Delete the temp file and rename the output
            if (!FileEx.RenameFile(deintName, FileInfo.FullName))
            {
                // Error
                FileEx.DeleteFile(deintName);
                return false;
            }
        }
        else
        {
            // Create a temp filename for the merged output
            string mergeName = Path.ChangeExtension(FileInfo.FullName, ".tmp2");

            Log.Logger.Information("Merging subtitles with deinterlaced video : {FileName}", FileInfo.Name);

            // Merge the subtitles from the original file with the deinterlaced file
            var subInfo = new MediaInfo(MediaTool.ToolType.MkvMerge);
            subInfo.Subtitle.AddRange(MkvMergeInfo.Subtitle);
            FileEx.DeleteFile(mergeName);
            if (!Tools.MkvMerge.MergeToMkv(FileInfo.FullName, subInfo, deintName, mergeName))
            {
                // Error
                FileEx.DeleteFile(deintName);
                FileEx.DeleteFile(mergeName);
                return false;
            }

            // Delete the temp files and rename the output
            FileEx.DeleteFile(deintName);
            if (!FileEx.RenameFile(mergeName, FileInfo.FullName))
            {
                // Error
                FileEx.DeleteFile(mergeName);
                return false;
            }
        }

        // Update state
        SidecarFile.State |= SidecarFile.StatesType.DeInterlaced;

        // Refresh
        modified = true;
        return Refresh(true);
    }

    public bool RemoveSubtitles(ref bool modified)
    {
        // Any subtitle tracks
        // Use MkvMerge
        if (MkvMergeInfo.Subtitle.Count == 0)
        {
            // Done
            return true;
        }

        // Remove all the subtitle tracks
        // Selected Keep
        // NotSelected Remove
        SelectMediaInfo selectMediaInfo = new(MkvMergeInfo, true);
        selectMediaInfo.Move(MkvMergeInfo.Subtitle, false);
        selectMediaInfo.SetState(TrackInfo.StateType.Keep, TrackInfo.StateType.Remove);

        Log.Logger.Information("Removing subtitle tracks : {FileName}", FileInfo.Name);
        selectMediaInfo.WriteLine("Keep", "Remove");

        // ReMux and only keep the specified tracks
        if (!Convert.ReMuxToMkv(FileInfo.FullName, selectMediaInfo, out string outputName))
        {
            // Error
            return false;
        }

        // Update state
        SidecarFile.State |= SidecarFile.StatesType.ReMuxed;

        // Refresh
        modified = true;
        return Refresh(outputName);
    }

    public bool RemoveClosedCaptions(ref bool modified)
    {
        // Conditional
        if (!Program.Config.ProcessOptions.RemoveClosedCaptions)
        {
            return true;
        }

        // Do we have any closed captions
        if (!FindClosedCaptionTracks(out VideoInfo videoInfo))
        {
            return true;
        }

        Log.Logger.Information("Removing Closed Captions from video stream : {FileName}", FileInfo.Name);
        videoInfo.WriteLine("Closed Captions");

        // Test
        if (Program.Options.TestNoModify)
        {
            return true;
        }

        // Create a temp output filename
        string tempName = Path.ChangeExtension(FileInfo.FullName, ".tmp");

        // Remove Closed Captions
        FileEx.DeleteFile(tempName);
        Log.Logger.Information("Removing Closed Captions using FfMpeg : {FileName}", FileInfo.Name);
        if (!Tools.FfMpeg.RemoveClosedCaptions(FileInfo.FullName, tempName))
        {
            // Error
            Log.Logger.Error("Removing Closed Captions using FfMpeg failed : {FileName}", FileInfo.Name);
            FileEx.DeleteFile(tempName);
            return false;
        }

        // Rename the temp file to the original file
        if (!FileEx.RenameFile(tempName, FileInfo.FullName))
        {
            FileEx.DeleteFile(tempName);
            return false;
        }

        // Update state
        SidecarFile.State |= SidecarFile.StatesType.ClearedCaptions;

        // Refresh
        modified = true;
        return Refresh(true);
    }

    public bool ReEncode(ref bool modified)
    {
        // Optional
        if (!Program.Config.ProcessOptions.ReEncode)
        {
            return true;
        }

        // Find all tracks that need re-encoding
        // Use FfProbeInfo for matching logic
        // Selected is ReEncode
        // NotSelected is Keep
        var selectMediaInfo = FindNeedReEncode();
        if (selectMediaInfo.Selected.Count == 0)
        {
            // Done
            return true;
        }

        Log.Logger.Information("ReEncoding required tracks : {FileName}", FileInfo.Name);
        selectMediaInfo.WriteLine("ReEncode", "Passthrough");

        // Reencode selected tracks
        if (!Convert.ConvertToMkv(FileInfo.FullName, selectMediaInfo, out string outputName))
        {
            // Convert will log error
            // Error
            return false;
        }

        // Update state
        SidecarFile.State |= SidecarFile.StatesType.ReEncoded;

        // Refresh
        modified = true;
        return Refresh(outputName);
    }

    private bool ShouldVerify(Process.Tasks task)
    {
        // Verified, nothing more to do
        if (SidecarFile.State.HasFlag(SidecarFile.StatesType.Verified))
        {
            Debug.Assert(!SidecarFile.State.HasFlag(SidecarFile.StatesType.VerifyFailed));
            Debug.Assert(!SidecarFile.State.HasFlag(SidecarFile.StatesType.RepairFailed));
            return false;
        }

        // VerifyFailed
        if (SidecarFile.State.HasFlag(SidecarFile.StatesType.VerifyFailed))
        {
            Debug.Assert(!SidecarFile.State.HasFlag(SidecarFile.StatesType.Verified));
            Debug.Assert(!SidecarFile.State.HasFlag(SidecarFile.StatesType.Repaired));

            // If repair is enabled but repair has not failed then re-verify
            // Possible when a verify or repair is interrupted
            if (Program.Config.VerifyOptions.AutoRepair &&
                !SidecarFile.State.HasFlag(SidecarFile.StatesType.RepairFailed))
            {
                return true;
            }

            // Conditional verify based on verify type
            return Process.CanReProcess(task);
        }

        // Not Verified, not VerifyFailed
        return true;
    }

    private bool ShouldRepair()
    {
        // Verified or Repaired, nothing more to do
        if (SidecarFile.State.HasFlag(SidecarFile.StatesType.Verified) ||
            SidecarFile.State.HasFlag(SidecarFile.StatesType.Repaired))
        {
            Debug.Assert(!SidecarFile.State.HasFlag(SidecarFile.StatesType.VerifyFailed));
            Debug.Assert(!SidecarFile.State.HasFlag(SidecarFile.StatesType.RepairFailed));
            return false;
        }

        // RepairFailed
        if (SidecarFile.State.HasFlag(SidecarFile.StatesType.RepairFailed))
        {
            Debug.Assert(!SidecarFile.State.HasFlag(SidecarFile.StatesType.Verified));
            Debug.Assert(!SidecarFile.State.HasFlag(SidecarFile.StatesType.Repaired));
            
            // Conditional repair
            return Process.CanReProcess(Process.Tasks.Repair);
        }

        // Not Verified, not RepairFailed
        return true;
    }

    public bool Verify(ref bool modified)
    {
        // TODO: Refactor, got too complicated

        // Verify if enabled
        if (!Program.Config.ProcessOptions.Verify)
        {
            return true;
        }

        // Skip files that are older than the minimum age
        TimeSpan fileAge = DateTime.UtcNow - FileInfo.LastWriteTimeUtc;
        TimeSpan testAge = TimeSpan.FromDays(Program.Config.VerifyOptions.MinimumFileAge);
        if (Program.Config.VerifyOptions.MinimumFileAge > 0 &&
            fileAge > testAge)
        {
            Log.Logger.Warning("Skipping file due to MinimumFileAge : {FileAge} > {TestAge} : {FileName}", fileAge, testAge, FileInfo.Name);
            return true;
        }

        // Conditional verify metadata
        if (!ShouldVerify(Process.Tasks.VerifyMetadata))
        {
            // Skip
            return true;
        }

        // Break out and skip to end when any verification step fails
        bool verified = false;
        for (; ; )
        {
            // Need at least one video or audio track
            if (MediaInfoInfo.Video.Count == 0 && MediaInfoInfo.Audio.Count == 0)
            {
                Log.Logger.Error("File missing audio and video track : {FileName}", FileInfo.Name);
                MediaInfoInfo.WriteLine("Missing");

                // Done
                break;
            }

            // Warn if audio or video tracks are missing
            if (MediaInfoInfo.Video.Count == 0 || MediaInfoInfo.Audio.Count == 0)
            {
                Log.Logger.Warning("File missing audio or video tracks : Audio: {Audio}, Video: {Video} : {FileName}", MediaInfoInfo.Audio.Count, MediaInfoInfo.Video.Count, FileInfo.Name);
                MediaInfoInfo.WriteLine("Missing");

                // Warning only, continue
            }

            // Warn if more than one video track
            if (MediaInfoInfo.Video.Count > 1)
            {
                Log.Logger.Warning("File has more than one video track : Video: {Video} : {FileName}", MediaInfoInfo.Video.Count, FileInfo.Name);
                MediaInfoInfo.WriteLine("Extra");

                // Warning only, continue
            }

            // Test playback duration
            // Ignore when snippets are enabled
            if (MkvMergeInfo.Duration < TimeSpan.FromMinutes(Program.Config.VerifyOptions.MinimumDuration) &&
                !Program.Options.TestSnippets)
            {
                // Playback duration is too short
                Log.Logger.Warning("File play duration is too short : {Duration} < {MinimumDuration} : {FileName}",
                    MkvMergeInfo.Duration,
                    TimeSpan.FromMinutes(Program.Config.VerifyOptions.MinimumDuration),
                    FileInfo.Name);
                MkvMergeInfo.WriteLine("Short");

                // Warning only, continue
            }

            // Verify HDR, cheap
            if (!VerifyHdrProfile())
            {
                // Cancel requested
                if (Program.IsCancelledError())
                {
                    return false;
                }

                // Done
                break;
            }

            // Conditional
            if (!ShouldVerify(Process.Tasks.VerifyStream))
            {
                // Skip
                return true;
            }

            // Verify bitrate, expensive
            if (!VerifyBitrate())
            {
                // Cancel requested
                if (Program.IsCancelledError())
                {
                    return false;
                }

                // Done
                break;
            }

            // Verify media streams, expensive
            Log.Logger.Information("Verifying media streams : {FileName}", FileInfo.Name);
            if (!Tools.FfMpeg.VerifyMedia(FileInfo.FullName, out string error))
            {
                // Cancel requested
                if (Program.IsCancelledError())
                {
                    return false;
                }

                // Failed stream validation
                Log.Logger.Error("Media stream validation failed : {FileName}", FileInfo.Name);
                Log.Logger.Error("{Error}", error);

                // Should we attempt file repair
                if (!Program.Config.VerifyOptions.AutoRepair)
                {
                    // Done
                    break;
                }

                // Attempt file repair
                if (!VerifyRepair(ref modified))
                {
                    // Cancel requested
                    if (Program.IsCancelledError())
                    {
                        return false;
                    }

                    // Done
                    break;
                }
            }

            // Done
            verified = true;
            break;
        }

        // Cancel requested
        if (Program.IsCancelled())
        {
            return false;
        }

        // If failed
        if (!verified)
        {
            // Test
            if (Program.Options.TestNoModify)
            {
                return false;
            }

            // Delete files if enabled
            if (Program.Config.VerifyOptions.DeleteInvalidFiles)
            {
                // Delete the media file and sidecar file
                // Ignore delete errors
                Log.Logger.Information("Deleting media file due to failed verification : {FileName}", FileInfo.FullName);
                FileEx.DeleteFile(FileInfo.FullName);
                FileEx.DeleteFile(SidecarFile.GetSidecarName(FileInfo));
                SidecarFile.State |= SidecarFile.StatesType.FileDeleted;

                // Done
                return false;
            }

            // Update state
            SidecarFile.State |= SidecarFile.StatesType.VerifyFailed;
            SidecarFile.State &= ~SidecarFile.StatesType.Verified;
            Refresh(false);

            // Failed
            return false;
        }

        // All ok
        SidecarFile.State |= SidecarFile.StatesType.Verified;
        SidecarFile.State &= ~SidecarFile.StatesType.VerifyFailed;
        return Refresh(false);
    }

    private bool VerifyBitrate()
    {
        // Skip if no bitrate limit
        if (Program.Config.VerifyOptions.MaximumBitrate == 0)
        {
            return true;
        }

        // TODO: Verify that bitrate is acceptable for the resolution of the content, i.e. no YIFY, no fake 4K
        // https://4kmedia.org/real-or-fake-4k/
        // https://en.wikipedia.org/wiki/YIFY

        // Calculate bitrate
        Log.Logger.Information("Calculating bitrate info : {FileName}", FileInfo.Name);
        if (!GetBitrateInfo(out BitrateInfo bitrateInfo))
        {
            // Cancel requested
            if (Program.IsCancelledError())
            {
                return false;
            }

            // Failed
            Log.Logger.Error("Failed to calculate bitrate info : {FileName}", FileInfo.Name);
            return false;
        }

        // Print bitrate
        bitrateInfo.WriteLine();

        // Combined bitrate exceeded threshold
        if (bitrateInfo.CombinedBitrate.Exceeded > 0)
        {
            Log.Logger.Warning("Maximum bitrate exceeded : {CombinedBitrate} > {MaximumBitrate} : {FileName}",
                Bitrate.ToBitsPerSecond(bitrateInfo.CombinedBitrate.Maximum),
                Bitrate.ToBitsPerSecond(Program.Config.VerifyOptions.MaximumBitrate / 8),
                FileInfo.Name);

            // Update state
            // Caller will Refresh()
            SidecarFile.State |= SidecarFile.StatesType.BitrateExceeded;
        }

        // Audio bitrate exceeds video bitrate, may indicate an error with the video track
        if (bitrateInfo.AudioBitrate.Average > bitrateInfo.VideoBitrate.Average)
        {
            Log.Logger.Warning("Audio bitrate exceeds Video bitrate : {AudioBitrate} > {VideoBitrate} : {FileName}",
                Bitrate.ToBitsPerSecond(bitrateInfo.AudioBitrate.Average),
                Bitrate.ToBitsPerSecond(bitrateInfo.VideoBitrate.Average),
                FileInfo.Name);
        }

        // Ignore the error
        return true;
    }

    private bool VerifyHdrProfile()
    {
        // Verify that HDR profiles are HDR or Dolby Vision Profile 7+
        // If HDR10 compatibility is not reported the video can't play (without funky colors) on non-DV hardware
        // https://en.wikipedia.org/wiki/High-dynamic-range_video
        // https://en.wikipedia.org/wiki/Dolby_Vision

        // From MediaInfo:

        // HDR:
        /*
        <HDR_Format>SMPTE ST 2086</HDR_Format>
        <HDR_Format_Compatibility>HDR10</HDR_Format_Compatibility>
        */

        // Dolby Vision Profile 5
        /*
        <HDR_Format>Dolby Vision</HDR_Format>
        <HDR_Format_Version>1.0</HDR_Format_Version>
        <HDR_Format_Profile>dvhe.05</HDR_Format_Profile>
        <HDR_Format_Level>06</HDR_Format_Level>
        <HDR_Format_Settings>BL+RPU</HDR_Format_Settings>
        */

        // Dolby Vision Profile 7
        /*
        <HDR_Format>Dolby Vision / SMPTE ST 2086</HDR_Format>
        <HDR_Format_Version>1.0 / </HDR_Format_Version>
        <HDR_Format_Profile>dvhe.07 / </HDR_Format_Profile>
        <HDR_Format_Level>06 / </HDR_Format_Level>
        <HDR_Format_Settings>BL+EL+RPU / </HDR_Format_Settings>
        <HDR_Format_Compatibility>Blu-ray / HDR10</HDR_Format_Compatibility>
        */

        // Dolby Vision Profile 8.1
        /*
        <HDR_Format>Dolby Vision / SMPTE ST 2086</HDR_Format>
        <HDR_Format_Version>1.0 / </HDR_Format_Version>
        <HDR_Format_Profile>dvhe.08 / </HDR_Format_Profile>
        <HDR_Format_Level>09 / </HDR_Format_Level>
        <HDR_Format_Settings>BL+RPU / </HDR_Format_Settings>
        <HDR_Format_Compatibility>Blu-ray / HDR10</HDR_Format_Compatibility>
        */

        // Find video tracks with HDR10 as format
        var hdrVideoTracks = MediaInfoInfo.Video.FindAll(videoItem => 
            Hdr10Format.Any(hdrFormat => 
            videoItem.FormatHdr.Contains(hdrFormat, StringComparison.OrdinalIgnoreCase)));
        hdrVideoTracks.ForEach(videoItem =>
        {
            Log.Logger.Warning("Video lacks HDR10 compatibility : {Hdr} : {FileName}", videoItem.FormatHdr, FileInfo.Name);
        });

        // Ignore the error
        return true;
    }

    private bool VerifyRepair(ref bool modified)
    {
        // TODO: Refactor, got too complicated

        // Test
        if (Program.Options.TestNoModify)
        {
            return false;
        }

        // Conditional
        if (!ShouldRepair())
        {
            // Skip
            return true;
        }

        // Trying again may not succeed unless tools changed
        if (SidecarFile.State.HasFlag(SidecarFile.StatesType.RepairFailed))
        {
            Log.Logger.Warning("Repairing again after previous attempt failed : {FileName}", FileInfo.Name);
        }

        // TODO: Analyze the error output and conditionally repair only the audio or video track
        // [aac @ 000001d3c5652440] noise_facs_q 32 is invalid
        // [ac3 @ 000002167861a840] error decoding the audio block
        // [h264 @ 00000270a07f6ac0] Missing reference picture, default is 65514
        // [h264 @ 000001979ebaa7c0] mmco: unref short failure
        // [h264 @ 00000152da828940] number of reference frames (0+5) exceeds max (4; probably corrupt input), discarding one
        // [NULL @ 000002a2115acac0] Invalid NAL unit size (-1148261185 > 8772).
        // [h264 @ 000002a21166bd00] Invalid NAL unit size (-1148261185 > 8772).
        // [matroska,webm @ 0000029a256d9280] Length 7 indicated by an EBML number's first byte 0x02 at pos 1601277 (0x186efd) exceeds max length 4.

        // TODO: Can we ignore the monotonically increasing display time stamp issue?
        // Lots of similar reports, can't find a CLI option to disable or ignore this as an error
        // Also see FFmpeg AVFMT_TS_NONSTRICT option
        // [null @ 0000018cd6bf1800] Application provided invalid, non monotonically increasing dts to muxer in stream 0: 8 >= 8
        // [null @ 0000018cd6bf1800] Application provided invalid, non monotonically increasing dts to muxer in stream 0: 12 >= 12
        // [null @ 0000018cd6bf1800] Application provided invalid, non monotonically increasing dts to muxer in stream 0: 16 >= 16
        // [null @ 0000018cd6bf1800] Application provided invalid, non monotonically increasing dts to muxer in stream 0: 20 >= 20
        // [null @ 0000018cd6bf1800] Application provided invalid, non monotonically increasing dts to muxer in stream 0: 348 >= 348

        // TODO: HandBrake sometimes fails with what looks like a remux error
        // ERROR: avformatMux: track 1, av_interleaved_write_frame failed with error 'Invalid argument'
        // M[15:35:43] libhb: work result = 4

        // TODO: FfMpeg fails to decode some files
        // https://trac.ffmpeg.org/search?q=%22Invalid+NAL+unit+size%22&noquickjump=1&milestone=on&ticket=on&wiki=on
        // https://trac.ffmpeg.org/search?q=%22non+monotonically+increasing+dts+to+muxer%22&noquickjump=1&milestone=on&ticket=on&wiki=on

        // TODO: FfMpeg x265 requires input resolution to be multiple of chroma subsampling
        // https://stackoverflow.com/questions/50371919/ffmpeg-cannot-open-libx265-encoder-error-initializing-output-stream-00-err
        // http://www.ffmpeg-archive.org/scaling-failed-height-must-be-an-integer-td4671624.html
        // E.g. 1280 x 718 will fail, 1280 x 720 will work
        // https://ffmpeg.org/ffmpeg-filters.html#crop
        // -vf crop='iw-mod(iw,4)':'ih-mod(ih,4)'

        // Do not repair in-place, repair to temp file, if successful replace original file
        string tempName = Path.ChangeExtension(FileInfo.FullName, ".tmp");

        // Convert using ffmpeg
        Log.Logger.Information("Attempting media repair by ReEncoding using FfMpeg : {FileName}", FileInfo.Name);
        if (!Tools.FfMpeg.ConvertToMkv(FileInfo.FullName, tempName))
        {
            // Failed, delete temp file
            FileEx.DeleteFile(tempName);

            // Cancel requested
            if (Program.IsCancelledError())
            {
                return false;
            }

            // Failed
            Log.Logger.Error("ReEncoding using FfMpeg failed : {FileName}", FileInfo.Name);

            // Try again using handbrake
            Log.Logger.Information("Attempting media repair by ReEncoding using HandBrake : {FileName}", FileInfo.Name);
            if (!Tools.HandBrake.ConvertToMkv(FileInfo.FullName, tempName, true, false))
            {
                // Failed, delete temp file
                FileEx.DeleteFile(tempName);

                // Cancel requested
                if (Program.IsCancelledError())
                {
                    return false;
                }

                // Failed
                Log.Logger.Error("ReEncode using HandBrake failed : {FileName}", FileInfo.Name);

                // Update state
                // Caller will Refresh()
                SidecarFile.State |= SidecarFile.StatesType.RepairFailed;

                return false;
            }
        }

        // Re-encoding succeeded, re-verify the temp file
        Log.Logger.Information("Re-verifying media streams : {FileName}", FileInfo.Name);
        if (!Tools.FfMpeg.VerifyMedia(tempName, out string error))
        {
            // Failed, delete temp file
            FileEx.DeleteFile(tempName);

            // Cancel requested
            if (Program.IsCancelledError())
            {
                return false;
            }

            // Failed stream validation
            Log.Logger.Error("Media stream validation failed after repair attempt : {FileName}", FileInfo.Name);
            Log.Logger.Error("{Error}", error);

            // Update state
            // Caller will Refresh()
            SidecarFile.State |= SidecarFile.StatesType.RepairFailed;

            return false;
        }

        // Rename the temp file to the original file
        if (!FileEx.RenameFile(tempName, FileInfo.FullName))
        {
            return false;
        }

        // Repair succeeded
        Log.Logger.Information("Repair succeeded : {FileName}", FileInfo.Name);

        // Update state
        SidecarFile.State |= SidecarFile.StatesType.Repaired;
        SidecarFile.State &= ~SidecarFile.StatesType.RepairFailed;

        // Caller will Refresh()
        modified = true;
        return true;
    }

    public bool SetLastWriteTimeUtc(DateTime lastWriteTimeUtc)
    {
        // Conditional
        if (!Program.Config.ProcessOptions.RestoreFileTimestamp ||
            Program.Options.TestNoModify)
        {
            return true;
        }

        // Set modified timestamp
        File.SetLastWriteTimeUtc(FileInfo.FullName, lastWriteTimeUtc);

        // Refresh sidecar info
        return Refresh(true);
    }

    public bool SetReVerifyState()
    {
        // Conditionally remove the VerifyFailed flag if set
        if (Program.Options.ReVerify &&
            State.HasFlag(SidecarFile.StatesType.VerifyFailed))
        {
            Log.Logger.Information("Resetting verify and repair state : {FileName}", FileInfo.Name);

            // Remove VerifyFailed and RepairFailed flags
            Debug.Assert(!State.HasFlag(SidecarFile.StatesType.Verified));
            Debug.Assert(!State.HasFlag(SidecarFile.StatesType.Repaired));
            SidecarFile.State &= ~SidecarFile.StatesType.VerifyFailed;
            SidecarFile.State &= ~SidecarFile.StatesType.RepairFailed;

            // Refresh sidecar info
            return Refresh(true);
        }

        // Done
        return true;
    }

    private bool Refresh(string filename)
    {
        // Media filename changed
        // Compare case sensitive for Linux support
        Debug.Assert(filename != null);
        if (!FileInfo.FullName.Equals(filename, StringComparison.Ordinal))
        {
            // Refresh sidecar file info but preserve existing state, mark as renamed
            FileInfo = new FileInfo(filename);
            SidecarFile.StatesType state = SidecarFile.State | SidecarFile.StatesType.FileReNamed;
            SidecarFile = new SidecarFile(FileInfo)
            {
                State = state
            };
        }

        // Refresh sidecar info
        return Refresh(true);
    }

    private bool Refresh(bool modified)
    {
        // Call Refresh() at each processing function exit
        // Set modified to true if the media file has been modified
        // Set modified to false if only the state has been modified

        // If the file was modified wait for a little to let the IO complete
        // E.g. MkvPropEdit changes are not visible when immediately re-reading the file
        if (modified)
        {
            Log.Logger.Information("Waiting for IO to flush : {RefreshWaitTime}s : {File}", RefreshWaitTime, FileInfo.Name);
            Thread.Sleep(RefreshWaitTime * 1000);
        }

        // Load info from sidecar
        if (Program.Config.ProcessOptions.UseSidecarFiles)
        {
            // Open will read, create, update
            if (!SidecarFile.Open(modified))
            {
                // Failed to read or create sidecar
                return false;
            }

            // Assign results
            FfProbeInfo = SidecarFile.FfProbeInfo;
            MkvMergeInfo = SidecarFile.MkvMergeInfo;
            MediaInfoInfo = SidecarFile.MediaInfoInfo;

            return true;
        }

        // Get info directly from tools
        if (!MediaInfo.GetMediaInfo(FileInfo,
                out MediaInfo ffprobeInfo,
                out MediaInfo mkvmergeInfo,
                out MediaInfo mediainfoInfo))
        {
            return false;
        }

        // Assign results
        FfProbeInfo = ffprobeInfo;
        MkvMergeInfo = mkvmergeInfo;
        MediaInfoInfo = mediainfoInfo;

        return true;
    }

    public bool VerifyMediaInfo()
    {
        // TODO: Mixing anything other than MvMerge to MkvMerge requires the track numbers to be the same
        // Id's are unique to the tool, numbers come from the matroska header
        // FfProbe does not report numbers, only id's

        // Make sure the track counts match
        if (FfProbeInfo.Audio.Count != MkvMergeInfo.Audio.Count ||
            MkvMergeInfo.Audio.Count != MediaInfoInfo.Audio.Count ||
            FfProbeInfo.Video.Count != MkvMergeInfo.Video.Count ||
            MkvMergeInfo.Video.Count != MediaInfoInfo.Video.Count ||
            FfProbeInfo.Subtitle.Count != MkvMergeInfo.Subtitle.Count ||
            MkvMergeInfo.Subtitle.Count != MediaInfoInfo.Subtitle.Count)
        {
            // Something is wrong; bad logic, bad media, bad tools?
            Log.Logger.Error("Tool track count discrepancy : {File}", FileInfo.Name);
            MediaInfoInfo.WriteLine("MediaInfo");
            MkvMergeInfo.WriteLine("MkvMerge");
            FfProbeInfo.WriteLine("FfProbe");

            return false;
        }

        return true;
    }

    public bool GetMediaInfo()
    {
        // Only MKV files
        Debug.Assert(MkvMergeTool.IsMkvFile(FileInfo));

        // Get media info
        return Refresh(false);
    }

    public bool MonitorFileTime(int seconds)
    {
        bool timestampChanged = false;
        FileInfo.Refresh();
        DateTime fileTime = FileInfo.LastWriteTimeUtc;
        Log.Logger.Information("MonitorFileTime : {FileTime} : {FileName}\"", fileTime, FileInfo.Name);
        for (int i = 0; i < seconds; i++)
        {
            if (Program.IsCancelled(1000))
            {
                break;
            }

            FileInfo.Refresh();
            if (FileInfo.LastWriteTimeUtc != fileTime)
            {
                timestampChanged = true;
                Log.Logger.Warning("MonitorFileTime : {LastWriteTimeUtc} != {FileTime} : {FileName}",
                    FileInfo.LastWriteTimeUtc,
                    fileTime,
                    FileInfo.Name);
            }
            fileTime = FileInfo.LastWriteTimeUtc;
        }

        return timestampChanged;
    }

    public bool GetBitrateInfo(out BitrateInfo bitrateInfo)
    {
        bitrateInfo = null;

        // Get packet info
        if (!Tools.FfProbe.GetPacketInfo(FileInfo.FullName, out List<Packet> packetList))
        {
            return false;
        }

        // Use the default track, else the first track
        var videoInfo = FfProbeInfo.Video.Find(item => item.Flags.HasFlag(TrackInfo.FlagsType.Default));
        videoInfo ??= FfProbeInfo.Video.FirstOrDefault();
        var audioInfo = FfProbeInfo.Audio.Find(item => item.Flags.HasFlag(TrackInfo.FlagsType.Default));
        audioInfo ??= FfProbeInfo.Audio.FirstOrDefault();

        // Compute bitrate from packets
        bitrateInfo = new BitrateInfo();
        bitrateInfo.Calculate(packetList,
            videoInfo == null ? -1 : videoInfo.Id,
            audioInfo == null ? -1 : audioInfo.Id,
            Program.Config.VerifyOptions.MaximumBitrate / 8);

        return true;
    }

    private bool GetIdetInfo(out FfMpegIdetInfo idetInfo)
    {
        // Count the frame types using the idet filter
        Log.Logger.Information("Counting interlaced frames : {FileName}", FileInfo.Name);
        if (!FfMpegIdetInfo.GetIdetInfo(FileInfo, out idetInfo))
        {
            // Cancel requested
            if (Program.IsCancelledError())
            {
                return false;
            }

            // Failed
            Log.Logger.Error("Failed to count interlaced frames : {FileName}", FileInfo.Name);
            return false;
        }

        // Log result
        Log.Logger.Information("FfMpeg Idet : Interlaced: {IdetInterlaced} ({Percentage:P} > {Threshold:P}), Interlaced: {Interlaced}, Progressive: {Progressive}, Undetermined: {Undetermined}, Total: {Total} : {FileName}",
            idetInfo.IsInterlaced(),
            idetInfo.InterlacedPercentage,
            FfMpegIdetInfo.InterlacedThreshold,
            idetInfo.Interlaced,
            idetInfo.Progressive,
            idetInfo.Undetermined,
            idetInfo.Total,
            FileInfo.Name);

        return true;
    }

    public SelectMediaInfo FindUnknownLanguageTracks()
    {
        // IETF languages will only be set for MkvMerge
        // Select all tracks with undefined languages
        // Selected is Unknown
        // NotSelected is Known
        SelectMediaInfo selectMediaInfo = new(MkvMergeInfo, item => Language.IsUndefined(item.LanguageIetf));
        return selectMediaInfo;
    }

    public SelectMediaInfo FindNeedReEncode()
    {
        // Filter logic values are based on FfProbe attributes
        // Start with empty selection
        // Selected is ReEncode
        // NotSelected is Keep
        SelectMediaInfo selectMediaInfo = new(MediaTool.ToolType.FfProbe);

        // Add audio and video tracks
        // Select tracks matching the reencode lists
        FfProbeInfo.Video.ForEach(item => selectMediaInfo.Add(item, Program.Config.ProcessOptions.ReEncodeVideo.Any(item.CompareVideo)));
        FfProbeInfo.Audio.ForEach(item => selectMediaInfo.Add(item, Program.Config.ProcessOptions.ReEncodeAudioFormats.Contains(item.Format)));

        // Keep all subtitles
        selectMediaInfo.Add(FfProbeInfo.Subtitle, false);

        // If we are encoding audio, the video track may need to be reencoded at the same time
        // [matroska @ 00000195b3585c80] Timestamps are unset in a packet for stream 0.
        // [matroska @ 00000195b3585c80] Can't write packet with unknown timestamp
        // av_interleaved_write_frame(): Invalid argument
        if (selectMediaInfo.Selected.Audio.Count > 0 &&
            selectMediaInfo.Selected.Video.Count == 0)
        {
            // If the video is not H264, H265 or AV1 (by experimentation), then tag the video to also be reencoded
            var reEncodeVideo = selectMediaInfo.NotSelected.Video.FindAll(item => !ReEncodeVideoOnAudioReEncode.Contains(item.Format, StringComparer.OrdinalIgnoreCase));
            if (reEncodeVideo.Count > 0) 
            {
                selectMediaInfo.Move(reEncodeVideo, true);
                Log.Logger.Warning("Including incompatible Video track for Audio only ReEncoding");
            }
        }

        // Selected is ReEncode
        // NotSelected is Keep
        selectMediaInfo.SetState(TrackInfo.StateType.ReEncode, TrackInfo.StateType.Keep);
        return selectMediaInfo;
    }

    public SelectMediaInfo FindDuplicateTracks()
    {
        // IETF languages will only be set for MkvMerge
        // Start with all tracks as NotSelected
        // Selected is Keep
        // NotSelected is Remove
        SelectMediaInfo selectMediaInfo = new(MkvMergeInfo, false);

        // Get a track list
        var trackList = MkvMergeInfo.GetTrackList();

        // Get a list of all the IETF track languages
        var languageList = Language.GetLanguageList(trackList);
        foreach (var language in languageList)
        {
            // Get all tracks matching this language
            var trackLanguageList = trackList.FindAll(item => Language.IsEqual(language, item.LanguageIetf));

            // If multiple audio tracks exist for this language, keep the preferred audio codec track
            var audioTrackList = trackLanguageList.FindAll(item => item.GetType() == typeof(AudioInfo));
            if (audioTrackList.Count > 1)
            {
                var audioInfo = FindPreferredAudio(audioTrackList);
                selectMediaInfo.Move(audioInfo, true);
            }

            // Keep all tracks with flags
            var trackFlagList = trackLanguageList.FindAll(item => item.Flags != TrackInfo.FlagsType.None);
            selectMediaInfo.Move(trackFlagList, true);

            // Keep one non-flag track
            // E.g. for subtitles it could be forced, hearingimpaired, and one normal
            var videoNotFlagList = trackLanguageList.FindAll(item => item.Flags == TrackInfo.FlagsType.None && item.GetType() == typeof(VideoInfo));
            if (videoNotFlagList.Count > 0)
            {
                selectMediaInfo.Move(videoNotFlagList.First(), true);
            }
            var audioNotFlagList = trackLanguageList.FindAll(item => item.Flags == TrackInfo.FlagsType.None && item.GetType() == typeof(AudioInfo));
            if (audioNotFlagList.Count > 0)
            {
                selectMediaInfo.Move(audioNotFlagList.First(), true);
            }
            var subtitleNotFlagList = trackLanguageList.FindAll(item => item.Flags == TrackInfo.FlagsType.None && item.GetType() == typeof(SubtitleInfo));
            if (subtitleNotFlagList.Count > 0)
            {
                selectMediaInfo.Move(subtitleNotFlagList.First(), true);
            }
        }

        // We should have at least one of each kind of track, if any exists
        Debug.Assert(selectMediaInfo.Selected.Video.Count > 0 || MkvMergeInfo.Video.Count == 0);
        Debug.Assert(selectMediaInfo.Selected.Audio.Count > 0 || MkvMergeInfo.Audio.Count == 0);
        Debug.Assert(selectMediaInfo.Selected.Subtitle.Count > 0 || MkvMergeInfo.Subtitle.Count == 0);

        // Selected is Keep
        // NotSelected is Remove
        selectMediaInfo.SetState(TrackInfo.StateType.Keep, TrackInfo.StateType.Remove);
        return selectMediaInfo;
    }

    public SelectMediaInfo FindUnwantedLanguageTracks()
    {
        // Note that zxx, und, and the default language will always be added to Program.Config.ProcessOptions.KeepLanguages

        // IETF language field will only be set for MkvMerge
        // Select tracks with wanted languages, or the original language if set to keep
        // Selected is Keep
        // NotSelected is Remove
        SelectMediaInfo selectMediaInfo = new(MkvMergeInfo, item => Language.IsMatch(item.LanguageIetf, Program.Config.ProcessOptions.KeepLanguages) ||
                                                                    (Program.Config.ProcessOptions.KeepOriginalLanguage && item.Flags.HasFlag(TrackInfo.FlagsType.Original)));

        // Keep at least one video track if any
        if (selectMediaInfo.Selected.Video.Count == 0 && MkvMergeInfo.Video.Count > 0)
        {
            // Use the first track
            var videoInfo = MkvMergeInfo.Video.First();
            selectMediaInfo.Move(videoInfo, true);
            Log.Logger.Warning("No video track matching requested language : {Available} not in {Languages}, selecting {Selected}", Language.GetLanguageList(MkvMergeInfo.Video), Program.Config.ProcessOptions.KeepLanguages, videoInfo.LanguageIetf);
        }

        // Keep at least one audio track if any
        if (selectMediaInfo.Selected.Audio.Count == 0 && MkvMergeInfo.Audio.Count > 0)
        {
            // Use the preferred audio codec track from the unselected tracks
            var audioInfo = FindPreferredAudio(selectMediaInfo.NotSelected.Audio);
            selectMediaInfo.Move(audioInfo, true);
            Log.Logger.Warning("No audio track matching requested language : {Available} not in {Languages}, selecting {Selected}", Language.GetLanguageList(MkvMergeInfo.Audio), Program.Config.ProcessOptions.KeepLanguages, audioInfo.LanguageIetf);
        }

        // No language matching subtitle tracks
        if (selectMediaInfo.Selected.Subtitle.Count == 0 && MkvMergeInfo.Subtitle.Count > 0)
        {
            Log.Logger.Warning("No subtitle track matching requested language : {Available} not in {Languages}", Language.GetLanguageList(MkvMergeInfo.Subtitle), Program.Config.ProcessOptions.KeepLanguages);
        }

        // Selected is Keep
        // NotSelected is Remove
        selectMediaInfo.SetState(TrackInfo.StateType.Keep, TrackInfo.StateType.Remove);
        return selectMediaInfo;
    }

    static AudioInfo FindPreferredAudio(IEnumerable<TrackInfo> trackInfoList)
    {
        // No preferred tracks, or only 1 track, use first track
        Debug.Assert(trackInfoList.Count() > 0);
        if (Program.Config.ProcessOptions.PreferredAudioFormats.Count == 0 ||
            trackInfoList.Count() == 1)
        {
            return (AudioInfo)trackInfoList.First();
        }

        // Iterate through the preferred codecs in order
        var audioInfoList = trackInfoList.OfType<AudioInfo>().ToList();
        foreach (var codec in Program.Config.ProcessOptions.PreferredAudioFormats)
        {
            // Return on first match
            var audioInfo = audioInfoList.Find(item => item.Format.Equals(codec, StringComparison.OrdinalIgnoreCase));
            if (audioInfo != null)
            {
                Log.Logger.Information("Preferred audio codec selected : {Preferred} in {Codecs}", audioInfo.Format, audioInfoList.Select(item => item.Format));
                return audioInfo;
            }
        }

        // Return first item
        Log.Logger.Information("No audio codec matching preferred codecs : {Preferred} not in {Codecs}, Selecting {Selected}", Program.Config.ProcessOptions.PreferredAudioFormats, audioInfoList.Select(item => item.Format), audioInfoList.First().Codec);
        return audioInfoList.First();
    }

    public bool Modified { get; set; }
    public MediaInfo FfProbeInfo { get; private set; }
    public MediaInfo MkvMergeInfo { get; private set; }
    public MediaInfo MediaInfoInfo { get; private set; }
    public SidecarFile.StatesType State => SidecarFile.State;
    public FileInfo FileInfo { get; private set; }

    private SidecarFile SidecarFile;

    private const int RefreshWaitTime = 5;
    private static readonly string[] Hdr10Format = { "SMPTE ST 2086", "SMPTE ST 2094" };
    private static readonly string[] ReEncodeVideoOnAudioReEncode = { "h264", "hevc", "av1" };
}
