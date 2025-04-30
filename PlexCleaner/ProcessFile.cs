using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using InsaneGenius.Utilities;
using Serilog;

namespace PlexCleaner;

public class ProcessFile
{
    public ProcessFile(string mediaFile)
    {
        FileInfo = new FileInfo(mediaFile);
        _sidecarFile = new SidecarFile(FileInfo);
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
        string mediaFile = SidecarFile.GetMkvName(FileInfo);

        // Does the media file exist
        if (File.Exists(mediaFile))
        {
            // File exists, nothing more to do
            return true;
        }

        // Media file does not exists, delete this sidecar file
        Log.Information(
            "Deleting sidecar file with no matching MKV file : {FileName}",
            FileInfo.Name
        );

        // Delete the file
        if (!FileEx.DeleteFile(FileInfo.FullName))
        {
            // Error
            return false;
        }

        // File deleted, do not continue processing
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.FileDeleted;
        return false;
    }

    public bool DeleteNonMkvFile(ref bool modified)
    {
        // If MKV file nothing to do
        if (SidecarFile.IsMkvFile(FileInfo))
        {
            return true;
        }

        // Only delete if the option is enabled else just skip
        if (!Program.Config.ProcessOptions.DeleteUnwantedExtensions)
        {
            Log.Warning("Skipping non-MKV file : {FileName}", FileInfo.Name);
            return false;
        }

        // Non-MKV file, delete
        Log.Warning("Deleting non-MKV file : {FileName}", FileInfo.Name);

        // Delete the file
        if (!FileEx.DeleteFile(FileInfo.FullName))
        {
            // Error
            return false;
        }

        // File deleted, do not continue processing
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.FileDeleted;
        return false;
    }

    public bool MakeExtensionLowercase(ref bool modified)
    {
        // Is the extension lowercase
        string lowerExtension = FileInfo.Extension.ToLowerInvariant();
        if (FileInfo.Extension.Equals(lowerExtension, StringComparison.Ordinal))
        {
            return true;
        }

        // Make the extension lowercase
        Log.Information("Making file extension lowercase : {FileName}", FileInfo.Name);

        // Rename the file
        // Windows is case insensitive, so we need to rename in two steps
        string tempName = Path.ChangeExtension(FileInfo.FullName, ".tmp7");
        string lowerName = Path.ChangeExtension(FileInfo.FullName, lowerExtension);
        if (
            !FileEx.RenameFile(FileInfo.FullName, tempName)
            || !FileEx.RenameFile(tempName, lowerName)
        )
        {
            // TODO: Chance of partial failure if only one rename succeeds
            return false;
        }

        // Modified filename
        modified = true;
        return Refresh(lowerName);
    }

    public bool IsWriteable() => FileInfo.Exists && !FileInfo.IsReadOnly;

    public bool IsSidecarAvailable() => _sidecarFile.Exists();

    public bool IsSidecarWriteable() => _sidecarFile.IsWriteable();

    public bool RemuxByExtension(bool conditional, ref bool modified)
    {
        // Optional
        if (conditional && !Program.Config.ProcessOptions.ReMux)
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
        Log.Information("ReMux file matched by extension : {FileName}", FileInfo.Name);

        // ReMux the file, use the new filename
        if (!Convert.ReMuxToMkv(FileInfo.FullName, out string outputName))
        {
            // Error
            return false;
        }

        // Extension may have changed
        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.ReMuxed;
        return Refresh(outputName);
    }

    public bool RemuxNonMkvContainer(ref bool modified)
    {
        // Make sure that MKV named files are Matroska containers
        if (MkvMergeTool.IsMkvContainer(MkvMergeInfo))
        {
            // Nothing to do
            return true;
        }

        // Optional, but required for correct processing
        if (!Program.Config.ProcessOptions.ReMux)
        {
            // Error, MKV files must be Matroska, enable ReMux option
            Log.Error(
                "MKV file is not in Matroska format, ReMux option not enabled : Container: {Container} : {FileName}",
                MkvMergeInfo.Container,
                FileInfo.Name
            );
            return false;
        }

        // ReMux the file
        Log.Information(
            "ReMux {Container} to Matroska : {FileName}",
            MkvMergeInfo.Container,
            FileInfo.Name
        );

        // ReMux the file, use the new filename
        if (!Convert.ReMuxToMkv(FileInfo.FullName, out string outputName))
        {
            // Error
            return false;
        }

        // Extension may have changed
        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.ReMuxed;
        return Refresh(outputName);
    }

    public bool HasMetadataErrors() =>
        FfProbeInfo.AnyErrors || MkvMergeInfo.AnyErrors || MediaInfoInfo.AnyErrors;

    public bool HasMetadataErrors(TrackInfo.StateType stateType) =>
        FfProbeInfo.GetTrackList().Any(item => item.State == stateType)
        || MkvMergeInfo.GetTrackList().Any(item => item.State == stateType)
        || MediaInfoInfo.GetTrackList().Any(item => item.State == stateType);

