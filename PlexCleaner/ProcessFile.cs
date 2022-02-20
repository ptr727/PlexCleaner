using InsaneGenius.Utilities;
using PlexCleaner.FfMpegToolJsonSchema;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace PlexCleaner;

public class ProcessFile
{
    public ProcessFile(FileInfo mediaFile)
    {
        FileInfo = mediaFile;
        SidecarFile = new SidecarFile(mediaFile);
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
        string mediafile = Path.ChangeExtension(FileInfo.FullName, ".mkv");

        // Does the media file exist
        if (File.Exists(mediafile))
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
        SidecarFile.State |= SidecarFile.States.Deleted;
        return false;
    }

    public bool MakeExtensionLowercase(ref bool modified)
    {
        // Is the extension lowercase
        string lowerExtension = FileInfo.Extension.ToLower();
        if (FileInfo.Extension.Equals(lowerExtension))
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
        return FileInfo.Exists && FileEx.IsFileReadWriteable(FileInfo);
    }

    public bool IsSidecarWriteable()
    {
        // If the sidecar file exists it must be writeable
        return !SidecarFile.Exists() || SidecarFile.IsWriteable();
    }

    public bool RemuxByExtensions(HashSet<string> remuxExtensions, ref bool modified)
    {
        if (remuxExtensions == null)
        {
            throw new ArgumentNullException(nameof(remuxExtensions));
        }

        // Optional
        if (!Program.Config.ProcessOptions.ReMux)
        {
            return true;
        }

        // Does the extension match
        if (!remuxExtensions.Contains(FileInfo.Extension))
        {
            // Nothing to do
            return true;
        }

        // ReMux the file
        Log.Logger.Information("ReMux file matched by extension : {FileName}", FileInfo.Name);

        // Remux the file, use the new filename
        // Convert will test for Options.TestNoModify
        if (!Convert.ReMuxToMkv(FileInfo.FullName, out string outputname))
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
        SidecarFile.State |= SidecarFile.States.ReMuxed;

        // Extension may have changed
        // Refresh
        modified = true;
        return Refresh(outputname);
    }

    public bool RemuxNonMkvContainer(ref bool modified)
    {
        // Optional
        if (!Program.Config.ProcessOptions.ReMux)
        {
            return true;
        }

        // Make sure that MKV named files are Matroska containers
        if (MkvMergeInfo.Container.Equals("Matroska", StringComparison.OrdinalIgnoreCase))
        {
            // Nothing to do
            return true;
        }

        // ReMux the file
        Log.Logger.Information("ReMux {Container} container : {FileName}", MkvMergeInfo.Container, FileInfo.Name);

        // Remux the file, use the new filename
        // Convert will test for Options.TestNoModify
        if (!Convert.ReMuxToMkv(FileInfo.FullName, out string outputname))
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
        SidecarFile.State |= SidecarFile.States.ReMuxed;

        // Extension may have changed
        // Refresh
        modified = true;
        return Refresh(outputname);
    }

    public bool ReMuxMediaInfoErrors(ref bool modified)
    {
        // Do we have any errors
        if (!FfProbeInfo.HasErrors &&
            !MkvMergeInfo.HasErrors &&
            !MediaInfoInfo.HasErrors)
        {
            return true;
        }

        // Look for the ReMuxed flag and don't try to remux again
        if (SidecarFile.State.HasFlag(SidecarFile.States.ReMuxed))
        {
            Log.Logger.Warning("Skipping ReMux due to previous ReMux failed to clear errors : {FileName}", FileInfo.Name);
            // Return success
            return true;
        }

        // Try to ReMux the file
        Log.Logger.Information("ReMux to repair metadata errors : {FileName}", FileInfo.Name);

        // Remux the file, use the new filename
        if (!Convert.ReMuxToMkv(FileInfo.FullName, out string outputname))
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
        SidecarFile.State |= SidecarFile.States.ReMuxed;

        // Refresh
        modified = true;
        bool result = Refresh(outputname);

        // Do we still have errors after remuxing
        if (FfProbeInfo.HasErrors ||
            MkvMergeInfo.HasErrors ||
            MediaInfoInfo.HasErrors)
        {
            // Ignore error
            Log.Logger.Warning("ReMux failed to clear metadata errors : {FileName}", FileInfo.Name);
        }
        else
        {
            Log.Logger.Information("ReMux cleared metadata errors : {FileName}", FileInfo.Name);
        }

        // Extension may have changed
        return result;
    }

