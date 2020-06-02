using InsaneGenius.Utilities;
using PlexCleaner.FfMpegToolJsonSchema;
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
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole($"Deleting file with undesired extension : \"{MediaFile.Name}\"");

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
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole($"Deleting sidecar file with no matching MKV file : \"{MediaFile.Name}\"");

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
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole($"ReMux file matched by extension : \"{MediaFile.Name}\"");

            // Remux the file, use the new filename
            // Convert will test for Options.TestNoModify
            if (!Convert.ReMuxToMkv(MediaFile.FullName, out string outputname))
            {
                // Error
                Result = false;
                return false;
            }

            // Refresh
            modified = true;
            MediaFile = new FileInfo(outputname);

            // In test mode the file will not be remuxed to MKV so abort
            if (Program.Options.TestNoModify)
                return false;

            return Refresh(true);
        }

        public void MediaInfoErrors()
        {
            // Do we have any errors
            if (FfProbeInfo.HasErrors || MkvMergeInfo.HasErrors || MediaInfoInfo.HasErrors)
            {
                ConsoleEx.WriteLine("");
                Program.LogFile.LogConsole($"Warning : Media file has metadata errors : \"{MediaFile.Name}\"");
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
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole($"Clearing all tags from media file : \"{MediaFile.Name}\"");

            // Delete the tags
            if (!Program.Options.TestNoModify &&
                !MkvTool.ClearMkvTags(MediaFile.FullName))
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

            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole($"Setting unknown language tracks to \"{Program.Config.ProcessOptions.DefaultLanguage}\" : \"{MediaFile.Name}\"");
            known.WriteLine("Known");
            unknown.WriteLine("Unknown");

            // Set the track language to the default language
            if (!Program.Options.TestNoModify &&
                !MkvTool.SetMkvTrackLanguage(MediaFile.FullName, unknown, Program.Config.ProcessOptions.DefaultLanguage))
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
            MediaInfo removeTracks = new MediaInfo(MediaInfo.ParserType.MkvMerge);

            // Get all unwanted language tracks
            // Use MKVMerge logic
            if (Program.Config.ProcessOptions.RemoveUnwantedLanguageTracks &&
                MkvMergeInfo.FindUnwantedLanguage(keepLanguages, out MediaInfo keep, out MediaInfo remove))
            {
                ConsoleEx.WriteLine("");
                Program.LogFile.LogConsole($"Removing unwanted language tracks : \"{MediaFile.Name}\"");
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
                ConsoleEx.WriteLine("");
                Program.LogFile.LogConsole($"Removing duplicate tracks : \"{MediaFile.Name}\"");
                keep.WriteLine("Keep");
                remove.WriteLine("Remove");

                // Remove the duplicate tracks
                keepTracks.RemoveTracks(remove);
                removeTracks.AddTracks(remove);
                remux = true;
            }

            // Do any tracks need remuxing
            // Use MediaInfo logic
            if (Program.Config.ProcessOptions.ReMux &&
                MediaInfoInfo.FindNeedReMux(out keep, out remove))
            {
                ConsoleEx.WriteLine("");
                Program.LogFile.LogConsole($"Re-muxing problematic tracks : \"{MediaFile.Name}\"");
                keep.WriteLine("Keep");
                remove.WriteLine("ReMux");

                // We can't remux per track, we need to remux entire file
                remux = true;
            }

            // Any remuxing to do
            if (!remux)
                // Done
                return true;

            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole($"Re-muxing union of tracks : \"{MediaFile.Name}\"");
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

            // Refresh
            modified = true;
            MediaFile = new FileInfo(outputname);
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

            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole($"De-interlacing interlaced video : \"{MediaFile.Name}\"");
            keepTracks.WriteLine("Keep");
            deinterlaceTracks.WriteLine("DeInterlace");

            // TODO : Add support for H265 encoding
            if (FfProbeInfo.Video.First().Format.Equals("HEVC", StringComparison.OrdinalIgnoreCase))
            {
                ConsoleEx.WriteLine("");
                Program.LogFile.LogConsole($"Warning : De-interlacing H265 to H264 not supported, skipping de-interlace : \"{MediaFile.Name}\"");
                return true;
            }

            // TODO : De-interlacing before re-encoding may be undesired if the media also requires re-encoding

            // Convert using HandBrakeCLI, it produces the best de-interlacing results
            // Convert will test for Options.TestNoModify
            if (!Convert.DeInterlaceToMkv(MediaFile.FullName, out string outputname))
            {
                // Error
                Result = false;
                return false;
            }

            // Refresh
            modified = true;
            MediaFile = new FileInfo(outputname);
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

            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole($"Re-encoding required tracks : \"{MediaFile.Name}\"");
            keep.WriteLine("Passthrough");
            reencode.WriteLine("ReEncode");

            // TODO : Add support for H265 encoding
            if (FfProbeInfo.Video.First().Format.Equals("HEVC", StringComparison.OrdinalIgnoreCase))
            {
                ConsoleEx.WriteLine("");
                Program.LogFile.LogConsole($"Warning : De-interlacing H265 to H264 not supported, skipping re-encode : \"{MediaFile.Name}\"");
                return true;
            }

            // Reencode selected tracks
            // Convert will test for Options.TestNoModify
            if (!Convert.ConvertToMkv(MediaFile.FullName, keep, reencode, out string outputname))
            {
                // Error
                Result = false;
                return false;
            }

            // Refresh
            modified = true;
            MediaFile = new FileInfo(outputname);
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
                (DateTime.UtcNow - MediaFile.LastWriteTimeUtc) > TimeSpan.FromDays(Program.Config.VerifyOptions.MinimumFileAge))
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
                    ConsoleEx.WriteLine("");
                    Program.LogFile.LogConsole($"Error : File missing required tracks : \"{MediaFile.Name}\"");
                    MediaInfoInfo.WriteLine("Invalid");
                    
                    // Done
                    failed = true;
                    break;
                }

                // Test duration
                if (MkvMergeInfo.Duration < TimeSpan.FromMinutes(Program.Config.VerifyOptions.MinimumDuration))
                {
                    // File is too short
                    ConsoleEx.WriteLine("");
                    Program.LogFile.LogConsole($"Error : File play duration is too short ({MkvMergeInfo.Duration}) : \"{MediaFile.Name}\"");
                    MkvMergeInfo.WriteLine("Short");
                    
                    // Done
                    failed = true;
                    break;
                }

                // Verify media streams
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLine($"Verifying media streams : \"{MediaFile.Name}\"");
                if (!FfMpegTool.VerifyMedia(MediaFile.FullName, out string error))
                {
                    // Cancel requested
                    if (Program.Cancel.State)
                        return false;

                    // Failed stream validation
                    ConsoleEx.WriteLine("");
                    Program.LogFile.LogConsole($"Error : Media stream validation failed : \"{MediaFile.Name}\"");
                    Program.LogFile.LogConsole(error);

                    // Don't repair in test mode
                    // Don't modify if repair not enabled
                    if (Program.Options.TestNoModify ||
                        !Program.Config.VerifyOptions.AutoRepair)
                    {
                        // Done
                        failed = true;
                        break;
                    }

                    // Try to repair the file
                    for (;;)
                    {
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

                        // FFmpeg fails to decode some files, so we use Handbrake to re-encode the video and audio
                        // https://trac.ffmpeg.org/search?q=%22Invalid+NAL+unit+size%22&noquickjump=1&milestone=on&ticket=on&wiki=on
                        // https://trac.ffmpeg.org/search?q=%22non+monotonically+increasing+dts+to+muxer%22&noquickjump=1&milestone=on&ticket=on&wiki=on

                        // Re-encode audio and video
                        ConsoleEx.WriteLine("");
                        Program.LogFile.LogConsole($"Attempting media repair by re-encoding : \"{MediaFile.Name}\"");
                        if (!Convert.ConvertToMkvHandBrake(MediaFile.FullName, out string outputname))
                        {
                            // Cancel requested
                            if (Program.Cancel.State)
                                return false;

                            ConsoleEx.WriteLine("");
                            Program.LogFile.LogConsole($"Error : Re-encoding failed : \"{MediaFile.Name}\"");

                            // Failed repair
                            failed = true;
                            break;
                        }

                        // Re-verify after repair
                        ConsoleEx.WriteLine("");
                        ConsoleEx.WriteLine($"Re-verifying media streams : \"{MediaFile.Name}\"");
                        if (!FfMpegTool.VerifyMedia(MediaFile.FullName, out error))
                        {
                            // Cancel requested
                            if (Program.Cancel.State)
                                return false;

                            // Failed stream validation
                            ConsoleEx.WriteLine("");
                            Program.LogFile.LogConsole($"Error : Media stream validation failed after repair attempt : \"{MediaFile.Name}\"");
                            Program.LogFile.LogConsole(error);

                            // Failed repair
                            failed = true;
                            break;
                        }

                        // Repair succeeded
                        ConsoleEx.WriteLine("");
                        Program.LogFile.LogConsole($"Repair succeeded : \"{MediaFile.Name}\"");

                        // Refresh
                        modified = true;
                        MediaFile = new FileInfo(outputname);
                        if (!Refresh(true))
                        {
                            // Error
                            Result = false;
                            return false;
                        }

                        // Done
                        break;
                    }

                    // Done
                    if (failed)
                        break;
                }

                {
                    // TODO: From sampling files GetIdetInfo is not a reliable way of detecting interlacing
                    // Get interlaced flags
                    bool mediainfo = MediaInfoInfo.FindNeedDeInterlace(out MediaInfo _, out MediaInfo _);
                    bool ffprobe = FfProbeInfo.FindNeedDeInterlace(out MediaInfo _, out MediaInfo _);
                    // MkvMergeInfo does not implement interlace detection
                    if (mediainfo != ffprobe)
                    {
                        ConsoleEx.WriteLine("");
                        Program.LogFile.LogConsole($"Warning : Interlaced flags do not match : \"{MediaFile.Name}\"");
                        Program.LogFile.LogConsole($"MediaInfoInfo.FindNeedDeInterlace() : {mediainfo}");
                        Program.LogFile.LogConsole($"FfProbeInfo.FindNeedDeInterlace() : {ffprobe}");
                        ConsoleEx.WriteLine($"Calculating idet info : \"{MediaFile.Name}\"");
                        if (FfMpegTool.GetIdetInfo(MediaFile.FullName, out FfMpegIdetInfo idetinfo))
                        {
                            bool idet = idetinfo.IsInterlaced(out double single, out double multi);
                            Program.LogFile.LogConsole($"FfMpegIdetInfo.IsInterlaced ({single:P} / {multi:P}) : {idet}");
                        }
                        else 
                        {
                            // Cancel requested
                            if (Program.Cancel.State)
                                return false;

                            ConsoleEx.WriteLine("");
                            Program.LogFile.LogConsole($"Error : Failed to calculate idet info : \"{MediaFile.Name}\"");
                            // Ignore error
                        }
                    }
                }

                // Cancel requested
                if (Program.Cancel.State)
                    return false;

                // Compute the bitrate
                if (Program.Config.VerifyOptions.MaximumBitrate > 0)
                {
                    ConsoleEx.WriteLine("");
                    ConsoleEx.WriteLine($"Calculating bitrate info : \"{MediaFile.Name}\"");
                    if (GetBitrateInfo(out BitrateInfo bitrateInfo))
                    {
                        // TODO : Verify that bitrate is acceptable for the resolution of the content, i.e. no YIFY, no fake 4K
                        // https://4kmedia.org/real-or-fake-4k/
                        // https://en.wikipedia.org/wiki/YIFY

                        if (bitrateInfo.ThresholdExceeded > 0)
                        { 
                            ConsoleEx.WriteLine("");
                            Program.LogFile.LogConsole($"Warning : Maximum bitrate exceeded : {Format.BytesToKilo(bitrateInfo.Maximum * 8, "bps")} > {Format.BytesToKilo(bitrateInfo.Threshold * 8, "bps")} : \"{MediaFile.Name}\"");
                            Program.LogFile.LogConsole(bitrateInfo.ToString());
                        }
                    }
                    else 
                    {
                        // Cancel requested
                        if (Program.Cancel.State)
                            return false;

                        ConsoleEx.WriteLine("");
                        Program.LogFile.LogConsole($"Error : Failed to calculate bitrate info : \"{MediaFile.Name}\"");
                        // Ignore error
                    }
                }

                // Done
                break;
            }

            // Cancel requested
            if (Program.Cancel.State)
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

            // Don't delete if not enabled
            // Don't delete in test mode
            if (Program.Options.TestNoModify ||
                !Program.Config.VerifyOptions.DeleteInvalidFiles)
            {
                // Error
                Result = false;
                return false;
            }

            // Delete file
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole($"Deleting media file due to failed validation : \"{MediaFile.Name}\"");
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

        private bool Refresh(bool refresh)
        {
            // Call Refresh() at each processing function exit
            // The refresh hint will force a reload, else the media file will be compared with the sidecar and conditionally updated

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
            MediaInfo ffprobeInfo = null;
            MediaInfo mkvmergeInfo = null;
            MediaInfo mediainfoInfo = null;
            if (!MediaInfo.GetMediaInfo(MediaFile, out ffprobeInfo, out mkvmergeInfo, out mediainfoInfo))
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
            Debug.Assert(MkvTool.IsMkvFile(MediaFile));

            return Refresh(false);
        }

        public void MonitorFileTime(int seconds)
        {
            MediaFile.Refresh();
            DateTime fileTime = MediaFile.LastWriteTimeUtc;
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole($"Warning : MonitorFileTime : {fileTime} : \"{MediaFile.Name}\"");
            for (int i = 0; i < seconds; i ++)
            {
                if (Program.Cancel.WaitForSet(1000))
                    break;
                MediaFile.Refresh();
                if (MediaFile.LastWriteTimeUtc != fileTime)
                    Program.LogFile.LogConsole($"Warning : MonitorFileTime : {MediaFile.LastWriteTimeUtc} != {fileTime} : \"{MediaFile.Name}\"");
                fileTime = MediaFile.LastWriteTimeUtc;
            }
        }

        public bool GetBitrateInfo(out BitrateInfo bitrateInfo)
        {
            bitrateInfo = null;

            List<Packet> packetList = null;
            if (!FfMpegTool.GetPacketInfo(MediaFile.FullName, out packetList))
                return false;

            // Compute bitrate
            bitrateInfo = new BitrateInfo();
            bitrateInfo.Threshold = Program.Config.VerifyOptions.MaximumBitrate / 8;
            bitrateInfo.Calculate(packetList);

            return true;
        }

        public bool Result { get; set; }
        public  MediaInfo FfProbeInfo { get; set; }
        public MediaInfo MkvMergeInfo { get; set; }
        public MediaInfo MediaInfoInfo { get; set; }

        private FileInfo MediaFile;
        private SidecarFile SidecarFile = new SidecarFile();
    }
}
