using InsaneGenius.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace PlexCleaner
{
    public class ProcessFile
    {
        public ProcessFile(FileInfo fileInfo)
        {
            TargetFile = fileInfo;
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
            if (keepExtensions.Contains(TargetFile.Extension) ||
                remuxExtensions.Contains(TargetFile.Extension))
                // Keep file, nothing more to do
                return true;

            // Delete the file
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"Deleting file with undesired extension : \"{TargetFile.Name}\"");

            // Delete the file
            if (!Process.Options.TestNoModify &&
                !FileEx.DeleteFile(TargetFile.FullName))
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
            if (!SidecarFile.IsSidecarExtension(TargetFile.Extension))
                // Nothing to do
                return true;

            // Get the matching MKV file
            string mediafile = Path.ChangeExtension(TargetFile.FullName, ".mkv");

            // Does the media file exist
            if (File.Exists(mediafile))
                // File exists, nothing more to do
                return true;

            // Media file does not exists, delete this sidecar file
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"Deleting sidecar file with no matching MKV file : \"{TargetFile.Name}\"");

            // Delete the file
            if (!Process.Options.TestNoModify &&
                !FileEx.DeleteFile(TargetFile.FullName))
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
            if (!remuxExtensions.Contains(TargetFile.Extension))
                // Nothing to do
                return true;

            // ReMux the file
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"ReMux file matched by extension : \"{TargetFile.Name}\"");

            // Remux the file, use the new filename
            // Convert will test for Options.TestNoModify
            if (!Convert.ReMuxToMkv(TargetFile.FullName, out string outputname))
            {
                // Error
                Result = false;
                return false;
            }

            // Refresh
            modified = true;
            TargetFile = new FileInfo(outputname);

            // In test mode the file will not be remuxed to MKV so abort
            if (Process.Options.TestNoModify)
                return false;

            return Refresh();
        }

        public void CheckForErrors()
        {
            // Do we have any errors
            Refresh();
            if (FfProbeInfo.HasErrors || MkvMergeInfo.HasErrors || MediaInfoInfo.HasErrors)
            {
                Program.LogFile.LogConsole("");
                Program.LogFile.LogConsole($"Media file has errors : \"{TargetFile.Name}\"");
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
            Program.LogFile.LogConsole($"Media file contains tags, clearing all tags : \"{TargetFile.Name}\"");

            // Delete the tags
            if (!Process.Options.TestNoModify &&
                !MkvTool.ClearMkvTags(TargetFile.FullName))
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
            Program.LogFile.LogConsole($"Found tracks with an unknown language, setting to \"{Process.Options.DefaultLanguage}\" : \"{TargetFile.Name}\"");
            known.WriteLine("Known");
            unknown.WriteLine("Unknown");

            // Set the track language to the default language
            if (!Process.Options.TestNoModify &&
                !MkvTool.SetMkvTrackLanguage(TargetFile.FullName, unknown, Process.Options.DefaultLanguage))
            {
                // Error
                Result = false;
                return false;
            }

            // Refresh
            modified = true;
            return Refresh();
        }

        public bool RemoveUnwantedLanguages(HashSet<string> keepLanguages, ref bool modified)
        {
            // Init
            Result = true;

            // Optional
            if (!Process.Options.RemoveUnwantedLanguageTracks)
                return true;

            // Find unwanted languages
            if (!MkvMergeInfo.FindUnwantedLanguage(keepLanguages, out MediaInfo keep, out MediaInfo remove))
                // Nothing to do
                return true;

            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"Found language tracks that need to be removed : \"{TargetFile.Name}\"");
            keep.WriteLine("Keep");
            remove.WriteLine("Remove");

            // ReMux and only keep the specified tracks
            // Convert will test for Options.TestNoModify
            if (!Convert.ReMuxToMkv(TargetFile.FullName, keep, out string outputname))
            {                
                // Error
                Result = false;
                return false;
            }

            // Refresh
            modified = true;
            TargetFile = new FileInfo(outputname);
            return Refresh();
        }

        public bool RemoveDuplicateTracks(List<string> preferredAudioFormats, ref bool modified)
        {
            // Init
            Result = true;

            // Optional
            if (!Process.Options.RemoveDuplicateTracks)
                return true;

            // Find duplicates
            if (!MkvMergeInfo.FindDuplicateTracks(preferredAudioFormats, out MediaInfo keep, out MediaInfo remove))
                // Nothing to do
                return true;

            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"Found duplicate tracks to be removed : \"{TargetFile.Name}\"");
            keep.WriteLine("Keep");
            remove.WriteLine("Remove");

            // ReMux and only keep the specified tracks
            // Convert will test for Options.TestNoModify
            if (!Convert.ReMuxToMkv(TargetFile.FullName, keep, out string outputname))
            {
                // Error
                Result = false;
                return false;
            }

            // Refresh
            modified = true;
            TargetFile = new FileInfo(outputname);
            return Refresh();
        }

        public bool RemuxTracks(ref bool modified)
        {
            // Init
            Result = true;

            // Optional
            if (!Process.Options.ReMux)
                return true;

            // Find tracks that need remuxing
            if (!MediaInfoInfo.FindNeedReMux(out MediaInfo keep, out MediaInfo remux))
                // Nothing to do
                return true;

            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"Found tracks that need to be re-muxed : \"{TargetFile.Name}\"");
            keep.WriteLine("Keep");
            remux.WriteLine("ReMux");

            // ReMux all tracks, not possible to remux only some tracks
            // Convert will test for Options.TestNoModify
            if (!Convert.ReMuxToMkv(TargetFile.FullName, out string outputname))
            {
                // Error
                Result = false;
                return false;
            }

            // Refresh
            modified = true;
            TargetFile = new FileInfo(outputname);
            return Refresh();
        }

        public bool DeInterlace(ref bool modified)
        {
            // Init
            Result = true;

            // Optional
            if (!Process.Options.DeInterlace)
                return true;

            // Find all the tracks that need deinterlacing
            // Use MediaInfo ScanType field, but it is not reliable
            // FFmpeg with idet and frame counting is more reliable, but too slow
            // TODO : Store FFmpeg idet info in sidecar file
            if (!MediaInfoInfo.FindNeedDeInterlace(out MediaInfo keep, out MediaInfo deinterlace))
                // Nothing to do
                return true;

            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"Found interlaced video to be de-interlaced : \"{TargetFile.Name}\"");
            keep.WriteLine("Keep");
            deinterlace.WriteLine("DeInterlace");

            // Verify with FFmpeg idet frame counts
            if (!FfMpegTool.IsFileInterlaced(TargetFile.FullName, out bool interlaced))
            {
                // Error
                Result = false;
                return false;
            }
            if (!interlaced)
                Program.LogFile.LogConsole($"Warning : FFmpeg reports file is not interlaced : \"{TargetFile.Name}\"");

            // Convert using HandBrakeCLI, it produces the best de-interlacing results
            // Convert will test for Options.TestNoModify
            if (!Convert.DeInterlaceToMkv(TargetFile.FullName, out string outputname))
            {
                // Error
                Result = false;
                return false;
            }

            // Refresh
            modified = true;
            TargetFile = new FileInfo(outputname);
            return Refresh();
        }

        public bool ReEncode(List<VideoInfo> reencodeVideoInfos, HashSet<string> reencodeAudioFormats, ref bool modified)
        {
            // Init
            Result = true;

            // Optional
            if (!Process.Options.ReEncode)
                return true;

            // Find all tracks that need reencoding
            if (!MediaInfoInfo.FindNeedReEncode(reencodeVideoInfos, reencodeAudioFormats, out MediaInfo keep, out MediaInfo reencode))
                // Nothing to do
                return true;

            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"Found tracks that need to be re-encoded : \"{TargetFile.Name}\"");
            keep.WriteLine("Passthrough");
            reencode.WriteLine("ReEncode");

            // Reencode selected tracks
            // Convert will test for Options.TestNoModify
            if (!Convert.ConvertToMkv(TargetFile.FullName, keep, reencode, out string outputname))
            {
                // Error
                Result = false;
                return false;
            }

            // Refresh
            modified = true;
            TargetFile = new FileInfo(outputname);
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
            Program.LogFile.LogConsole($"File missing required tracks : \"{TargetFile.Name}\"");
            MediaInfoInfo.WriteLine("Invalid");

            // Delete the file
            if (!Process.Options.TestNoModify &&
                !FileEx.DeleteFile(TargetFile.FullName))
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
            Program.LogFile.LogConsole($"Warning : File play duration is less than 5 minutes : \"{TargetFile.Name}\"");
            MediaInfoInfo.WriteLine("Invalid");

            // Experimental, need more testing and configuration, just log for now
            return true;
            /*
            // Delete the file
            if (!Process.Options.TestNoModify &&
                !FileEx.DeleteFile(TargetFile.FullName))
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
            // Get new media info
            Result = GetMediaInfo();

            // Call Refresh() at processing function exit
            return Result;
        }

        public bool GetMediaInfo()
        {
            return Process.Options.UseSidecarFiles ?
                SidecarFile.GetMediaInfo(TargetFile, false, out FfProbeInfo, out MkvMergeInfo, out MediaInfoInfo) :
                MediaInfo.GetMediaInfo(TargetFile, out FfProbeInfo, out MkvMergeInfo, out MediaInfoInfo);
        }

        public void MonitorFileTime(int seconds)
        {
            TargetFile.Refresh();
            DateTime fileTime = TargetFile.LastWriteTimeUtc;
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"Warning : MonitorFileTime : {fileTime} : \"{TargetFile.Name}\"");
            for (int i = 0; i < seconds; i ++)
            {
                if (Program.Cancel.WaitForSet(1000))
                    break;
                TargetFile.Refresh();
                if (TargetFile.LastWriteTimeUtc != fileTime)
                    Program.LogFile.LogConsole($"Warning : MonitorFileTime : {TargetFile.LastWriteTimeUtc} != {fileTime} : \"{TargetFile.Name}\"");
                fileTime = TargetFile.LastWriteTimeUtc;
            }
            Program.LogFile.LogConsole("");
        }

        public bool Result { get; set; }

        private FileInfo TargetFile;
        private MediaInfo FfProbeInfo;
        private MediaInfo MkvMergeInfo;
        private MediaInfo MediaInfoInfo;
    }
}