    public bool RemoveTags(ref bool modified)
    {
        // Optional
        if (!Program.Config.ProcessOptions.RemoveTags)
        {
            return true;
        }

        // Does the file have tags
        if (!MkvMergeInfo.HasTags)
        {
            // No tags
            return true;
        }

        // Remove tags
        Log.Logger.Information("Clearing all tags from media file : {FileName}", FileInfo.Name);

        // Delete the tags
        if (!Program.Options.TestNoModify &&
            !Tools.MkvPropEdit.ClearMkvTags(FileInfo.FullName, MkvMergeInfo))
        {
            // Error
            return false;
        }

        // Set modified state
        SidecarFile.State |= SidecarFile.States.ClearedTags;

        // Refresh
        modified = true;
        return Refresh(true);
    }

    public bool SetUnknownLanguage(ref bool modified)
    {
        // Optional
        if (!Program.Config.ProcessOptions.SetUnknownLanguage)
        {
            return true;
        }

        // Find unknown languages
        if (!MkvMergeInfo.FindUnknownLanguage(out MediaInfo known, out MediaInfo unknown))
        {
            // Nothing to do
            return true;
        }

        Log.Logger.Information("Setting unknown language tracks to {DefaultLanguage} : {FileName}",
            Program.Config.ProcessOptions.DefaultLanguage,
            FileInfo.Name);
        known.WriteLine("Known");
        unknown.WriteLine("Unknown");

        // Set the track language to the default language
        if (!Program.Options.TestNoModify &&
            !Tools.MkvPropEdit.SetMkvTrackLanguage(FileInfo.FullName, unknown, Program.Config.ProcessOptions.DefaultLanguage))
        {
            // Error
            return false;
        }

        // Set modified state
        SidecarFile.State |= SidecarFile.States.SetLanguage;

        // Refresh
        modified = true;
        return Refresh(true);
    }

    public bool ReMux(HashSet<string> keepLanguages, List<string> preferredAudioFormats, ref bool modified)
    {
        // Start out with keeping all the tracks
        MediaInfo keepTracks = MkvMergeInfo;
        MediaInfo removeTracks = new(MediaTool.ToolType.MkvMerge);

        // Get all unwanted language tracks
        // Use MKVMerge logic
        bool remux = false;
        if (Program.Config.ProcessOptions.RemoveUnwantedLanguageTracks &&
            MkvMergeInfo.FindUnwantedLanguage(keepLanguages, preferredAudioFormats, out MediaInfo keep, out MediaInfo remove))
        {
            Log.Logger.Information("Removing unwanted language tracks : {FileName}", FileInfo.Name);
            keep.WriteLine("Keep");
            remove.WriteLine("Remove");

            // Remove the unwanted language tracks
            keepTracks.RemoveTracks(remove);
            removeTracks.AddTracks(remove);
            remux = true;
        }

        // Get all duplicate tracks  
        // Use MKVMerge logic
        if (Program.Config.ProcessOptions.RemoveDuplicateTracks &&
            MkvMergeInfo.FindDuplicateTracks(preferredAudioFormats, out keep, out remove))
        {
            Log.Logger.Information("Removing duplicate tracks : {FileName}", FileInfo.Name);
            keep.WriteLine("Keep");
            remove.WriteLine("Remove");

            // Remove the duplicate tracks
            keepTracks.RemoveTracks(remove);
            removeTracks.AddTracks(remove);
            remux = true;
        }

        // Any remuxing to do
        if (!remux)
        {
            // Done
            return true;
        }

        Log.Logger.Information("Re-muxing union of tracks : {FileName}", FileInfo.Name);
        keepTracks.WriteLine("Keep");
        removeTracks.WriteLine("Remove");

        // ReMux and only keep the specified tracks
        // Convert will test for Options.TestNoModify
        if (!Convert.ReMuxToMkv(FileInfo.FullName, keepTracks, out string outputname))
        {
            // Error
            return false;
        }

        // Update state
        SidecarFile.State |= SidecarFile.States.ReMuxed;

        // Extension may have changed
        // Refresh
        modified = true;
        return Refresh(outputname);
    }

