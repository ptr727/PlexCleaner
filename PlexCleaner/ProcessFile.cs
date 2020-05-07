using InsaneGenius.Utilities;
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
            Program.LogFile.LogConsole("");
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
            Program.LogFile.LogConsole("");
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
            Program.LogFile.LogConsole("");
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

            return Refresh();
        }

        public void MediaInfoErrors()
        {
            // Do we have any errors
            if (FfProbeInfo.HasErrors || MkvMergeInfo.HasErrors || MediaInfoInfo.HasErrors)
            {
                Program.LogFile.LogConsole("");
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
            Program.LogFile.LogConsole("");
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
            return Refresh();
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

            Program.LogFile.LogConsole("");
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
            return Refresh();
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
                Program.LogFile.LogConsole("");
                Program.LogFile.LogConsole($"Removing unwanted language tracks : \"{MediaFile.Name}\"");
                keep.WriteLine("Keep");
                remove.WriteLine("Remove");
                Program.LogFile.LogConsole("");

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
                Program.LogFile.LogConsole("");
                Program.LogFile.LogConsole($"Removing duplicate tracks : \"{MediaFile.Name}\"");
                keep.WriteLine("Keep");
                remove.WriteLine("Remove");
                Program.LogFile.LogConsole("");

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
                Program.LogFile.LogConsole("");
                Program.LogFile.LogConsole($"Re-muxing problematic tracks : \"{MediaFile.Name}\"");
                keep.WriteLine("Keep");
                remove.WriteLine("ReMux");
                Program.LogFile.LogConsole("");

                // We can't remux per track, we need to remux entire file
                remux = true;
            }

            // Any remuxing to do
            if (!remux)
                // Done
                return true;

            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"Re-muxing union of tracks : \"{MediaFile.Name}\"");
            keepTracks.WriteLine("Keep");
            removeTracks.WriteLine("Remove");
            Program.LogFile.LogConsole("");

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
            return Refresh();
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

            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"De-interlacing interlaced video : \"{MediaFile.Name}\"");
            keepTracks.WriteLine("Keep");
            deinterlaceTracks.WriteLine("DeInterlace");

            // TODO : Add support for H265 encoding
            if (FfProbeInfo.Video.First().Format.Equals("HEVC", StringComparison.OrdinalIgnoreCase))
            {
                Program.LogFile.LogConsole($"Warning : De-interlacing H265 to H264 not supported, skipping de-interlace : \"{MediaFile.Name}\"");
                Program.LogFile.LogConsole("");
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
            return Refresh();
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

            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"Re-encoding required tracks : \"{MediaFile.Name}\"");
            keep.WriteLine("Passthrough");
            reencode.WriteLine("ReEncode");

            // TODO : Add support for H265 encoding
            if (FfProbeInfo.Video.First().Format.Equals("HEVC", StringComparison.OrdinalIgnoreCase))
            {
                Program.LogFile.LogConsole($"Warning : De-interlacing H265 to H264 not supported, skipping re-encode : \"{MediaFile.Name}\"");
                Program.LogFile.LogConsole("");
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
            return Refresh();
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

            // Need at least one video and audio track
            bool delete = false;
            if (MediaInfoInfo.Video.Count == 0 || MediaInfoInfo.Audio.Count == 0)
            {
                // File is missing required streams
                Program.LogFile.LogConsole("");
                Program.LogFile.LogConsole($"Error : File missing required tracks : \"{MediaFile.Name}\"");
                MediaInfoInfo.WriteLine("Invalid");
                Program.LogFile.LogConsole("");
                delete = true;
            }

            // Test duration
            if (MkvMergeInfo.Duration < TimeSpan.FromMinutes(Program.Config.VerifyOptions.MinimumDuration))
            {
                // File is too short
                Program.LogFile.LogConsole("");
                Program.LogFile.LogConsole($"Error : File play duration is too short ({MkvMergeInfo.Duration}) : \"{MediaFile.Name}\"");
                MkvMergeInfo.WriteLine("Short");
                Program.LogFile.LogConsole("");
                delete = true;
            }

            // Verify media streams
            if (!FfMpegTool.VerifyMedia(MediaFile.FullName, out string error))
            { 
                // Failed streaming validation
                Program.LogFile.LogConsole("");
                Program.LogFile.LogConsole($"Error : Media stream validation failed : \"{MediaFile.Name}\"");
                Program.LogFile.LogConsole(error);
                FfProbeInfo.WriteLine("Failed");
                Program.LogFile.LogConsole("");

                // Try to repair the file
                if (!Program.Options.TestNoModify &&
                    Program.Config.VerifyOptions.AutoRepair)
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

                    // Some files cannot be decoded by ffmpeg so we use Handbrake to repair
                    // https://trac.ffmpeg.org/search?q=%22Invalid+NAL+unit+size%22&noquickjump=1&milestone=on&ticket=on&wiki=on

                    // Some content 
                    Program.LogFile.LogConsole($"Attempting media repair : \"{MediaFile.Name}\"");
                    if (!Convert.ConvertToMkvHandBrake(MediaFile.FullName, out string outputname) ||
                        !FfMpegTool.VerifyMedia(MediaFile.FullName, out error))
                    {
                        Program.LogFile.LogConsole($"Repair failed : \"{MediaFile.Name}\"");
                        Program.LogFile.LogConsole(error);
                        Program.LogFile.LogConsole("");
                        
                        // Failed repair
                        delete = true;
                    }
                    else
                    {
                        Program.LogFile.LogConsole($"Repair succeeded : \"{MediaFile.Name}\"");
                        Program.LogFile.LogConsole("");

                        // Refresh
                        modified = true;
                        MediaFile = new FileInfo(outputname);
                        if (!Refresh())
                        {
                            // Error
                            Result = false;
                            return false;
                        }
                    }
                }
                else
                    // Don't repair
                    delete = true;
            }

            {
                // From sampling GetIdetInfo is not a reliable way of detecting interlacing
                // MkvMergeInfo does not implement interlace detection

                // Get interlaced flags
                bool mediainfo = MediaInfoInfo.FindNeedDeInterlace(out MediaInfo _, out MediaInfo _);
                bool ffprobe = FfProbeInfo.FindNeedDeInterlace(out MediaInfo _, out MediaInfo _);
                if (mediainfo != ffprobe)
                {

                    Program.LogFile.LogConsole("");
                    Program.LogFile.LogConsole($"Warning : Interlaced flags do not match : \"{MediaFile.Name}\"");
                    Program.LogFile.LogConsole($"MediaInfoInfo.FindNeedDeInterlace() : {mediainfo}");
                    Program.LogFile.LogConsole($"FfProbeInfo.FindNeedDeInterlace() : {ffprobe}");
                    if (FfMpegTool.GetIdetInfo(MediaFile.FullName, out FfMpegIdetInfo idetinfo))
                    {
                        bool idet = idetinfo.IsInterlaced(out double single, out double multi);
                        Program.LogFile.LogConsole($"FfMpegIdetInfo.IsInterlaced ({single:P} / {multi:P}) : {idet}");
                    }
                    Program.LogFile.LogConsole("");
                }

                // Nothing to do
            }

            // TODO : Verify bitrate to not exceed network speed
            // https://www.reddit.com/r/PleX/comments/eoa03e/psa_100_mbps_is_not_enough_to_direct_play_4k/
            // https://github.com/slhck/ffmpeg-bitrate-stats

            // All ok
            if (delete == false)
            {
                // Save the verified status in the sidecar file
                SidecarFile.Verified = true;
                if (Program.Config.ProcessOptions.UseSidecarFiles &&
                    !SidecarFile.WriteSidecar(MediaFile))
                {
                    // Error
                    Result = false;
                    return false;
                }

                // Done
                return true;
            }

            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"Deleting media file due to failed validation : \"{MediaFile.Name}\"");

            // Delete the media file and the sidecar file
            if (!Program.Options.TestNoModify &&
                Program.Config.VerifyOptions.DeleteInvalidFiles &&
                (!FileEx.DeleteFile(MediaFile.FullName) || !FileEx.DeleteFile(SidecarFile.GetSidecarName(MediaFile))))
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

        private bool Refresh()
        {
            // Call Refresh() at each processing function exit

            if (Program.Config.ProcessOptions.UseSidecarFiles)
            {
                // Get info from sidecar file
                // If the sidecar does not exist it will be created
                if (!SidecarFile.GetMediaInfo(MediaFile))
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

            return Refresh();
        }

        public void MonitorFileTime(int seconds)
        {
            MediaFile.Refresh();
            DateTime fileTime = MediaFile.LastWriteTimeUtc;
            Program.LogFile.LogConsole("");
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
            Program.LogFile.LogConsole("");
        }

        public bool Result { get; set; }
        public  MediaInfo FfProbeInfo { get; set; }
        public MediaInfo MkvMergeInfo { get; set; }
        public MediaInfo MediaInfoInfo { get; set; }

        private FileInfo MediaFile;
        private SidecarFile SidecarFile = new SidecarFile();
    }
}