    public void ClearMetadataErrors()
    {
        // Clear all the error flags
        FfProbeInfo.HasErrors = false;
        MkvMergeInfo.HasErrors = false;
        MediaInfoInfo.HasErrors = false;
        FfProbeInfo.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.None);
        MkvMergeInfo.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.None);
        MediaInfoInfo.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.None);
    }

    public bool RepairMetadataErrors(ref bool modified)
    {
        // Conditional
        if (!Program.Config.VerifyOptions.AutoRepair)
        {
            // If repair is not enabled just clear any error states
            ClearMetadataErrors();
            return true;
        }

        // Any metadata errors to repair
        if (!HasMetadataErrors())
        {
            // Nothing to do
            return true;
        }

        // Start with remuxing
        if (!RepairMetadataRemux(ref modified))
        {
            return false;
        }

        // Any metadata errors left to repair after remuxing
        if (!HasMetadataErrors())
        {
            // Done
            return true;
        }

        // Set track flags
        return RepairMetadataFlags(ref modified);
    }

    public bool RepairMetadataRemux(ref bool modified)
    {
        // Conditional
        if (!Program.Config.ProcessOptions.ReMux)
        {
            // Done
            return true;
        }

        // Per MkvToolNix docs remux is the recommended approach to correcting language tags
        // Honor Program.Config.ProcessOptions.SetIetfLanguageTags as lots of older media does not have IETF tags set

        // Any tracks need remuxing
        if (
            !HasMetadataErrors(TrackInfo.StateType.Remove)
            && !HasMetadataErrors(TrackInfo.StateType.ReMux)
            && !(
                Program.Config.ProcessOptions.SetIetfLanguageTags
                && HasMetadataErrors(TrackInfo.StateType.SetLanguage)
            )
        )
        {
            // Done
            return true;
        }

        // Already remuxed?
        if (_sidecarFile.State.HasFlag(SidecarFile.StatesType.ReMuxed))
        {
            Log.Warning("Metadata errors re-detected after remuxing : {FileName}", FileInfo.Name);
        }

        // Start with keeping all tracks
        // Selected is Keep
        // NotSelected is Remove
        SelectMediaInfo selectMediaInfo = new(MkvMergeInfo, true);

        // TODO: Remove is currently only set by MediaInfo for subtitle tracks that need to be removed
        // Mapping of track Id's are non-trivial, use the Matroska header track number to find the matching tracks
        List<TrackInfo> mediaInfoRemoveList = MediaInfoInfo
            .GetTrackList()
            .FindAll(item => item.State == TrackInfo.StateType.Remove);
        List<TrackInfo> mkvMergeRemoveList = MkvMergeInfo.MatchMediaInfoToMkvMerge(
            mediaInfoRemoveList
        );
        mkvMergeRemoveList.ForEach(item => item.State = TrackInfo.StateType.Remove);
        Debug.Assert(mediaInfoRemoveList.Count == mkvMergeRemoveList.Count);

        // To be removed tracks
        selectMediaInfo.Move(mkvMergeRemoveList, false);

        // Do not call SetState() on items that are not in scope, further processing is done by state

        // ReMux the file
        Log.Information("ReMux to repair metadata errors : {FileName}", FileInfo.Name);
        selectMediaInfo.WriteLine("Keep", "Remove");

        // Conditional with tracks or all tracks
        if (
            !Convert.ReMuxToMkv(
                FileInfo.FullName,
                selectMediaInfo.NotSelected.Count > 0 ? selectMediaInfo : null,
                out string outputName
            )
        )
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.ReMuxed;
        return Refresh(outputName);
    }

    public bool RemuxRemoveExtraVideoTracks(ref bool modified)
    {
        if (MkvMergeInfo.Video.Count <= 1)
        {
            // Nothing to do
            return true;
        }

        // Conditional
        if (!Program.Config.ProcessOptions.ReMux)
        {
            // Error, multiple video tracks are not supported, enable ReMux option
            Log.Error(
                "Multiple video tracks not supported, ReMux option not enabled : Video: {TrackCount} : {FileName}",
                MkvMergeInfo.Video.Count,
                FileInfo.Name
            );
            return false;
        }

        // Start with keeping all tracks
        // Selected is Keep
        // NotSelected is Remove
        SelectMediaInfo selectMediaInfo = new(MkvMergeInfo, true);

        // Remove all but first video track
        List<VideoInfo> mkvMergeRemoveList = [.. MkvMergeInfo.Video.Skip(1)];
        mkvMergeRemoveList.ForEach(item => item.State = TrackInfo.StateType.Remove);

        // To be removed tracks
        selectMediaInfo.Move(mkvMergeRemoveList, false);

        // ReMux the file
        Log.Information("ReMux to remove extra video tracks : {FileName}", FileInfo.Name);
        selectMediaInfo.WriteLine("Keep", "Remove");
        if (!Convert.ReMuxToMkv(FileInfo.FullName, selectMediaInfo, out string outputName))
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.ReMuxed;
        return Refresh(outputName);
    }

    public bool RepairMetadataFlags(ref bool modified)
    {
        // Conditional
        if (!Program.Config.ProcessOptions.SetTrackFlags)
        {
            // Done
            return true;
        }

        // Any tracks to set flags on
        if (!HasMetadataErrors(TrackInfo.StateType.SetFlags))
        {
            // Nothing to do
            return true;
        }

        // Already flagged?
        if (_sidecarFile.State.HasFlag(SidecarFile.StatesType.SetFlags))
        {
            Log.Warning(
                "Metadata errors re-detected after setting flags : {FileName}",
                FileInfo.Name
            );
        }

        // Setting flags
        Log.Information("Setting track flags on media file : {FileName}", FileInfo.Name);

        // Set flags using MkvMergeInfo
        Debug.Assert(
            MkvMergeInfo.GetTrackList().Any(item => item.State == TrackInfo.StateType.SetFlags)
        );
        if (!Tools.MkvPropEdit.SetTrackFlags(FileInfo.FullName, MkvMergeInfo))
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.SetFlags;
        return Refresh(true);
    }

    public bool AnyTags() => MkvMergeInfo.AnyTags || FfProbeInfo.AnyTags || MediaInfoInfo.AnyTags;

    public bool RemoveTags(ref bool modified, bool ignoreConfig = false)
    {
        // Optional
        if (!ignoreConfig && !Program.Config.ProcessOptions.RemoveTags)
        {
            return true;
        }

        // Remove attachments
        if (!RemoveAttachments(ref modified))
        {
            // Error
            return false;
        }

        // Does the file have tags
        if (!AnyTags())
        {
            // No tags
            return true;
        }

        // Already cleared?
        // Tags can re-appear after running FfMpeg or HandBrake
        if (_sidecarFile.State.HasFlag(SidecarFile.StatesType.ClearedTags))
        {
            Log.Warning("Tags re-detected after clearing : {FileName}", FileInfo.Name);
        }

        // Remove tags
        Log.Information("Clearing all tags from media file : {FileName}", FileInfo.Name);

        // Delete the tags
        if (!Tools.MkvPropEdit.ClearTags(FileInfo.FullName, MkvMergeInfo))
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.ClearedTags;
        return Refresh(true);
    }

    public bool RemoveAttachments(ref bool modified)
    {
        // Any attachments, use MkvMergeInfo
        if (MkvMergeInfo.Attachments == 0)
        {
            // No attachments
            return true;
        }

        // Already removed?
        if (_sidecarFile.State.HasFlag(SidecarFile.StatesType.RemovedAttachments))
        {
            Log.Warning("Attachments re-detected after clearing : {FileName}", FileInfo.Name);
        }

        // Remove attachments
        Log.Information("Clearing attachments from media file : {FileName}", FileInfo.Name);

        // Delete the attachments
        if (!Tools.MkvPropEdit.ClearAttachments(FileInfo.FullName, MkvMergeInfo))
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.RemovedAttachments;
        return Refresh(true);
    }

    public bool RemoveCoverArt(ref bool modified)
    {
        // Any cover art
        if (!MkvMergeInfo.HasCovertArt && !FfProbeInfo.HasCovertArt && !MediaInfoInfo.HasCovertArt)
        {
            // Nothing to do
            return true;
        }

        // Conditional
        if (!Program.Config.ProcessOptions.ReMux)
        {
            // Error, cover art interferes with processing and must be removed, enable ReMux option
            Log.Error(
                "Cover Art must be removed, ReMux option not enabled : {FileName}",
                FileInfo.Name
            );
            return false;
        }

        // Cover art can be detected by MediaInfo or FfMpeg or MkvMerge
        // Process MkvMerge first, sometimes FfProbe detects attachments, and sometimes it detects video streams

        // Any MkvMergeInfo cover art
        if (MkvMergeInfo.HasCovertArt && !RemoveCoverArtMkvMerge(ref modified))
        {
            // Error
            return false;
        }

        // Any FfProbe cover art
        if (FfProbeInfo.HasCovertArt && !RemoveCoverArtFfProbe(ref modified))
        {
            // Error
            return false;
        }

        // Did we get it all?
        Debug.Assert(
            !MkvMergeInfo.HasCovertArt && !FfProbeInfo.HasCovertArt && !MediaInfoInfo.HasCovertArt
        );

        // Done
        return true;
    }

    public bool RemoveCoverArtFfProbe(ref bool modified)
    {
        // Any FfProbeInfo cover art
        if (!FfProbeInfo.HasCovertArt)
        {
            // No cover art
            return true;
        }

        // Remove attachments, if any
        if (!RemoveAttachments(ref modified))
        {
            // Error
            return false;
        }

        // Any FfProbeInfo cover art
        if (!FfProbeInfo.HasCovertArt)
        {
            // No cover art
            return true;
        }

        // Remove tags, if any
        if (!RemoveTags(ref modified, true))
        {
            // Error
            return false;
        }

        // Done
        return true;
    }

    public bool RemoveCoverArtMkvMerge(ref bool modified)
    {
        // Any MkvMergeInfo cover art
        if (!MkvMergeInfo.HasCovertArt)
        {
            // No cover art
            return true;
        }

        // Already removed?
        if (_sidecarFile.State.HasFlag(SidecarFile.StatesType.RemovedCoverArt))
        {
            Log.Warning("Cover Art re-detected after removing : {FileName}", FileInfo.Name);
        }

        // Select all tracks with cover art
        // Use MkvMerge for cover art logic
        // Selected is Keep
        // NotSelected is Remove
        SelectMediaInfo selectMediaInfo = new(MkvMergeInfo, true);
        selectMediaInfo.Move(MkvMergeInfo.Video.Find(item => item.IsCoverArt), false);

        // There must be something left to keep
        Debug.Assert(selectMediaInfo.Selected.Count > 0);
        selectMediaInfo.SetState(TrackInfo.StateType.Keep, TrackInfo.StateType.Remove);

        Log.Information("Removing Cover Art from media file : {FileName}", FileInfo.Name);
        selectMediaInfo.WriteLine("Keep", "Remove");

        // ReMux and only keep the selected tracks
        if (!Convert.ReMuxToMkv(FileInfo.FullName, selectMediaInfo, out string outputName))
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.ReMuxed;
        _sidecarFile.State |= SidecarFile.StatesType.RemovedCoverArt;
        return Refresh(outputName);
    }

    public bool SetUnknownLanguageTracks(ref bool modified)
    {
        // Conditional
        if (!Program.Config.ProcessOptions.SetUnknownLanguage)
        {
            return true;
        }

        // Select all tracks with unknown languages
        // Use MkvMerge for IETF language tags
        // Selected is Unknown
        // NotSelected is Known
        SelectMediaInfo selectMediaInfo = FindUnknownLanguageTracks();
        if (selectMediaInfo.Selected.Count == 0)
        {
            // Nothing to do
            return true;
        }

        Log.Information(
            "Setting unknown language tracks to {DefaultLanguage} : {FileName}",
            Program.Config.ProcessOptions.DefaultLanguage,
            FileInfo.Name
        );
        selectMediaInfo.WriteLine("Unknown", "Known");

        // Set the track language to the default language
        selectMediaInfo
            .Selected.GetTrackList()
            .ForEach(item => item.LanguageIetf = Program.Config.ProcessOptions.DefaultLanguage);

        // Set languages
        if (!Tools.MkvPropEdit.SetTrackLanguage(FileInfo.FullName, selectMediaInfo.Selected))
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.SetLanguage;
        return Refresh(true);
    }

    public bool RemoveUnwantedLanguageTracks(ref bool modified)
    {
        // Conditional
        if (!Program.Config.ProcessOptions.RemoveUnwantedLanguageTracks)
        {
            return true;
        }

        // Use MkvMerge for IETF language tags
        // Selected is Keep
        // NotSelected is Remove
        SelectMediaInfo selectMediaInfo = FindUnwantedLanguageTracks();
        if (selectMediaInfo.NotSelected.Count == 0)
        {
            // Done
            return true;
        }

        // There must be something left to keep
        Debug.Assert(selectMediaInfo.Selected.Count > 0);

        Log.Information("Removing unwanted language tracks : {FileName}", FileInfo.Name);
        selectMediaInfo.WriteLine("Keep", "Remove");

        // ReMux and only keep the selected tracks
        if (!Convert.ReMuxToMkv(FileInfo.FullName, selectMediaInfo, out string outputName))
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.ReMuxed;
        return Refresh(outputName);
    }

    public bool RemoveDuplicateTracks(ref bool modified)
    {
        // Conditional
        if (!Program.Config.ProcessOptions.RemoveDuplicateTracks)
        {
            return true;
        }

        // Use MkvMerge logic
        // Selected is Keep
        // NotSelected is Remove
        SelectMediaInfo selectMediaInfo = FindDuplicateTracks();
        if (selectMediaInfo.NotSelected.Count == 0)
        {
            // Done
            return true;
        }

        // There must be something left to keep
        Debug.Assert(selectMediaInfo.Selected.Count > 0);

        Log.Information("Removing duplicate tracks : {FileName}", FileInfo.Name);
        selectMediaInfo.WriteLine("Keep", "Remove");

        // ReMux and only keep the specified tracks
        if (!Convert.ReMuxToMkv(FileInfo.FullName, selectMediaInfo, out string outputName))
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.ReMuxed;
        return Refresh(outputName);
    }

    private bool FindInterlacedTracks(bool conditional, out VideoInfo videoInfo)
    {
        // Return false on error
        // Set videoInfo if interlaced

        // Any video tracks
        videoInfo = null;
        if (FfProbeInfo.Video.Count == 0)
        {
            // No video tracks
            return true;
        }

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
        if (
            conditional
            && (
                State.HasFlag(SidecarFile.StatesType.Verified)
                || State.HasFlag(SidecarFile.StatesType.VerifyFailed)
                || State.HasFlag(SidecarFile.StatesType.DeInterlaced)
            )
        )
        {
            // Assume not interlaced
            return true;
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
            return true;
        }

        // Idet said yes but metadata said no
        Log.Warning(
            "Idet reported interlaced, metadata reported not interlaced : {FileName}",
            FileInfo.Name
        );

        // Use the first video track from FfProbe
        videoInfo = FfProbeInfo.Video.First();
        videoInfo.Interlaced = true;

        return true;
    }

    private bool FindClosedCaptionTracks(bool conditional, out VideoInfo videoInfo)
    {
        // Return false on error
        // Set videoInfo if contains closed captions

        // Any video tracks
        videoInfo = null;
        if (FfProbeInfo.Video.Count == 0)
        {
            // No video tracks
            return true;
        }

        // Are any closed caption attributes set
        videoInfo ??= FfProbeInfo.Video.Find(item => item.ClosedCaptions);
        videoInfo ??= MediaInfoInfo.Video.Find(item => item.ClosedCaptions);
        videoInfo ??= MkvMergeInfo.Video.Find(item => item.ClosedCaptions);
        if (videoInfo != null)
        {
            // Attribute set
            return true;
        }

        // Running lavfi is expensive, skip if already verified or closed captions already removed
        if (
            conditional
            && (
                State.HasFlag(SidecarFile.StatesType.Verified)
                || State.HasFlag(SidecarFile.StatesType.VerifyFailed)
                || State.HasFlag(SidecarFile.StatesType.ClearedCaptions)
            )
        )
        {
            // Assume no closed captions
            return true;
        }

        // Get packet info using ccsub filter
        Log.Information("Finding Closed Captions in video stream : {FileName}", FileInfo.Name);
        if (
            !Tools.FfProbe.GetSubCcPacketInfo(
                FileInfo.FullName,
                out List<FfMpegToolJsonSchema.Packet> packetList
            )
        )
        {
            // Error
            return false;
        }

        // Any packets means there are subtitles present in the video stream
        if (packetList.Count > 0)
        {
            // Use the first video track from FfProbe
            videoInfo = FfProbeInfo.Video.First();
            videoInfo.ClosedCaptions = true;
        }

        return true;
    }

    public bool DeInterlace(bool conditional, ref bool modified)
    {
        // Conditional
        if (conditional && !Program.Config.ProcessOptions.DeInterlace)
        {
            return true;
        }

        // Do we have any interlaced video
        if (!FindInterlacedTracks(conditional, out VideoInfo videoInfo))
        {
            // Error
            return false;
        }
        if (videoInfo == null)
        {
            // Not interlaced
            return true;
        }

        // Already deinterlaced?
        if (State.HasFlag(SidecarFile.StatesType.DeInterlaced))
        {
            Log.Warning("DeInterlacing already deinterlaced media : {FileName}", FileInfo.Name);
        }

        Log.Information("DeInterlacing interlaced media : {FileName}", FileInfo.Name);
        videoInfo.State = TrackInfo.StateType.DeInterlace;
        videoInfo.WriteLine("Interlaced");

        // TODO: HandBrake will convert closed captions and subtitle tracks to ASS format
        // To work around this we will deinterlace without subtitles then add the subtitles back
        // https://github.com/ptr727/PlexCleaner/issues/95

        // Create a temp filename for the deinterlaced output
        string deintName = Path.ChangeExtension(FileInfo.FullName, ".tmp8");
        Debug.Assert(FileInfo.FullName != deintName);

        // DeInterlace using HandBrake and ignore subtitles
        _ = FileEx.DeleteFile(deintName);
        if (!Tools.HandBrake.ConvertToMkv(FileInfo.FullName, deintName, false, true))
        {
            // Error
            _ = FileEx.DeleteFile(deintName);
            return false;
        }

        // Create a temp filename for the remuxed output
        string remuxName = Path.ChangeExtension(FileInfo.FullName, ".tmp9");
        Debug.Assert(FileInfo.FullName != remuxName);

        // If there are subtitles in the original file merge them back
        if (MkvMergeInfo.Subtitle.Count == 0)
        {
            Log.Information("ReMuxing deinterlaced media : {FileName}", FileInfo.Name);

            // No subtitles, just remux all content
            _ = FileEx.DeleteFile(remuxName);
            if (!Tools.MkvMerge.ReMuxToMkv(deintName, remuxName))
            {
                // Error
                _ = FileEx.DeleteFile(deintName);
                _ = FileEx.DeleteFile(remuxName);
                return false;
            }
        }
        else
        {
            Log.Information(
                "ReMuxing subtitles and deinterlaced media : {FileName}",
                FileInfo.Name
            );

            // Merge the deinterlaced file with the subtitles from the original file
            MediaInfo subInfo = new(MediaTool.ToolType.MkvMerge);
            subInfo.Subtitle.AddRange(MkvMergeInfo.Subtitle);
            _ = FileEx.DeleteFile(remuxName);
            if (!Tools.MkvMerge.MergeToMkv(deintName, FileInfo.FullName, subInfo, remuxName))
            {
                // Error
                _ = FileEx.DeleteFile(deintName);
                _ = FileEx.DeleteFile(remuxName);
                return false;
            }
        }

        // Delete the temp files and rename the output
        _ = FileEx.DeleteFile(deintName);
        if (!FileEx.RenameFile(remuxName, FileInfo.FullName))
        {
            // Error
            _ = FileEx.DeleteFile(remuxName);
            return false;
        }

        // Clone the original MkvMergeInfo
        MediaInfo postMkvMerge = MkvMergeInfo.Clone();

        // The remuxed output will be [Video] [Audio] [Subtitles]
        // Reset the track numbers to be in the expected order
        int trackNumber = 1;
        postMkvMerge.Video.Clear();
        postMkvMerge.Video.AddRange(MkvMergeInfo.Video);
        postMkvMerge.Video.ForEach(item => item.Number = trackNumber++);
        postMkvMerge.Audio.Clear();
        postMkvMerge.Audio.AddRange(MkvMergeInfo.Audio);
        postMkvMerge.Audio.ForEach(item => item.Number = trackNumber++);
        postMkvMerge.Subtitle.Clear();
        postMkvMerge.Subtitle.AddRange(MkvMergeInfo.Subtitle);
        postMkvMerge.Subtitle.ForEach(item => item.Number = trackNumber++);

        // FfMpeg and HandBrake discards IETF language tags, restore them after encoding and deinterlacing
        // https://github.com/ptr727/PlexCleaner/issues/148
        if (!Tools.MkvPropEdit.SetTrackLanguage(FileInfo.FullName, postMkvMerge))
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.DeInterlaced;
        _sidecarFile.State |= SidecarFile.StatesType.ReMuxed;
        if (!Refresh(true))
        {
            return false;
        }

        // Verify that the pre- and post- info is using the same track numbers
        // If this fails then SetTrackLanguage() will have used the wrong tracks
        if (!MkvMergeInfo.VerifyTrackOrder(postMkvMerge))
        {
            Log.Error("MkvMerge and HandBrake track metadata does not match");
            Debug.Assert(false);
            return false;
        }
        return true;
    }

    public bool RemoveClosedCaptions(bool conditional, ref bool modified)
    {
        // Conditional
        if (conditional && !Program.Config.ProcessOptions.RemoveClosedCaptions)
        {
            return true;
        }

        // Do we have any closed captions
        if (!FindClosedCaptionTracks(conditional, out VideoInfo videoInfo))
        {
            // Error
            return false;
        }
        if (videoInfo == null)
        {
            // No closed captions
            return true;
        }

        // Already removed?
        if (_sidecarFile.State.HasFlag(SidecarFile.StatesType.ClearedCaptions))
        {
            Log.Warning("Closed Captions re-detected after removing : {FileName}", FileInfo.Name);
        }

        Log.Information("Removing Closed Captions from video stream : {FileName}", FileInfo.Name);
        videoInfo.WriteLine("Closed Captions");

        // Get SEI NAL unit based on video format
        int nalUnit = FfMpegTool.GetNalUnit(videoInfo.Format);
        if (nalUnit == default)
        {
            // Error
            Log.Error(
                "Unsupported video format for Closed Captions removal : Format: {Format} : {FileName}",
                videoInfo.Format,
                FileInfo.Name
            );
            return false;
        }

        // https://trac.ffmpeg.org/ticket/5283
        // TODO: HDR10+ information may be removed from H265 content
        // Use MediaInfo tags
        VideoInfo mediaInfoVideo = MediaInfoInfo.Video.First();
        if (
            Hdr10FormatList.Any(item =>
                mediaInfoVideo.FormatHdr.Contains(item, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            // TODO: Treating this as an error until verified with actual examples
            // Error
            Log.Error(
                "Removing Closed Captions may remove HDR10+ information : {Format} : {FileName}",
                mediaInfoVideo.FormatHdr,
                FileInfo.Name
            );
            return false;
        }

        // Create a temp output filename
        string tempName = Path.ChangeExtension(FileInfo.FullName, ".tmp10");
        Debug.Assert(FileInfo.FullName != tempName);

        // Remove Closed Captions
        _ = FileEx.DeleteFile(tempName);
        Log.Information("Removing Closed Captions using FfMpeg : {FileName}", FileInfo.Name);
        if (!Tools.FfMpeg.RemoveNalUnits(FileInfo.FullName, nalUnit, tempName))
        {
            // Error
            Log.Error("Removing Closed Captions using FfMpeg failed : {FileName}", FileInfo.Name);
            _ = FileEx.DeleteFile(tempName);
            return false;
        }

        // ReMux using MkvMerge after FfMpeg encoding
        Log.Information("ReMuxing reencoded media : {FileName}", FileInfo.Name);
        if (!Convert.ReMux(tempName))
        {
            // Error
            _ = FileEx.DeleteFile(tempName);
            return false;
        }

        // Rename the temp file to the original file
        if (!FileEx.RenameFile(tempName, FileInfo.FullName))
        {
            _ = FileEx.DeleteFile(tempName);
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.ClearedCaptions;
        _sidecarFile.State |= SidecarFile.StatesType.ReMuxed;
        return Refresh(true);
    }

    public bool RemoveSubtitles(ref bool modified)
    {
        // Only called by MkvProcess, not currently used in Process logic

        // Do we have any subtitles
        if (MkvMergeInfo.Subtitle.Count == 0)
        {
            // No subtitles to remove
            return true;
        }

        Log.Information("Removing Subtitles from video stream : {FileName}", FileInfo.Name);
        MkvMergeInfo.Subtitle.ForEach(item => item.WriteLine("Subtitles"));

        // Create a temp output filename
        string tempName = Path.ChangeExtension(FileInfo.FullName, ".tmp6");
        Debug.Assert(FileInfo.FullName != tempName);

        // Remove Subtitles
        _ = FileEx.DeleteFile(tempName);
        Log.Information("Removing Subtitles using MkvMerge : {FileName}", FileInfo.Name);
        if (!Tools.MkvMerge.RemoveSubtitles(FileInfo.FullName, tempName))
        {
            // Error
            Log.Error("Removing Subtitles using MkvMerge failed : {FileName}", FileInfo.Name);
            _ = FileEx.DeleteFile(tempName);
            return false;
        }

        // Rename the temp file to the original file
        if (!FileEx.RenameFile(tempName, FileInfo.FullName))
        {
            _ = FileEx.DeleteFile(tempName);
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.ReMuxed;
        return Refresh(true);
    }

    public bool ReEncode(bool conditional, ref bool modified)
    {
        // Conditional
        if (conditional && !Program.Config.ProcessOptions.ReEncode)
        {
            return true;
        }

        // Find all tracks that need re-encoding
        // Use FfProbeInfo for matching logic
        // Selected is ReEncode
        // NotSelected is Keep
        SelectMediaInfo selectMediaInfo = FindNeedReEncode();
        if (selectMediaInfo.Selected.Count == 0)
        {
            // Done
            return true;
        }

        // Already reencoded?
        if (State.HasFlag(SidecarFile.StatesType.ReEncoded))
        {
            Log.Warning("ReEncoding already reencoded media : {FileName}", FileInfo.Name);
        }

        Log.Information("ReEncoding required tracks : {FileName}", FileInfo.Name);
        selectMediaInfo.WriteLine("ReEncode", "Passthrough");

        // ReEncode selected tracks
        if (!Convert.ConvertToMkv(FileInfo.FullName, selectMediaInfo, out string outputName))
        {
            // Convert will log error
            // Error
            return false;
        }

        // ReMux using MkvMerge after FfMpeg encoding
        Log.Information("ReMuxing reencoded media : {FileName}", FileInfo.Name);
        if (!Convert.ReMux(outputName))
        {
            // Error
            return false;
        }

        // The FfMpeg map is constructed using the same order as the original file
        // No need to adjust the track numbers
        MediaInfo postMkvMerge = MkvMergeInfo.Clone();

        // FfMpeg and HandBrake discards IETF language tags, restore them after encoding and deinterlacing
        // https://github.com/ptr727/PlexCleaner/issues/148
        if (!Tools.MkvPropEdit.SetTrackLanguage(outputName, postMkvMerge))
        {
            // Error
            return false;
        }

        // Refresh
        modified = true;
        _sidecarFile.State |= SidecarFile.StatesType.ReEncoded;
        _sidecarFile.State |= SidecarFile.StatesType.ReMuxed;
        if (!Refresh(outputName))
        {
            return false;
        }

        // Verify that the pre- and post- info is using the same track numbers
        // If this fails then SetTrackLanguage() will have used the wrong tracks
        if (!MkvMergeInfo.VerifyTrackOrder(postMkvMerge))
        {
            Log.Error("MkvMerge and FfMpeg track metadata does not match");
            Debug.Assert(false);
            return false;
        }
        return true;
    }

    public bool VerifyTrackCounts()
    {
        // Use MkvMergeInfo

        // Tooling supports one and only one video track
        if (MkvMergeInfo.Video.Count == 0)
        {
            Log.Error("File missing video track : {FileName}", FileInfo.Name);
            MkvMergeInfo.WriteLine("Unsupported");

            // Error
            return false;
        }
        if (MkvMergeInfo.Video.Count > 1)
        {
            Log.Error(
                "File has more than one video track : Video: {Video} : {FileName}",
                MkvMergeInfo.Video.Count,
                FileInfo.Name
            );
            MkvMergeInfo.WriteLine("Unsupported");

            // Error
            return false;
        }

        // Warn if audio track is missing
        if (MkvMergeInfo.Audio.Count == 0)
        {
            Log.Warning("File missing audio : {FileName}", FileInfo.Name);
            MkvMergeInfo.WriteLine("Missing");

            // Warning only
        }

        return true;
    }

    public bool Verify(bool conditional, out bool canRepair)
    {
        // Do not set any state, caller will set state

        // Init
        canRepair = false;

        // Conditional
        if (conditional && !Program.Config.ProcessOptions.Verify)
        {
            // Done
            return true;
        }

        // Skip if Verified
        if (conditional && _sidecarFile.State.HasFlag(SidecarFile.StatesType.Verified))
        {
            Debug.Assert(!_sidecarFile.State.HasFlag(SidecarFile.StatesType.VerifyFailed));
            Debug.Assert(!_sidecarFile.State.HasFlag(SidecarFile.StatesType.RepairFailed));
            return true;
        }

        // Skip if VerifyFailed
        if (conditional && _sidecarFile.State.HasFlag(SidecarFile.StatesType.VerifyFailed))
        {
            Debug.Assert(!_sidecarFile.State.HasFlag(SidecarFile.StatesType.Verified));
            Debug.Assert(!_sidecarFile.State.HasFlag(SidecarFile.StatesType.Repaired));
            return false;
        }

        // Verify track counts
        if (!VerifyTrackCounts())
        {
            // Error, can't repair
            return false;
        }

        // Verify track flags
        // Warning only
        _ = VerifyTrackFlags();

        // Verify HDR profile
        // Warning only
        _ = VerifyHdrProfile();

        // Verify bitrate
        // Warning only
        // Will update sidecar state if bitrate exceeded
        _ = VerifyBitrate();

        // Verify media streams, repair is possible
        canRepair = true;
        return VerifyMediaStreams(FileInfo);
    }

    public static bool VerifyMediaStreams(FileInfo fileInfo)
    {
        // Verify
        Log.Information("Verifying media streams : {FileName}", fileInfo.Name);
        if (!Tools.FfMpeg.VerifyMedia(fileInfo.FullName, out string error))
        {
            // Cancel requested
            if (Program.IsCancelledError())
            {
                return false;
            }

            // Failed stream validation
            Log.Error("Media stream validation failed : {FileName}", fileInfo.Name);
            Log.Error("{Error}", error);

            // Caller should update the state
            return false;
        }

        // Verified
        return true;
    }

    public bool DeleteFailedFile()
    {
        // Conditional
        if (!Program.Config.VerifyOptions.DeleteInvalidFiles)
        {
            // Return false if not deleted
            return false;
        }

        // Delete the media file and sidecar file
        // Ignore delete errors
        Log.Warning("Deleting media file that failed processing : {FileName}", FileInfo.FullName);
        _ = FileEx.DeleteFile(FileInfo.FullName);
        _ = FileEx.DeleteFile(SidecarFile.GetSidecarName(FileInfo));

        // Set the sidecar state as deleted
        // Sidecar file no longer exists, but in-memory state does
        _sidecarFile.State |= SidecarFile.StatesType.FileDeleted;

        // Return true if deleted
        return true;
    }

    public bool VerifyAndRepair(ref bool modified)
    {
        // Verify (verify does not set any state, it just tests state)
        if (Verify(true, out bool canRepair))
        {
            // Set Verified state if not already set
            if (!_sidecarFile.State.HasFlag(SidecarFile.StatesType.Verified))
            {
                _sidecarFile.State |= SidecarFile.StatesType.Verified;
                _sidecarFile.State &= ~SidecarFile.StatesType.VerifyFailed;
                Debug.Assert(!_sidecarFile.State.HasFlag(SidecarFile.StatesType.RepairFailed));

                // Update state
                return Refresh(false);
            }

            // Done
            return true;
        }

        // Cancel requested
        if (Program.IsCancelled())
        {
            return false;
        }

        // Verify failed and can't repair
        // Or previous repair failed
        // Or repair not enabled
        if (
            !canRepair
            || _sidecarFile.State.HasFlag(SidecarFile.StatesType.RepairFailed)
            || !Program.Config.VerifyOptions.AutoRepair
        )
        {
            // Set VerifyFailed state if not already set
            if (!_sidecarFile.State.HasFlag(SidecarFile.StatesType.VerifyFailed))
            {
                _sidecarFile.State |= SidecarFile.StatesType.VerifyFailed;
                _sidecarFile.State &= ~SidecarFile.StatesType.Verified;
                Debug.Assert(!_sidecarFile.State.HasFlag(SidecarFile.StatesType.Repaired));

                // Update state
                _ = Refresh(false);
            }

            // Error
            return false;
        }

        // Sanity check
        Debug.Assert(!_sidecarFile.State.HasFlag(SidecarFile.StatesType.Verified));
        Debug.Assert(!_sidecarFile.State.HasFlag(SidecarFile.StatesType.Repaired));

        // Attempt repair, if repair fails the original file will not be modified
        bool repaired = RepairAndReVerify();

        // Cancel requested
        if (Program.IsCancelled())
        {
            return false;
        }

        // Repair failed
        if (!repaired)
        {
            // Repair failed and verify failed state
            _sidecarFile.State |= SidecarFile.StatesType.VerifyFailed;
            _sidecarFile.State &= ~SidecarFile.StatesType.Verified;
            _sidecarFile.State |= SidecarFile.StatesType.RepairFailed;
            _sidecarFile.State &= ~SidecarFile.StatesType.Repaired;

            // Update state
            _ = Refresh(false);

            // Error
            return false;
        }

        // Verified and repaired state
        _sidecarFile.State |= SidecarFile.StatesType.Verified;
        _sidecarFile.State &= ~SidecarFile.StatesType.VerifyFailed;
        _sidecarFile.State |= SidecarFile.StatesType.Repaired;
        _sidecarFile.State &= ~SidecarFile.StatesType.RepairFailed;

        // Repaired
        modified = true;
        return Refresh(true);
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
        Log.Information("Calculating bitrate info : {FileName}", FileInfo.Name);
        if (!GetBitrateInfo(out BitrateInfo bitrateInfo))
        {
            // Error
            Log.Error("Failed to calculate bitrate info : {FileName}", FileInfo.Name);
            return false;
        }

        // Print bitrate
        bitrateInfo.WriteLine();

        // Combined bitrate exceeded threshold
        if (bitrateInfo.CombinedBitrate.Exceeded > 0)
        {
            Log.Warning(
                "Maximum bitrate exceeded : {CombinedBitrate} > {MaximumBitrate} : {FileName}",
                Bitrate.ToBitsPerSecond(bitrateInfo.CombinedBitrate.Maximum),
                Bitrate.ToBitsPerSecond(Program.Config.VerifyOptions.MaximumBitrate / 8),
                FileInfo.Name
            );

            // Caller must Refresh()
            _sidecarFile.State |= SidecarFile.StatesType.BitrateExceeded;

            // Warning only
        }

        // Audio bitrate exceeds video bitrate, may indicate an error with the video track
        if (bitrateInfo.AudioBitrate.Average > bitrateInfo.VideoBitrate.Average)
        {
            Log.Warning(
                "Audio bitrate exceeds Video bitrate : {AudioBitrate} > {VideoBitrate} : {FileName}",
                Bitrate.ToBitsPerSecond(bitrateInfo.AudioBitrate.Average),
                Bitrate.ToBitsPerSecond(bitrateInfo.VideoBitrate.Average),
                FileInfo.Name
            );

            // Warning only
        }

        return true;
    }

    private bool VerifyTrackFlags()
    {
        // Use MkvMergeInfo

        // Count the number of Default tracks
        List<bool> videoDefaults =
        [
            .. MkvMergeInfo.Video.Select(item => item.Flags.HasFlag(TrackInfo.FlagsType.Default)),
        ];
        List<bool> audioDefaults =
        [
            .. MkvMergeInfo.Audio.Select(item => item.Flags.HasFlag(TrackInfo.FlagsType.Default)),
        ];
        List<bool> subtitleDefaults =
        [
            .. MkvMergeInfo.Subtitle.Select(item =>
                item.Flags.HasFlag(TrackInfo.FlagsType.Default)
            ),
        ];
        if (videoDefaults.Count > 1 || audioDefaults.Count > 1 || subtitleDefaults.Count > 1)
        {
            Log.Warning(
                "Multiple Default flagged tracks : Video: {Video}, Audio: {Audio}, Subtitle: {Subtitle} : {FileName}",
                videoDefaults.Count,
                audioDefaults.Count,
                subtitleDefaults.Count,
                FileInfo.Name
            );

            // Warning only
        }

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

        // Use MediaInfoInfo and find all HDR tracks
        List<VideoInfo> hdrTracks = MediaInfoInfo.Video.FindAll(videoItem =>
            !string.IsNullOrEmpty(videoItem.FormatHdr)
        );
        if (hdrTracks.Count == 0)
        {
            // No HDR tracks
            return true;
        }

        // Find tracks that are not HDR10 (SMPTE ST 2086) or HDR10+ (SMPTE ST 2094) compatible
        List<VideoInfo> nonHdr10Tracks = hdrTracks.FindAll(videoInfo =>
            Hdr10FormatList.All(hdr10Format =>
                !videoInfo.FormatHdr.Contains(hdr10Format, StringComparison.OrdinalIgnoreCase)
            )
        );
        nonHdr10Tracks.ForEach(videoItem =>
        {
            Log.Warning(
                "Video is not HDR10 compatible : {Hdr} not in {Hdr10}: {FileName}",
                videoItem.FormatHdr,
                Hdr10FormatList,
                FileInfo.Name
            );

            // Warning only
        });

        return true;
    }

    private bool RepairAndReVerify()
    {
        // Sanity check
        Debug.Assert(!_sidecarFile.State.HasFlag(SidecarFile.StatesType.Verified));
        Debug.Assert(!_sidecarFile.State.HasFlag(SidecarFile.StatesType.Repaired));

        // Trying again may not succeed unless the tools changed
        if (_sidecarFile.State.HasFlag(SidecarFile.StatesType.RepairFailed))
        {
            Log.Warning("Repairing previously repaired media : {FileName}", FileInfo.Name);
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

        // Repair to temp file, only if verify is successful replace original file
        string tempName = Path.ChangeExtension(FileInfo.FullName, ".tmp12");
        Debug.Assert(FileInfo.FullName != tempName);

        // Convert using ffmpeg
        Log.Information(
            "Attempting media repair by ReEncoding using FfMpeg : {FileName}",
            FileInfo.Name
        );
        if (!Tools.FfMpeg.ConvertToMkv(FileInfo.FullName, tempName))
        {
            // Failed, delete temp file
            _ = FileEx.DeleteFile(tempName);

            // Cancel requested
            if (Program.IsCancelledError())
            {
                return false;
            }

            // Failed
            Log.Error("ReEncoding using FfMpeg failed : {FileName}", FileInfo.Name);

            // Try again using handbrake
            Log.Information(
                "Attempting media repair by ReEncoding using HandBrake : {FileName}",
                FileInfo.Name
            );
            if (!Tools.HandBrake.ConvertToMkv(FileInfo.FullName, tempName, true, false))
            {
                // Failed, delete temp file
                _ = FileEx.DeleteFile(tempName);

                // Cancel requested
                if (Program.IsCancelledError())
                {
                    return false;
                }

                // Failed
                Log.Error("ReEncode using HandBrake failed : {FileName}", FileInfo.Name);

                // Caller will update state
                return false;
            }
        }

        // ReMux using MkvMerge after FfMpeg or HandBrake encoding
        Log.Information("ReMuxing repaired media : {FileName}", tempName);
        if (!Convert.ReMux(tempName))
        {
            // Failed
            _ = FileEx.DeleteFile(tempName);
            return false;
        }

        // Re-encoding succeeded, re-verify the temp file
        if (!VerifyMediaStreams(new FileInfo(tempName)))
        {
            // Failed
            _ = FileEx.DeleteFile(tempName);
            return false;
        }

        // Verify succeeded, rename the temp file to the original file
        if (!FileEx.RenameFile(tempName, FileInfo.FullName))
        {
            return false;
        }

        // Repair succeeded
        Log.Information("Repair succeeded : {FileName}", FileInfo.Name);

        // Caller will update state
        return true;
    }

    public bool SetLastWriteTimeUtc(DateTime lastWriteTimeUtc)
    {
        // Conditional
        if (!Program.Config.ProcessOptions.RestoreFileTimestamp)
        {
            return true;
        }

        // Set modified timestamp
        File.SetLastWriteTimeUtc(FileInfo.FullName, lastWriteTimeUtc);

        // Refresh sidecar info
        return Refresh(true);
    }

    private bool Refresh(string fileName)
    {
        // Media filename changed
        // Compare case sensitive for Linux support
        Debug.Assert(fileName != null);
        if (!FileInfo.FullName.Equals(fileName, StringComparison.Ordinal))
        {
            // Refresh sidecar file info but preserve existing state, mark as renamed
            FileInfo = new FileInfo(fileName);
            SidecarFile.StatesType state = _sidecarFile.State | SidecarFile.StatesType.FileReNamed;
            _sidecarFile = new SidecarFile(FileInfo) { State = state };
        }

        // Refresh sidecar info
        return Refresh(true);
    }

    private bool Refresh(bool modified)
    {
        // Call Refresh() at each processing function exit
        // Set modified to true if the media file has been modified
        // Set modified to false if only the state has been modified

        // Load info from sidecar
        if (Program.Config.ProcessOptions.UseSidecarFiles)
        {
            // Open will read, create, update
            if (!_sidecarFile.Open(modified))
            {
                // Failed to read or create sidecar
                return false;
            }

            // Assign results
            FfProbeInfo = _sidecarFile.FfProbeInfo;
            MkvMergeInfo = _sidecarFile.MkvMergeInfo;
            MediaInfoInfo = _sidecarFile.MediaInfoInfo;

            return true;
        }

        // Get info directly from tools
        if (
            !MediaInfo.GetMediaInfo(
                FileInfo,
                out MediaInfo ffProbeInfo,
                out MediaInfo mkvMergeInfo,
                out MediaInfo mediaInfoInfo
            )
        )
        {
            return false;
        }

        // Assign results
        MediaInfoInfo = mediaInfoInfo;
        MkvMergeInfo = mkvMergeInfo;
        FfProbeInfo = ffProbeInfo;

        // Print info
        MediaInfoInfo.WriteLine();
        MkvMergeInfo.WriteLine();
        FfProbeInfo.WriteLine();

        return true;
    }

    public bool VerifyMediaInfo()
    {
        // TODO: Mixing anything other than MvMerge to MkvMerge requires the track numbers to be the same
        // Id's are unique to the tool, numbers come from the Matroska header
        // FfProbe does not report numbers, only id's

        // Make sure the track counts match
        if (
            FfProbeInfo.Audio.Count != MkvMergeInfo.Audio.Count
            || MkvMergeInfo.Audio.Count != MediaInfoInfo.Audio.Count
            || FfProbeInfo.Video.Count != MkvMergeInfo.Video.Count
            || MkvMergeInfo.Video.Count != MediaInfoInfo.Video.Count
            || FfProbeInfo.Subtitle.Count != MkvMergeInfo.Subtitle.Count
            || MkvMergeInfo.Subtitle.Count != MediaInfoInfo.Subtitle.Count
        )
        {
            // Something is very wrong; bad logic, bad media, bad tools?
            Log.Error("Tool track count discrepancy : {File}", FileInfo.Name);
            MediaInfoInfo.WriteLine();
            MkvMergeInfo.WriteLine();
            FfProbeInfo.WriteLine();

            // Break in debug builds
            Debug.Assert(false);
            return false;
        }

        return true;
    }

    public bool GetMediaInfo()
    {
        // Only MKV files
        Debug.Assert(SidecarFile.IsMkvFile(FileInfo));

        // Get media info
        if (!Refresh(false))
        {
            Log.Error("Failed to get media tool info : {FileName}", FileInfo.Name);
            return false;
        }

        // Verify that all codecs and tracks are supported
        if (MediaInfoInfo.Unsupported || FfProbeInfo.Unsupported || MkvMergeInfo.Unsupported)
        {
            Log.Error("Unsupported media info : {FileName}", FileInfo.Name);
            if (MediaInfoInfo.Unsupported)
            {
                MediaInfoInfo.WriteLine("Unsupported");
            }
            if (MkvMergeInfo.Unsupported)
            {
                MkvMergeInfo.WriteLine("Unsupported");
            }
            if (FfProbeInfo.Unsupported)
            {
                FfProbeInfo.WriteLine("Unsupported");
            }
            return false;
        }

        // Done
        return true;
    }

    public bool GetBitrateInfo(out BitrateInfo bitrateInfo)
    {
        // Get packet info
        bitrateInfo = null;
        if (
            !Tools.FfProbe.GetBitratePacketInfo(
                FileInfo.FullName,
                out List<FfMpegToolJsonSchema.Packet> packetList
            )
        )
        {
            return false;
        }

        // Use the default track, else the first track
        VideoInfo videoInfo = FfProbeInfo.Video.Find(item =>
            item.Flags.HasFlag(TrackInfo.FlagsType.Default)
        );
        videoInfo ??= FfProbeInfo.Video.FirstOrDefault();
        AudioInfo audioInfo = FfProbeInfo.Audio.Find(item =>
            item.Flags.HasFlag(TrackInfo.FlagsType.Default)
        );
        audioInfo ??= FfProbeInfo.Audio.FirstOrDefault();

        // Compute bitrate from packets
        bitrateInfo = new BitrateInfo();
        bitrateInfo.Calculate(
            packetList,
            videoInfo?.Id ?? -1,
            audioInfo?.Id ?? -1,
            Program.Config.VerifyOptions.MaximumBitrate / 8
        );

        return true;
    }

    private bool GetIdetInfo(out FfMpegIdetInfo idetInfo)
    {
        // Count the frame types using the idet filter
        Log.Information("Counting interlaced frames : {FileName}", FileInfo.Name);
        if (!FfMpegIdetInfo.GetIdetInfo(FileInfo, out idetInfo, out string error))
        {
            // Cancel requested
            if (Program.IsCancelledError())
            {
                return false;
            }

            // Failed
            Log.Error("Failed to count interlaced frames : {FileName}", FileInfo.Name);
            Log.Error("{Error}", error);
            return false;
        }

        // Log result
        Log.Information(
            "FfMpeg Idet : Interlaced: {IdetInterlaced} ({Percentage:P} > {Threshold:P}), Interlaced: {Interlaced}, Progressive: {Progressive}, Undetermined: {Undetermined}, Total: {Total} : {FileName}",
            idetInfo.IsInterlaced(),
            idetInfo.InterlacedPercentage,
            FfMpegIdetInfo.InterlacedThreshold,
            idetInfo.Interlaced,
            idetInfo.Progressive,
            idetInfo.Undetermined,
            idetInfo.Total,
            FileInfo.Name
        );

        return true;
    }

    public SelectMediaInfo FindUnknownLanguageTracks()
    {
        // IETF languages will only be set for MkvMerge
        // Select all tracks with undefined languages
        // Selected is Unknown
        // NotSelected is Known
        SelectMediaInfo selectMediaInfo = new(
            MkvMergeInfo,
            item => Language.IsUndefined(item.LanguageIetf)
        );
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
        FfProbeInfo.Video.ForEach(item =>
            selectMediaInfo.Add(
                item,
                Program.Config.ProcessOptions.ReEncodeVideo.Any(item.CompareVideo)
            )
        );
        FfProbeInfo.Audio.ForEach(item =>
            selectMediaInfo.Add(
                item,
                Program.Config.ProcessOptions.ReEncodeAudioFormats.Contains(item.Format)
            )
        );

        // Keep all subtitles
        selectMediaInfo.Add(FfProbeInfo.Subtitle, false);

        // If we are encoding audio, the video track may need to be reencoded at the same time
        // [matroska @ 00000195b3585c80] Timestamps are unset in a packet for stream 0.
        // [matroska @ 00000195b3585c80] Can't write packet with unknown timestamp
        // av_interleaved_write_frame(): Invalid argument
        if (selectMediaInfo.Selected.Audio.Count > 0 && selectMediaInfo.Selected.Video.Count == 0)
        {
            // If the video is not H264, H265 or AV1 (by experimentation), then tag the video to also be reencoded
            List<VideoInfo> reEncodeVideo = selectMediaInfo.NotSelected.Video.FindAll(item =>
                !ReEncodeVideoOnAudioReEncodeList.Contains(
                    item.Format,
                    StringComparer.OrdinalIgnoreCase
                )
            );
            if (reEncodeVideo.Count > 0)
            {
                selectMediaInfo.Move(reEncodeVideo, true);
                Log.Warning(
                    "Audio reencoding requires video reencoding : Audio: {FormatA}, Video: {FormatV}",
                    selectMediaInfo.Selected.Audio.Select(item => $"{item.Format}:{item.Codec}"),
                    reEncodeVideo.Select(item => $"{item.Format}:{item.Codec}:{item.Profile}")
                );
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
        List<TrackInfo> trackList = MkvMergeInfo.GetTrackList();

        // Get a list of all the IETF track languages
        List<string> languageList = Language.GetLanguageList(trackList);
        foreach (string language in languageList)
        {
            // Get all tracks matching this language
            List<TrackInfo> trackLanguageList = trackList.FindAll(item =>
                language.Equals(item.LanguageIetf, StringComparison.OrdinalIgnoreCase)
            );

            // If multiple audio tracks exist for this language, keep the preferred audio codec track
            List<TrackInfo> audioTrackList = trackLanguageList.FindAll(item =>
                item.GetType() == typeof(AudioInfo)
            );
            if (audioTrackList.Count > 1)
            {
                AudioInfo audioInfo = FindPreferredAudio(audioTrackList);
                selectMediaInfo.Move(audioInfo, true);
            }

            // Keep all tracks with flags
            List<TrackInfo> trackFlagList = trackLanguageList.FindAll(item =>
                item.Flags != TrackInfo.FlagsType.None
            );
            selectMediaInfo.Move(trackFlagList, true);

            // Keep one non-flag track
            // E.g. for subtitles it could be forced, hearing impaired, and one normal
            List<TrackInfo> videoNotFlagList = trackLanguageList.FindAll(item =>
                item.Flags == TrackInfo.FlagsType.None && item.GetType() == typeof(VideoInfo)
            );
            if (videoNotFlagList.Count > 0)
            {
                selectMediaInfo.Move(videoNotFlagList.First(), true);
            }
            List<TrackInfo> audioNotFlagList = trackLanguageList.FindAll(item =>
                item.Flags == TrackInfo.FlagsType.None && item.GetType() == typeof(AudioInfo)
            );
            if (audioNotFlagList.Count > 0)
            {
                selectMediaInfo.Move(audioNotFlagList.First(), true);
            }
            List<TrackInfo> subtitleNotFlagList = trackLanguageList.FindAll(item =>
                item.Flags == TrackInfo.FlagsType.None && item.GetType() == typeof(SubtitleInfo)
            );
            if (subtitleNotFlagList.Count > 0)
            {
                selectMediaInfo.Move(subtitleNotFlagList.First(), true);
            }
        }

        // We should have at least one of each kind of track, if any exists
        Debug.Assert(selectMediaInfo.Selected.Video.Count > 0 || MkvMergeInfo.Video.Count == 0);
        Debug.Assert(selectMediaInfo.Selected.Audio.Count > 0 || MkvMergeInfo.Audio.Count == 0);
        Debug.Assert(
            selectMediaInfo.Selected.Subtitle.Count > 0 || MkvMergeInfo.Subtitle.Count == 0
        );

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
        SelectMediaInfo selectMediaInfo = new(
            MkvMergeInfo,
            item =>
                Language.Singleton.IsMatch(
                    item.LanguageIetf,
                    Program.Config.ProcessOptions.KeepLanguages
                )
                || (
                    Program.Config.ProcessOptions.KeepOriginalLanguage
                    && item.Flags.HasFlag(TrackInfo.FlagsType.Original)
                )
        );

        // Keep at least one video track if any
        if (selectMediaInfo.Selected.Video.Count == 0 && MkvMergeInfo.Video.Count > 0)
        {
            // Use the first track
            VideoInfo videoInfo = MkvMergeInfo.Video.First();
            selectMediaInfo.Move(videoInfo, true);
            Log.Warning(
                "No video track matching requested language : {Available} not in {Languages}, selecting {Selected}",
                Language.GetLanguageList(MkvMergeInfo.Video),
                Program.Config.ProcessOptions.KeepLanguages,
                videoInfo.LanguageIetf
            );
        }

        // Keep at least one audio track if any
        if (selectMediaInfo.Selected.Audio.Count == 0 && MkvMergeInfo.Audio.Count > 0)
        {
            // Use the preferred audio codec track from the unselected tracks
            AudioInfo audioInfo = FindPreferredAudio(selectMediaInfo.NotSelected.Audio);
            selectMediaInfo.Move(audioInfo, true);
            Log.Warning(
                "No audio track matching requested language : {Available} not in {Languages}, selecting {Selected}",
                Language.GetLanguageList(MkvMergeInfo.Audio),
                Program.Config.ProcessOptions.KeepLanguages,
                audioInfo.LanguageIetf
            );
        }

        // No language matching subtitle tracks
        if (selectMediaInfo.Selected.Subtitle.Count == 0 && MkvMergeInfo.Subtitle.Count > 0)
        {
            Log.Warning(
                "No subtitle track matching requested language : {Available} not in {Languages}",
                Language.GetLanguageList(MkvMergeInfo.Subtitle),
                Program.Config.ProcessOptions.KeepLanguages
            );
        }

        // Selected is Keep
        // NotSelected is Remove
        selectMediaInfo.SetState(TrackInfo.StateType.Keep, TrackInfo.StateType.Remove);
        return selectMediaInfo;
    }

    private static AudioInfo FindPreferredAudio(IEnumerable<TrackInfo> trackInfoList)
    {
        // No preferred tracks, or only 1 track, use first track
        List<AudioInfo> audioInfoList = [.. trackInfoList.OfType<AudioInfo>()];
        Debug.Assert(audioInfoList.Count > 0);
        if (
            Program.Config.ProcessOptions.PreferredAudioFormats.Count == 0
            || audioInfoList.Count == 1
        )
        {
            return audioInfoList.First();
        }

        // Iterate through the preferred codecs in order
        foreach (string format in Program.Config.ProcessOptions.PreferredAudioFormats)
        {
            // Return on first match
            AudioInfo audioInfo = audioInfoList.Find(item =>
                item.Format.Equals(format, StringComparison.OrdinalIgnoreCase)
            );
            if (audioInfo != null)
            {
                Log.Information(
                    "Preferred audio format selected : {Preferred} in {Formats}",
                    audioInfo.Format,
                    audioInfoList.Select(item => item.Format)
                );
                return audioInfo;
            }
        }

        // Return first item
        Log.Information(
            "No audio format matching preferred formats : {Preferred} not in {Formats}, Selecting {Selected}",
            Program.Config.ProcessOptions.PreferredAudioFormats,
            audioInfoList.Select(item => item.Format),
            audioInfoList.First().Format
        );
        return audioInfoList.First();
    }

    public static bool IsTempFile(FileInfo fileInfo) =>
        // All temp files are to be named tmp[x] where x is an incrementing number
        // All uses of temp files must be uniquely named allowing nested use without overlap in temp file names
        fileInfo.Extension.StartsWith(".tmp", StringComparison.OrdinalIgnoreCase);

    public MediaInfo FfProbeInfo { get; private set; }
    public MediaInfo MkvMergeInfo { get; private set; }
    public MediaInfo MediaInfoInfo { get; private set; }
    public SidecarFile.StatesType State => _sidecarFile.State;
    public FileInfo FileInfo { get; private set; }

    private SidecarFile _sidecarFile;

    // HDR10 (SMPTE ST 2086) or HDR10+ (SMPTE ST 2094) (Using MediaInfo tags)
    public static readonly string[] Hdr10FormatList =
    [
        MediaInfoTool.HDR10Format,
        MediaInfoTool.HDR10PlusFormat,
    ];

    // ReEncode audio unless video is H264, H265 or AV1 (using MediaInfo tags)
    public static readonly string[] ReEncodeVideoOnAudioReEncodeList =
    [
        MediaInfoTool.H264Format,
        MediaInfoTool.H265Format,
        MediaInfoTool.AV1Format,
    ];
}