    public bool DeInterlace(ref bool modified)
    {
        // Optional
        if (!Program.Config.ProcessOptions.DeInterlace)
        {
            return true;
        }

        // Test if content is interlaced
        if (!FindNeedDeInterlace(out MediaInfo keep, out MediaInfo deinterlace))
        {
            return true;
        }

        Log.Logger.Information("De-interlacing interlaced video : {FileName}", FileInfo.Name);
        keep.WriteLine("Keep");
        deinterlace.WriteLine("DeInterlace");

        // Convert using HandBrakeCLI, it produces the best de-interlacing results using the decomb filter
        // Convert will test for Options.TestNoModify
        if (!Convert.DeInterlaceToMkv(FileInfo.FullName, out string outputname))
        {
            // Error
            return false;
        }

        // Update state
        SidecarFile.State |= SidecarFile.States.DeInterlaced;

        // Extension may have changed
        // Refresh
        modified = true;
        return Refresh(outputname);
    }

    public bool RemoveSubtitles(ref bool modified)
    {
        // Start out with keeping all the tracks
        MediaInfo keepTracks = MkvMergeInfo;
        MediaInfo removeTracks = new(MediaTool.ToolType.MkvMerge);

        // Remove all the subtitle tracks
        removeTracks.Subtitle.AddRange(keepTracks.Subtitle);
        keepTracks.Subtitle.Clear();

        // Any remuxing to do
        if (removeTracks.Subtitle.Count == 0)
        {
            // Done
            return true;
        }

        Log.Logger.Information("Re-muxing tracks : {FileName}", FileInfo.Name);
        keepTracks.WriteLine("Keep");
        removeTracks.WriteLine("Remove");

        // ReMux and only keep the specified tracks
        // Convert will test for Options.TestNoModify
        if (!Convert.ReMuxToMkv(FileInfo.FullName, keepTracks, out string outputname))
        {
            // Error
            return false;
        }

        // Update state
        SidecarFile.State |= SidecarFile.States.ReMuxed;

        // Extension may have changed
        // Refresh
        modified = true;
        return Refresh(outputname);
    }

    public bool ReEncode(List<VideoInfo> reencodeVideoInfos, HashSet<string> reencodeAudioFormats, ref bool modified)
    {
        // Optional
        if (!Program.Config.ProcessOptions.ReEncode)
        {
            return true;
        }

        // Find all tracks that need re-encoding
        // Use FfProbeInfo because the video matching logic uses FFprobe attributes
        if (!FfProbeInfo.FindNeedReEncode(reencodeVideoInfos, reencodeAudioFormats, out MediaInfo keep, out MediaInfo reencode))
        {
            // Nothing to do
            return true;
        }

        Log.Logger.Information("Re-encoding required tracks : {FileName}", FileInfo.Name);
        keep.WriteLine("Passthrough");
        reencode.WriteLine("ReEncode");

        // Reencode selected tracks
        // Convert will test for Options.TestNoModify
        if (!Convert.ConvertToMkv(FileInfo.FullName, keep, reencode, out string outputname))
        {
            // Error
            return false;
        }

        // Update state
        SidecarFile.State |= SidecarFile.States.ReEncoded;

        // Extension may have changed
        // Refresh
        modified = true;
        return Refresh(outputname);
    }

    public bool Verify(bool conditional, ref bool modified)
    {
        // Conditional or always
        if (conditional)
        {
            // Verify if enabled
            if (!Program.Config.ProcessOptions.Verify)
            {
                return true;
            }

            // If we are using a sidecar file we can use the last result
            if (Program.Config.ProcessOptions.UseSidecarFiles &&
                SidecarFile.State.HasFlag(SidecarFile.States.Verified))
            {
                return true;
            }

            // Skip files that are older than the minimum age
            TimeSpan fileAge = DateTime.UtcNow - FileInfo.LastWriteTimeUtc;
            TimeSpan testAge = TimeSpan.FromDays(Program.Config.VerifyOptions.MinimumFileAge);
            if (Program.Config.VerifyOptions.MinimumFileAge > 0 &&
                fileAge > testAge)
            {
                Log.Logger.Warning("Skipping file due to age : {FileAge} > {TestAge} : {FileName}", fileAge, testAge, FileInfo.Name);
                return true;
            }
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
                Log.Logger.Warning("File missing audio or video track : {FileName}", FileInfo.Name);
                MediaInfoInfo.WriteLine("Missing");

                // Warning only, continue
            }

            // Test playback duration
            if (MkvMergeInfo.Duration < TimeSpan.FromMinutes(Program.Config.VerifyOptions.MinimumDuration))
            {
                // Playback duration is too short
                Log.Logger.Warning("File play duration is too short : {Duration} < {MinimumDuration} : {FileName}",
                    MkvMergeInfo.Duration,
                    TimeSpan.FromMinutes(Program.Config.VerifyOptions.MinimumDuration),
                    FileInfo.Name);
                MkvMergeInfo.WriteLine("Short");

                // Warning only, continue
            }

            // Verify media streams
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

            // Get and compare interlaced flags
            if (!VerifyInterlacedFlags())
            {
                // Cancel requested
                if (Program.IsCancelledError())
                {
                    return false;
                }

                // Done
                break;
            }

            // Verify bitrate
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

            // Verify HDR
            if (!VerifyHdr())
            {
                // Cancel requested
                if (Program.IsCancelledError())
                {
                    return false;
                }

                // Done
                break;
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
            // If testing we are done
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

                // Done
                return false;
            }

            // Add the failed file to the ignore list
            if (Program.Config.VerifyOptions.RegisterInvalidFiles)
            {
                Program.Config.ProcessOptions.FileIgnoreList.Add(FileInfo.FullName);
            }

            // Update state
            SidecarFile.State |= SidecarFile.States.VerifyFailed;
            SidecarFile.State &= ~SidecarFile.States.Verified;
            Refresh(false);

            // Failed
            return false;
        }

