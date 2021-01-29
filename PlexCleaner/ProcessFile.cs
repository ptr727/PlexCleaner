using InsaneGenius.Utilities;
using PlexCleaner.FfMpegToolJsonSchema;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PlexCleaner
{
    public class ProcessFile
    {
        public ProcessFile(FileInfo mediaFile)
        {
            MediaFile = mediaFile;
        }

        public bool DeleteUnwantedExtensions(HashSet<string> keepExtensions, HashSet<string> remuxExtensions, ref bool modified)
        {
            if (keepExtensions == null)
                throw new ArgumentNullException(nameof(keepExtensions));
            if (remuxExtensions == null)
                throw new ArgumentNullException(nameof(remuxExtensions));

            // Init
            Result = true;

            // Optional
            if (!Program.Config.ProcessOptions.DeleteUnwantedExtensions)
                return true;

            // Is the file extension in our keep list
            if (keepExtensions.Contains(MediaFile.Extension) ||
                remuxExtensions.Contains(MediaFile.Extension))
                // Keep file, nothing more to do
                return true;

            // Delete the file
            Log.Logger.Information("Deleting file with undesired extension : {Name}", MediaFile.Name);

            // Delete the file
            if (!Program.Options.TestNoModify &&
                !FileEx.DeleteFile(MediaFile.FullName))
            {
                // Error
                Result = false;
                return false;
            }

            // File deleted, do not continue processing
            modified = true;
            Result = true;
            return false;
        }

        public bool DeleteMissingSidecarFiles(ref bool modified)
        {
            // Init
            Result = true;

            // Is this a sidecar file
            if (!SidecarFile.IsSidecarExtension(MediaFile.Extension))
                // Nothing to do
                return true;

            // Get the matching MKV file
            string mediafile = Path.ChangeExtension(MediaFile.FullName, ".mkv");

            // Does the media file exist
            if (File.Exists(mediafile))
                // File exists, nothing more to do
                return true;

            // Media file does not exists, delete this sidecar file
            Log.Logger.Information("Deleting sidecar file with no matching MKV file : {Name}", MediaFile.Name);

            // Delete the file
            if (!Program.Options.TestNoModify &&
                !FileEx.DeleteFile(MediaFile.FullName))
            {
                // Error
                Result = false;
                return false;
            }

            // File deleted, do not continue processing
            modified = true;
            Result = true;
            return false;
        }

        public bool RemuxByExtensions(HashSet<string> remuxExtensions, ref bool modified)
        {
            if (remuxExtensions == null)
                throw new ArgumentNullException(nameof(remuxExtensions));

            // Init
            Result = true;

            // Optional
            if (!Program.Config.ProcessOptions.ReMux)
                return true;

            // Does the extension match
            if (!remuxExtensions.Contains(MediaFile.Extension))
                // Nothing to do
                return true;

            // ReMux the file
            Log.Logger.Information("ReMux file matched by extension : {Name}", MediaFile.Name);

            // Remux the file, use the new filename
            // Convert will test for Options.TestNoModify
            if (!Convert.ReMuxToMkv(MediaFile.FullName, out string outputname))
            {
                // Error
                Result = false;
                return false;
            }

            // In test mode the file will not be remuxed to MKV so abort
            if (Program.Options.TestNoModify)
                return false;

            // Extension may have changed
            MediaFile = new FileInfo(outputname);

            // Refresh
            modified = true;
            return Refresh(true);
        }

        public bool RemuxNonMkvContainer(ref bool modified)
        {
            // Init
            Result = true;

            // Optional
            if (!Program.Config.ProcessOptions.ReMux)
                return true;

            // Make sure that MKV named files are Matroska containers
            if (MkvMergeInfo.Container.Equals("Matroska", StringComparison.OrdinalIgnoreCase))
                // Nothing to do
                return true;

            // ReMux the file
            Log.Logger.Information("ReMux {Container} container : {Name}", MkvMergeInfo.Container, MediaFile.Name);

            // Remux the file, use the new filename
            // Convert will test for Options.TestNoModify
            if (!Convert.ReMuxToMkv(MediaFile.FullName, out string outputname))
            {
                // Error
                Result = false;
                return false;
            }

            // In test mode the file will not be remuxed to MKV so abort
            if (Program.Options.TestNoModify)
                return false;

            // Extension may have changed
            MediaFile = new FileInfo(outputname);

            // Refresh
            modified = true;
            return Refresh(true);
        }

        public void MediaInfoErrors()
        {
            // Do we have any errors
            if (FfProbeInfo.HasErrors || MkvMergeInfo.HasErrors || MediaInfoInfo.HasErrors)
            {
                Log.Logger.Warning("Media file has metadata errors : {Name}", MediaFile.Name);
            }
        }

        public bool RemoveTags(ref bool modified)
        {
            // Init
            Result = true;

            // Optional
            if (!Program.Config.ProcessOptions.RemoveTags)
                return true;

            // Does the file have tags
            if (!MkvMergeInfo.HasTags)
                // No tags
                return true;

            // Remove tags
            Log.Logger.Information("Clearing all tags from media file : {Name}", MediaFile.Name);

            // Delete the tags
            if (!Program.Options.TestNoModify &&
                !Tools.MkvPropEdit.ClearMkvTags(MediaFile.FullName, MkvMergeInfo))
            {
                // Error
                Result = false;
                return false;
            }

            // Refresh
            modified = true;
            return Refresh(true);
        }

        public bool SetUnknownLanguage(ref bool modified)
        {
            // Init
            Result = true;

            // Optional
            if (!Program.Config.ProcessOptions.SetUnknownLanguage)
                return true;

            // Find unknown languages
            if (!MkvMergeInfo.FindUnknownLanguage(out MediaInfo known, out MediaInfo unknown))
                // Nothing to do
                return true;

            Log.Logger.Information("Setting unknown language tracks to {DefaultLanguage} : {Name}", Program.Config.ProcessOptions.DefaultLanguage, MediaFile.Name);
            known.WriteLine("Known");
            unknown.WriteLine("Unknown");

            // Set the track language to the default language
            if (!Program.Options.TestNoModify &&
                !Tools.MkvPropEdit.SetMkvTrackLanguage(MediaFile.FullName, unknown, Program.Config.ProcessOptions.DefaultLanguage))
            {
                // Error
                Result = false;
                return false;
            }

            // Refresh
            modified = true;
            return Refresh(true);
        }

        public bool ReMux(HashSet<string> keepLanguages, List<string> preferredAudioFormats, ref bool modified)
        {
            // Init
            Result = true;
            bool remux = false;

            // Start out with keeping all the tracks
            // TODO : Deep vs. shallow vs. value compare
            MediaInfo keepTracks = MkvMergeInfo;
            MediaInfo removeTracks = new MediaInfo(MediaTool.ToolType.MkvMerge);

            // Get all unwanted language tracks
            // Use MKVMerge logic
            if (Program.Config.ProcessOptions.RemoveUnwantedLanguageTracks &&
                MkvMergeInfo.FindUnwantedLanguage(keepLanguages, out MediaInfo keep, out MediaInfo remove))
            {
                Log.Logger.Information("Removing unwanted language tracks : {Name}", MediaFile.Name);
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
                Log.Logger.Information("Removing duplicate tracks : {Name}", MediaFile.Name);
                keep.WriteLine("Keep");
                remove.WriteLine("Remove");

                // Remove the duplicate tracks
                keepTracks.RemoveTracks(remove);
                removeTracks.AddTracks(remove);
                remux = true;
            }

            // Any remuxing to do
            if (!remux)
                // Done
                return true;

            Log.Logger.Information("Re-muxing union of tracks : {Name}", MediaFile.Name);
            keepTracks.WriteLine("Keep");
            removeTracks.WriteLine("Remove");

            // ReMux and only keep the specified tracks
            // Convert will test for Options.TestNoModify
            if (!Convert.ReMuxToMkv(MediaFile.FullName, keepTracks, out string outputname))
            {
                // Error
                Result = false;
                return false;
            }

            // Extension may have changed
            MediaFile = new FileInfo(outputname);

            // Refresh
            modified = true;
            return Refresh(true);
        }

        public bool DeInterlace(ref bool modified)
        {
            // Init
            Result = true;

            // Optional
            if (!Program.Config.ProcessOptions.DeInterlace)
                return true;

            // Use FFprobe for de-interlace detection
            if (!FfProbeInfo.FindNeedDeInterlace(out MediaInfo keepTracks, out MediaInfo deinterlaceTracks))
                return true;

            Log.Logger.Information("De-interlacing interlaced video : {Name}", MediaFile.Name);
            keepTracks.WriteLine("Keep");
            deinterlaceTracks.WriteLine("DeInterlace");

            // TODO : De-interlacing before re-encoding may be undesired if the media also requires re-encoding

            // Convert using HandBrakeCLI, it produces the best de-interlacing results
            // Convert will test for Options.TestNoModify
            if (!Convert.DeInterlaceToMkv(MediaFile.FullName, out string outputname))
            {
                // Error
                Result = false;
                return false;
            }

            // Extension may have changed
            MediaFile = new FileInfo(outputname);

            // Refresh
            modified = true;
            return Refresh(true);
        }

        public bool ReEncode(List<VideoInfo> reencodeVideoInfos, HashSet<string> reencodeAudioFormats, ref bool modified)
        {
            // Init
            Result = true;

            // Optional
            if (!Program.Config.ProcessOptions.ReEncode)
                return true;

            // Find all tracks that need re-encoding
            // Use FfProbeInfo becasue the video matching logic uses FFprobe attributes
            if (!FfProbeInfo.FindNeedReEncode(reencodeVideoInfos, reencodeAudioFormats, out MediaInfo keep, out MediaInfo reencode))
                // Nothing to do
                return true;

            Log.Logger.Information("Re-encoding required tracks : {Name}", MediaFile.Name);
            keep.WriteLine("Passthrough");
            reencode.WriteLine("ReEncode");

            // Reencode selected tracks
            // Convert will test for Options.TestNoModify
            if (!Convert.ConvertToMkv(MediaFile.FullName, keep, reencode, out string outputname))
            {
                // Error
                Result = false;
                return false;
            }

            // Extension may have changed
            MediaFile = new FileInfo(outputname);

            // Refresh
            modified = true;
            return Refresh(true);
        }

        public bool Verify(ref bool modified)
        {
            // Init
            Result = true;

            // Optional
            if (!Program.Config.ProcessOptions.Verify)
                return true;

            // If we are using a sidecar file we can use the last result
            if (Program.Config.ProcessOptions.UseSidecarFiles &&
                SidecarFile.Verified)
                // Done
                return true;

            // Skip files that are older than the minimum age
            if (Program.Config.VerifyOptions.MinimumFileAge > 0 &&
                DateTime.UtcNow - MediaFile.LastWriteTimeUtc > TimeSpan.FromDays(Program.Config.VerifyOptions.MinimumFileAge))
                // Done
                return true;

            // Break out and skip to end when processing is done
            bool failed = false;
            for (;;)
            { 
                // Need at least one video and audio track
                if (MediaInfoInfo.Video.Count == 0 || MediaInfoInfo.Audio.Count == 0)
                {
                    // File is missing required streams
                    Log.Logger.Error("File missing required tracks : {Name}", MediaFile.Name);
                    MediaInfoInfo.WriteLine("Invalid");
                    
                    // Done
                    failed = true;
                    break;
                }

                // Test playback duration
                if (MkvMergeInfo.Duration < TimeSpan.FromMinutes(Program.Config.VerifyOptions.MinimumDuration))
                {
                    // Playback duration is too short
                    Log.Logger.Error("File play duration is too short ({Duration}) : {Name}", MkvMergeInfo.Duration, MediaFile.Name);
                    MkvMergeInfo.WriteLine("Short");
                    
                    // Done
                    failed = true;
                    break;
                }

                // Verify media streams
                Log.Logger.Information("Verifying media streams : {Name}", MediaFile.Name);
                if (!Tools.FfMpeg.VerifyMedia(MediaFile.FullName, out string error))
                {
                    // Cancel requested
                    if (Program.IsCancelledError())
                        return false;

                    // Failed stream validation
                    Log.Logger.Error("Media stream validation failed : {Name}", MediaFile.Name);
                    Log.Logger.Error(error);

                    // Attempt file repair
                    if (!Repair(ref modified))
                    {
                        // Cancel requested
                        if (Program.IsCancelledError())
                            return false;

                        // Done
                        failed = true;
                        break;
                    }
                }

                // Get and compare interlaced flags
                {
                    // TODO: From sampling files GetIdetInfo is not a reliable way of detecting interlacing
                    bool mediainfoInterlaced = MediaInfoInfo.FindNeedDeInterlace(out MediaInfo _, out MediaInfo _);
                    bool ffprobeInterlaced = FfProbeInfo.FindNeedDeInterlace(out MediaInfo _, out MediaInfo _);
                    // MkvMergeInfo does not implement interlace detection
                    if (mediainfoInterlaced != ffprobeInterlaced)
                    {
                        Log.Logger.Warning("Interlaced flags do not match : {Name}", MediaFile.Name);
                        Log.Logger.Warning("MediaInfoInfo.FindNeedDeInterlace() : {MediainfoInterlaced}, FfProbeInfo.FindNeedDeInterlace() : {FfprobeInterlaced}", mediainfoInterlaced, ffprobeInterlaced);
                        Log.Logger.Information("Calculating idet info : {Name}", MediaFile.Name);
                        if (Tools.FfMpeg.GetIdetInfo(MediaFile.FullName, out FfMpegIdetInfo idetinfo))
                        {
                            bool idet = idetinfo.IsInterlaced(out double single, out double multi);
                            Log.Logger.Information("FfMpegIdetInfo.IsInterlaced ({Single:P} / {Multi:P}) : {Idet}", single, multi, idet);
                        }
                        else 
                        {
                            // Cancel requested
                            if (Program.IsCancelledError())
                                return false;

                            Log.Logger.Error("Failed to calculate idet info : {Name}", MediaFile.Name);
                            // Ignore error
                        }
                    }
                }

                // Cancel requested
                if (Program.IsCancelled())
                    return false;

                // Compute the bitrate
                // TODO : Verify that bitrate is acceptable for the resolution of the content, i.e. no YIFY, no fake 4K
                // https://4kmedia.org/real-or-fake-4k/
                // https://en.wikipedia.org/wiki/YIFY
                if (Program.Config.VerifyOptions.MaximumBitrate > 0)
                {
                    Log.Logger.Information("Calculating bitrate info : {Name}", MediaFile.Name);
                    if (GetBitrateInfo(out BitrateInfo bitrateInfo))
                    {
                        // Print combined audio and video bitrate
                        Log.Logger.Information(bitrateInfo.CombinedBitrate.ToString());

                        // Combined bitrate exceeded threshold
                        if (bitrateInfo.CombinedBitrate.Exceeded > 0)
                            Log.Logger.Warning("Maximum bitrate exceeded : {CombinedBitrate} > {MaximumBitrate}, {Exceeded} for {Duration}s : {Name}", Format.BytesToKilo(bitrateInfo.CombinedBitrate.Maximum * 8, "bps"), Format.BytesToKilo(Program.Config.VerifyOptions.MaximumBitrate, "bps"), bitrateInfo.CombinedBitrate.Exceeded, bitrateInfo.CombinedBitrate.Duration, MediaFile.Name);

                        // Audio bitrate exceeds video bitrate, may indicate an error with the video track
                        if (bitrateInfo.AudioBitrate.Average > bitrateInfo.VideoBitrate.Average)
                            Log.Logger.Warning("Audio bitrate exceeds Video bitrate : {AudioBitrate} > {VideoBitrate} : {Name}", Format.BytesToKilo(bitrateInfo.AudioBitrate.Average * 8, "bps"), Format.BytesToKilo(bitrateInfo.VideoBitrate.Average, "bps"), MediaFile.Name);
                    }
                    else 
                    {
                        // Cancel requested
                        if (Program.IsCancelledError())
                            return false;

                        Log.Logger.Error("Failed to calculate bitrate info : {Name}", MediaFile.Name);
                        // Ignore error
                    }
                }

                // Done
                break;
            }

            // Cancel requested
            if (Program.IsCancelled())
                return false;

            // All ok
            if (failed == false)
            {
                // Save the verified status in the sidecar file
                SidecarFile.Verified = true;
                if (Program.Config.ProcessOptions.UseSidecarFiles &&
                    !SidecarFile.WriteSidecarJson(MediaFile))
                {
                    // Error
                    Result = false;
                    return false;
                }

                // Done
                return true;
            }

            // Add the failed file to the ignore list
            // Don't add if delete not enabled
            // Don't add in test mode
            if (!Program.Options.TestNoModify &&
                !Program.Config.VerifyOptions.DeleteInvalidFiles &&
                Program.Config.VerifyOptions.RegisterInvalidFiles)
                Program.Config.ProcessOptions.FileIgnoreList.Add(MediaFile.FullName);

            // Don't delete if delete not enabled
            // Don't delete in test mode
            if (Program.Options.TestNoModify ||
                !Program.Config.VerifyOptions.DeleteInvalidFiles)
            {
                // Error
                Result = false;
                return false;
            }

            // Delete the media file
            Log.Logger.Information("Deleting media file due to failed validation : {Name}", MediaFile.FullName);
            if (!FileEx.DeleteFile(MediaFile.FullName) || 
                !FileEx.DeleteFile(SidecarFile.GetSidecarName(MediaFile)))
            {
                // Error
                Result = false;
                return false;
            }

            // File deleted, do not continue processing
            // Failed verify reported as an error
            modified = true;
            Result = false;
            return false;
        }

        public bool Repair(ref bool modified)
        {
            // Don't repair in test mode
            // Don't modify if repair not enabled
            if (Program.Options.TestNoModify ||
                !Program.Config.VerifyOptions.AutoRepair)
            {
                // Done
                return false;
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

            // Create a temp filename based on the input name
            string tempname = Path.ChangeExtension(MediaFile.FullName, ".tmp");

            // Convert using ffmpeg
            Log.Logger.Information("Attempting media repair by re-encoding using FfMpeg : {Name}", MediaFile.Name);
            if (!Tools.FfMpeg.ConvertToMkv(MediaFile.FullName, tempname))
            {
                // Failed, delete temp file
                FileEx.DeleteFile(tempname);

                // Cancel requested
                if (Program.IsCancelledError())
                    return false;

                // Try again using handbrake
                Log.Logger.Information("Attempting media repair by re-encoding using HandBrake : {Name}", MediaFile.Name);
                if (!Tools.HandBrake.ConvertToMkv(MediaFile.FullName, tempname))
                {
                    // Failed, delete temp file
                    FileEx.DeleteFile(tempname);

                    // Cancel requested
                    if (Program.IsCancelledError())
                        return false;

                    // Failed again
                    Log.Logger.Error("Re-encoding failed : {Name}", MediaFile.Name);
                    return false;
                }
            }

            // Re-encoding succeeded, re-verify the temp file
            Log.Logger.Information("Re-verifying media streams : {Name}", MediaFile.Name);
            if (!Tools.FfMpeg.VerifyMedia(tempname, out string error))
            {
                // Failed, delete temp file
                FileEx.DeleteFile(tempname);

                // Cancel requested
                if (Program.IsCancelledError())
                    return false;

                // Failed stream validation
                Log.Logger.Error("Media stream validation failed after repair attempt : {Name}", MediaFile.Name);
                Log.Logger.Error(error);
                return false;
            }

            // Rename the temp file to the original file
            if (!FileEx.RenameFile(tempname, MediaFile.FullName))
                return false;

            // Repair succeeded
            Log.Logger.Information("Repair succeeded : {Name}", MediaFile.Name);

            // Refresh
            modified = true;
            return Refresh(true);
        }

        private bool Refresh(bool refresh)
        {
            // Call Refresh() at each processing function exit
            // The refresh hint will force a reload, else the media file will be compared with the sidecar and conditionally updated

            if (refresh)
                MediaFile = new FileInfo(MediaFile.FullName);

            if (Program.Config.ProcessOptions.UseSidecarFiles)
            {
                // Get info from sidecar file
                // If the sidecar does not exist it will be created
                if (!SidecarFile.GetMediaInfo(MediaFile, refresh))
                {
                    Result = false;
                    return false;
                }

                // Assign results
                FfProbeInfo = SidecarFile.FfProbeInfo;
                MkvMergeInfo = SidecarFile.MkvMergeInfo;
                MediaInfoInfo = SidecarFile.MediaInfoInfo;

                Result = true;
                return true;
            }

            // Get info directly
            if (!MediaInfo.GetMediaInfo(MediaFile, out MediaInfo ffprobeInfo, out MediaInfo mkvmergeInfo, out MediaInfo mediainfoInfo))
            {
                Result = false;
                return false;
            }

            // Assign results
            FfProbeInfo = ffprobeInfo;
            MkvMergeInfo = mkvmergeInfo;
            MediaInfoInfo = mediainfoInfo;

            Result = true;
            return true;
        }

        public bool GetMediaInfo()
        {
            // By now all the files we are processing should be MKV files
            Debug.Assert(MkvMergeTool.IsMkvFile(MediaFile));

            return Refresh(false);
        }

        public bool MonitorFileTime(int seconds)
        {
            bool timestampChanged = false;
            MediaFile.Refresh();
            DateTime fileTime = MediaFile.LastWriteTimeUtc;
            Log.Logger.Information("MonitorFileTime : {FileTime} : {Name}\"", fileTime, MediaFile.Name);
            for (int i = 0; i < seconds; i ++)
            {
                if (Program.IsCancelled(1000))
                    break;
                MediaFile.Refresh();
                if (MediaFile.LastWriteTimeUtc != fileTime)
                {
                    timestampChanged = true;
                    Log.Logger.Warning("MonitorFileTime : {LastWriteTimeUtc} != {FileTime} : {Name}", MediaFile.LastWriteTimeUtc, fileTime, MediaFile.Name);
                }
                fileTime = MediaFile.LastWriteTimeUtc;
            }

            return timestampChanged;
        }

        public bool GetBitrateInfo(out BitrateInfo bitrateInfo)
        {
            bitrateInfo = null;

            // Get packet info
            if (!Tools.FfProbe.GetPacketInfo(MediaFile.FullName, out List<Packet> packetList))
                return false;

            // Compute bitrate from packets
            // Use the first video and audio track for calculation
            // TODO: Use default tracks
            bitrateInfo = new BitrateInfo();
            bitrateInfo.Calculate(packetList, FfProbeInfo.Video.First().Id, FfProbeInfo.Audio.First().Id, Program.Config.VerifyOptions.MaximumBitrate / 8);

            return true;
        }

        public bool Result { get; set; }
        public  MediaInfo FfProbeInfo { get; set; }
        public MediaInfo MkvMergeInfo { get; set; }
        public MediaInfo MediaInfoInfo { get; set; }

        private FileInfo MediaFile;
        private readonly SidecarFile SidecarFile = new SidecarFile();
    }
}
