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
            if (!Process.Options.DeleteUnwantedExtensions)
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
            if (!Process.Options.ReMux)
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

        public void CheckForErrors()
        {
            // Do we have any errors
            if (FfProbeInfo.HasErrors || MkvMergeInfo.HasErrors || MediaInfoInfo.HasErrors)
            {
                Program.LogFile.LogConsole("");
                Program.LogFile.LogConsole($"Media file has possible errors : \"{MediaFile.Name}\"");
            }
        }

        public bool RemoveTags(ref bool modified)
        {
            // Init
            Result = true;

            // Optional
            if (!Process.Options.RemoveTags)
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
            if (!Process.Options.SetUnknownLanguage)
                return true;

            // Find unknown languages
            if (!MkvMergeInfo.FindUnknownLanguage(out MediaInfo known, out MediaInfo unknown))
                // Nothing to do
                return true;

            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"Setting unknown language tracks to \"{Process.Options.DefaultLanguage}\" : \"{MediaFile.Name}\"");
            known.WriteLine("Known");
            unknown.WriteLine("Unknown");

            // Set the track language to the default language
            if (!Program.Options.TestNoModify &&
                !MkvTool.SetMkvTrackLanguage(MediaFile.FullName, unknown, Process.Options.DefaultLanguage))
            {
                // Error
                Result = false;
                return false;
            }

            // Refresh
            modified = true;
            return Refresh();
        }

        public bool ReMuxMulti(HashSet<string> keepLanguages, List<string> preferredAudioFormats, ref bool modified)
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
            if (Process.Options.RemoveUnwantedLanguageTracks &&
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
            if (Process.Options.RemoveDuplicateTracks &&
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
            if (Process.Options.ReMux &&
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
            if (!Process.Options.DeInterlace)
                return true;

            // TODO : Figure out what method is the most reliable
            // FFprobe may be more reliable compared to MediaInfo for some files
            // There are media files with mixed content measured using idet frame counts, even if the stream flags say otherwise
            {
                // MkvMergeInfo does not implement interlace detection
                int count = 0;
                bool mediainfo = MediaInfoInfo.FindNeedDeInterlace(out MediaInfo _, out MediaInfo _);
                if (mediainfo)
                    count ++;
                bool ffprobe = FfProbeInfo.FindNeedDeInterlace(out MediaInfo _, out MediaInfo _);
                if (ffprobe)
                    count ++;
                bool idet = FfMpegIdetInfo.IsInterlaced(out double single, out double multi);
                if (idet)
                    count ++;
                // Log any disagreement
                if (count != 0 && count != 3)
                { 
                    Trace.WriteLine($"MediaInfoInfo.FindNeedDeInterlace() : {mediainfo} : \"{MediaFile.Name}\"");
                    Trace.WriteLine($"FfProbeInfo.FindNeedDeInterlace() : {ffprobe} : \"{MediaFile.Name}\"");
                    Trace.WriteLine($"FfMpegIdetInfo.IsInterlaced({single:P}/{multi:P}) : {idet} : \"{MediaFile.Name}\"");
                }
            }

            // Use FFprobe
            if (!FfProbeInfo.FindNeedDeInterlace(out MediaInfo keepTracks, out MediaInfo deinterlaceTracks))
                return true;

            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"De-interlacing interlaced video : \"{MediaFile.Name}\"");
            keepTracks.WriteLine("Keep");
            deinterlaceTracks.WriteLine("DeInterlace");

            // TODO : Add support for H265 encoding
            if (MediaInfoInfo.Video.First().Format.Equals("HEVC", StringComparison.OrdinalIgnoreCase))
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
            if (!Process.Options.ReEncode)
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

        public bool VerifyTrackCount(ref bool modified)
        {
            // Init
            Result = true;

            // Optional
            if (!Process.Options.DeleteInvalidFiles)
                return true;

            // Need at least one video and audio track
            if (MediaInfoInfo.Video.Count >= 1 && MediaInfoInfo.Audio.Count >= 1)
                // Ok
                return true;

            // File is missing required streams
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"File missing required tracks : \"{MediaFile.Name}\"");
            MediaInfoInfo.WriteLine("Invalid");

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

        public bool VerifyStreams(ref bool modified)
        {
            // Init
            Result = true;

            // Optional
            if (!Process.Options.DeleteInvalidFiles)
                return true;

            // Verify media streams
            if (FfMpegTool.VerifyMedia(MediaFile.FullName))
                // Ok
                return true;

            // Failed streaming validation
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"Media stream validation failed : \"{MediaFile.Name}\"");
            FfProbeInfo.WriteLine("Failed");

            // TODO : Complete testing before activating
            return true;
            
            /*
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
            */
        }

        public bool VerifyDuration(ref bool modified)
        {
            // Init
            Result = true;

            // Optional
            if (!Process.Options.DeleteInvalidFiles)
                return true;

            // The duration needs to be longer than 5 minutes
            if (MkvMergeInfo.Duration >= TimeSpan.FromMinutes(5))
                // Ok
                return true;

            // File is too short
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"Warning : File play duration is only {MkvMergeInfo.Duration} : \"{MediaFile.Name}\"");
            MkvMergeInfo.WriteLine("Short");

            // TODO : Complete testing before activating
            return true;
            
            /*
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
            */
        }

        private bool Refresh()
        {
            // Call Refresh() at each processing function exit

            if (Process.Options.UseSidecarFiles)
            {
                // Get info from sidecar file
                SidecarFile sidecarFile = new SidecarFile();
                if (!sidecarFile.GetMediaInfo(MediaFile))
                {
                    Result = false;
                    return false;
                }

                // Assign results
                FfProbeInfo = sidecarFile.FfProbeInfo;
                MkvMergeInfo = sidecarFile.MkvMergeInfo;
                MediaInfoInfo = sidecarFile.MediaInfoInfo;
                FfMpegIdetInfo = sidecarFile.FfMpegIdetInfo;

                Result = true;
                return true;
            }

            // Get info directly
            MediaInfo ffprobeInfo = null;
            MediaInfo mkvmergeInfo = null;
            MediaInfo mediainfoInfo = null;
            FfMpegIdetInfo ffmpegidetInfo = null;
            if (!MediaInfo.GetMediaInfo(MediaFile, out ffprobeInfo, out mkvmergeInfo, out mediainfoInfo) ||
                !FfMpegIdetInfo.GetIdetInfo(MediaFile, out ffmpegidetInfo))
                Result = false;

            // Assign results
            FfProbeInfo = ffprobeInfo;
            MkvMergeInfo = mkvmergeInfo;
            MediaInfoInfo = mediainfoInfo;
            FfMpegIdetInfo = ffmpegidetInfo;

            return Result;
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
        public FfMpegIdetInfo FfMpegIdetInfo { get; set; }

        private FileInfo MediaFile;
    }
}