        // All ok
        SidecarFile.State |= SidecarFile.States.Verified;
        SidecarFile.State &= ~SidecarFile.States.VerifyFailed;
        return Refresh(false);
    }

    private bool VerifyBitrate()
    {
        // Skip if no bitrate limit
        if (Program.Config.VerifyOptions.MaximumBitrate == 0)
        {
            return true;
        }

        // TODO : Verify that bitrate is acceptable for the resolution of the content, i.e. no YIFY, no fake 4K
        // https://4kmedia.org/real-or-fake-4k/
        // https://en.wikipedia.org/wiki/YIFY

        // Calculate bitrate
        Log.Logger.Information("Calculating bitrate info : {FileName}", FileInfo.Name);
        if (!GetBitrateInfo(out BitrateInfo bitrateInfo))
        {
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
            SidecarFile.State |= SidecarFile.States.BitrateExceeded;
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

    private bool VerifyHdr()
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

        // Use first video track, if any
        if (MediaInfoInfo.Video.Count == 0)
        {
            return true;
        }

        VideoInfo videoInfo = MediaInfoInfo.Video.First();

        // Test for HDR
        if (string.IsNullOrEmpty(videoInfo.FormatHdr))
        {
            return true;
        }

        // Look for HDR10 format
        bool hdr10 = Hdr10Format.Any(format => videoInfo.FormatHdr.Contains(format, StringComparison.OrdinalIgnoreCase));
        if (!hdr10)
        {
            Log.Logger.Warning("Video lacks HDR10 compatibility : {Hdr} : {FileName}", videoInfo.FormatHdr, FileInfo.Name);
        }

        // Ignore the error
        return true;
    }

    private bool VerifyInterlacedFlags()
    {
        // Do the tool interlaced flags match
        bool mediainfoInterlaced = MediaInfoInfo.FindNeedDeInterlace(out MediaInfo _, out MediaInfo _);
        bool ffprobeInterlaced = FfProbeInfo.FindNeedDeInterlace(out MediaInfo _, out MediaInfo _);
        // MkvMergeInfo does not currently implement interlace detection

        // Running idet is time consuming, skip if MediaInfo and FFprobe agree
        if (mediainfoInterlaced == ffprobeInterlaced)
        {
            return true;
        }

        // Disagreement in interlaced flags
        Log.Logger.Warning("Interlaced flags do not match : {MediainfoInterlaced} != {FfprobeInterlaced} : {FileName}",
            mediainfoInterlaced,
            ffprobeInterlaced,
            FileInfo.Name);
        Log.Logger.Information("Calculating interlaced frame info : {FileName}", FileInfo.Name);
        if (!Tools.FfMpeg.GetIdetInfo(FileInfo.FullName, out FfMpegIdetInfo idetinfo))
        {
            Log.Logger.Error("Failed to calculate interlaced frame info : {FileName}", FileInfo.Name);
            return false;
        }

        // Idet
        bool idetinterlaced = idetinfo.IsInterlaced();
        Log.Logger.Information("FfMpeg interlace detection : Idet: {IdetInterlaced}, Interlaced: {Interlaced}, Progressive: {Progressive}, Undetermined: {Undetermined}, Total: {Total} : {FileName}",
            idetinterlaced,
            idetinfo.Interlaced,
            idetinfo.Progressive,
            idetinfo.Undetermined,
            idetinfo.Total,
            FileInfo.Name);

        // Ignore the error
        return true;
    }

    private bool VerifyRepair(ref bool modified)
    {
        // Don't repair in test mode
        if (Program.Options.TestNoModify)
        {
            return false;
        }

        // Previous repair attempt failed
        if (SidecarFile.State.HasFlag(SidecarFile.States.RepairFailed))
        {
            // Just warn, maybe tools changed, try again
            Log.Logger.Warning("Previous attempts to repair failed : {State} : {FileName}", SidecarFile.State, FileInfo.Name);
        }

        // TODO : Analyze the error output and conditionally repair only the audio or video track
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

        // TODO: Handbrake sometimes fails with what looks like a remux error
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
        string tempname = Path.ChangeExtension(FileInfo.FullName, ".tmp");

        // Convert using ffmpeg
        Log.Logger.Information("Attempting media repair by re-encoding using FfMpeg : {FileName}", FileInfo.Name);
        if (!Tools.FfMpeg.ConvertToMkv(FileInfo.FullName, tempname))
        {
            // Failed, delete temp file
            FileEx.DeleteFile(tempname);

            // Cancel requested
            if (Program.IsCancelledError())
            {
                return false;
            }

            // Try again using handbrake
            Log.Logger.Information("Attempting media repair by re-encoding using HandBrake : {FileName}", FileInfo.Name);
            if (!Tools.HandBrake.ConvertToMkv(FileInfo.FullName, tempname))
            {
                // Failed, delete temp file
                FileEx.DeleteFile(tempname);

                // Cancel requested
                if (Program.IsCancelledError())
                {
                    return false;
                }

                // Failed again
                Log.Logger.Error("Repair by re-encoding failed : {FileName}", FileInfo.Name);

                // Update state
                // Caller will Refresh()
                SidecarFile.State |= SidecarFile.States.RepairFailed;

                return false;
            }
        }

        // Re-encoding succeeded, re-verify the temp file
        Log.Logger.Information("Re-verifying media streams : {FileName}", FileInfo.Name);
        if (!Tools.FfMpeg.VerifyMedia(tempname, out string error))
        {
            // Failed, delete temp file
            FileEx.DeleteFile(tempname);

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
            SidecarFile.State |= SidecarFile.States.RepairFailed;

            return false;
        }

        // Rename the temp file to the original file
        if (!FileEx.RenameFile(tempname, FileInfo.FullName))
        {
            return false;
        }

        // Repair succeeded
        Log.Logger.Information("Repair succeeded : {FileName}", FileInfo.Name);

        // Update state
        SidecarFile.State |= SidecarFile.States.Repaired;
        SidecarFile.State &= ~SidecarFile.States.RepairFailed;

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

    private bool Refresh(string filename)
    {
        // Media filename changed
        // Compare case sensitive for Linux support
        if (!FileInfo.FullName.Equals(filename, StringComparison.Ordinal))
        {
            // Refresh file info but preserve state
            FileInfo = new FileInfo(filename);
            SidecarFile.States state = SidecarFile.State | SidecarFile.States.ReNamed;
            SidecarFile = new SidecarFile(FileInfo);

            // Refresh will create a new sidecar file for the renamed file
            if (!Refresh(true))
            {
                return false;
            }

            // Reset the state and refresh again
            SidecarFile.State = state;
            return Refresh(false);
        }

        return Refresh(true);
    }

    private bool Refresh(bool modified)
    {
        // Call Refresh() at each processing function exit when the media file has been modified

        // If the file was modified wait for a little to let the IO complete
        // E.g. MkvPropEdit changes are not visible when immediately reading the file
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
        // Make sure the track counts match
        if (FfProbeInfo.Audio.Count != MkvMergeInfo.Audio.Count ||
            MkvMergeInfo.Audio.Count != MediaInfoInfo.Audio.Count ||
            FfProbeInfo.Video.Count != MkvMergeInfo.Video.Count ||
            MkvMergeInfo.Video.Count != MediaInfoInfo.Video.Count ||
            FfProbeInfo.Subtitle.Count != MkvMergeInfo.Subtitle.Count ||
            MkvMergeInfo.Subtitle.Count != MediaInfoInfo.Subtitle.Count)
        {
            // Something is wrong; bad logic, bad media, bad tools?
            Log.Logger.Error("Tool track count discrepency : {File}", FileInfo.Name);
            MediaInfoInfo.WriteLine("MediaInfo");
            MkvMergeInfo.WriteLine("MKVMerge");
            FfProbeInfo.WriteLine("FFProbe");

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

    public bool GetToolInfo()
    {
        // Read the tool info text
        if (!Tools.MediaInfo.GetMediaInfoXml(FileInfo.FullName, out string mediaInfoXml) ||
            !Tools.MkvMerge.GetMkvInfoJson(FileInfo.FullName, out string mkvMergeInfoJson) ||
            !Tools.FfProbe.GetFfProbeInfoJson(FileInfo.FullName, out string ffProbeInfoJson))
        {
            Log.Logger.Error("Failed to read tool info : {FileName}", FileInfo.Name);
            return false;
        }

        // Assign the text values
        MediaInfoText = mediaInfoXml;
        MkvMergeText = mkvMergeInfoJson;
        FfProbeText = ffProbeInfoJson;

        return true;
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

        // Compute bitrate from packets
        // Use the first video and audio track for calculation
        // TODO: Use default tracks not the first track
        bitrateInfo = new BitrateInfo();
        bitrateInfo.Calculate(packetList,
            FfProbeInfo.Video.Count > 0 ? FfProbeInfo.Video.First().Id : -1,
            FfProbeInfo.Audio.Count > 0 ? FfProbeInfo.Audio.First().Id : -1,
            Program.Config.VerifyOptions.MaximumBitrate / 8);

        return true;
    }

    public bool FindNeedDeInterlace(out MediaInfo keep, out MediaInfo deinterlace)
    {
        // Test the deinterlace flags from media info 
        if (FfProbeInfo.FindNeedDeInterlace(out keep, out deinterlace) ||
            MediaInfoInfo.FindNeedDeInterlace(out keep, out deinterlace) ||
            MkvMergeInfo.FindNeedDeInterlace(out keep, out deinterlace))
        {
            return true;
        }

        // Running idet is expensive, skip if already verified or deinterlaced
        if (State.HasFlag(SidecarFile.States.Verified) ||
            State.HasFlag(SidecarFile.States.DeInterlaced))
        {
            return false;
        }

        // Count the frame types using the idet filter
        Log.Logger.Information("Counting interlaced frames : {FileName}", FileInfo.Name);
        if (!FfMpegIdetInfo.GetIdetInfo(FileInfo, out FfMpegIdetInfo idetInfo))
        {
            // Error
            return false;
        }

        // Result
        bool result = idetInfo.IsInterlaced();
        Log.Logger.Information("Idet Interlaced: {Result}, Undetermined: {Undetermined}, Progressive: {Progressive}, Interlaced: {Interlaced}, Total: {Total}",
                               result,
                               idetInfo.Undetermined,
                               idetInfo.Progressive,
                               idetInfo.Interlaced,
                               idetInfo.Total);
        if (!result)
        {
            return false;
        }

        // Idet says yes metadata said no
        Log.Logger.Warning("Idet reported interlaced, metadata reported not interlaced : {FileName}", FileInfo.Name);

        // Use logic similar to MediaInfo::FindNeedDeInterlace()

        // Use FFprobeInfo type
        keep = new MediaInfo(MediaTool.ToolType.FfProbe);
        deinterlace = new MediaInfo(MediaTool.ToolType.FfProbe);

        // No filter for audio or subtitle
        keep.Subtitle.Clear();
        keep.Subtitle.AddRange(FfProbeInfo.Subtitle);
        keep.Audio.Clear();
        keep.Audio.AddRange(FfProbeInfo.Audio);

        // Add all video tracks
        deinterlace.Video.Clear();
        deinterlace.Video.AddRange(FfProbeInfo.Video);

        // Set the correct state on all the objects
        deinterlace.Video.ForEach(item => item.State = TrackInfo.StateType.DeInterlace);
        deinterlace.Video.ForEach(item => item.Interlaced = true);
        keep.GetTrackList().ForEach(item => item.State = TrackInfo.StateType.Keep);

        return true;
    }

    public bool Modified { get; set; }
    public MediaInfo FfProbeInfo { get; set; }
    public MediaInfo MkvMergeInfo { get; set; }
    public MediaInfo MediaInfoInfo { get; set; }
    public string MkvMergeText { get; set; }
    public string FfProbeText { get; set; }
    public string MediaInfoText { get; set; }
    public SidecarFile.States State => SidecarFile.State;
    public FileInfo FileInfo { get; private set; }

    private SidecarFile SidecarFile;

    private const int RefreshWaitTime = 5;
    private static readonly string[] Hdr10Format = { "SMPTE ST 2086", "SMPTE ST 2094" };
}