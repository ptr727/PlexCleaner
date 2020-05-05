using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using InsaneGenius.Utilities;

namespace PlexCleaner
{
    internal class Process
    {
        public Process()
        {
            // TODO : Add cleanup for extra empty entry when string is empty
            // extensionlist = extensionlist.Where(s => !String.IsNullOrWhiteSpace(s)).Distinct().ToList();

            // Wanted extensions, always keep .mkv and sidecar files
            // TODO : Add support for ignoring FUSE files e.g. .fuse_hidden191817c5000c5ee7, will need wildcard support
            List<string> stringlist = Program.Config.ProcessOptions.KeepExtensions.Split(',').ToList();
            KeepExtensions = new HashSet<string>(stringlist, StringComparer.OrdinalIgnoreCase)
            {
                ".mkv",
                SidecarFile.SidecarExtension
            };

            // Containers types that can be remuxed to MKV
            stringlist = Program.Config.ProcessOptions.ReMuxExtensions.Split(',').ToList();
            ReMuxExtensions = new HashSet<string>(stringlist, StringComparer.OrdinalIgnoreCase);

            // Languages are in short form using ISO 639-2 notation
            // https://www.loc.gov/standards/iso639-2/php/code_list.php
            // zxx = no linguistic content, und = undetermined
            // Default language
            if (string.IsNullOrEmpty(Program.Config.ProcessOptions.DefaultLanguage))
                Program.Config.ProcessOptions.DefaultLanguage = "eng";

            // Languages to keep, always keep no linguistic content and the default language
            // The languages must be in ISO 639-2 form
            stringlist = Program.Config.ProcessOptions.KeepLanguages.Split(',').ToList();
            KeepLanguages = new HashSet<string>(stringlist, StringComparer.OrdinalIgnoreCase)
            {
                "zxx",
                Program.Config.ProcessOptions.DefaultLanguage
            };

            // Re-encode any video track that match the list
            // We use ffmpeg to re-encode, so we use ffprobe formats
            // All other formats will be encoded to h264
            List<string> codeclist = Program.Config.ProcessOptions.ReEncodeVideoCodecs.Split(',').ToList();
            List<string> formatlist = Program.Config.ProcessOptions.ReEncodeVideoFormats.Split(',').ToList();
            List<string> profilelist = Program.Config.ProcessOptions.ReEncodeVideoProfiles.Split(',').ToList();
            Debug.Assert(codeclist.Count == formatlist.Count && formatlist.Count == profilelist.Count);
            ReEncodeVideoInfos = new List<VideoInfo>();
            for (int i = 0; i < codeclist.Count; i++)
            {
                // We match against the format and profile
                // Match the logic in VideoInfo.CompareVideo
                VideoInfo videoinfo = new VideoInfo
                {
                    Codec = codeclist.ElementAt(i),
                    Format = formatlist.ElementAt(i),
                    Profile = profilelist.ElementAt(i)
                };
                ReEncodeVideoInfos.Add(videoinfo);
            }

            // Re-encode any audio track that match the list
            // We use ffmpeg to re-encode, so we use ffprobe formats
            // All other formats will be encoded to the default codec, e.g. ac3
            stringlist = Program.Config.ProcessOptions.ReEncodeAudioFormats.Split(',').ToList();
            ReEncodeAudioFormats = new HashSet<string>(stringlist, StringComparer.OrdinalIgnoreCase);

            // Preferred audio codecs
            PreferredAudioFormats = Program.Config.ProcessOptions.PreferredAudioFormats.Split(',').ToList();
        }

        public bool ProcessFiles(List<FileInfo> fileList)
        {
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"Processing {fileList.Count} files ...");
            Program.LogFile.LogConsole("");

            // Start the stopwatch
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Process all files
            int totalcount = fileList.Count;
            int processedcount = 0;
            int errorcount = 0;
            int modifiedcount = 0;
            foreach (FileInfo fileinfo in fileList)
            {
                // Percentage
                processedcount ++;
                double done = System.Convert.ToDouble(processedcount) / System.Convert.ToDouble(totalcount);

                // Process the file
                ConsoleEx.WriteLine($"Processing ({done:P}) : \"{fileinfo.FullName}\"");
                if (!ProcessFile(fileinfo, out bool modified))
                {
                    Program.LogFile.LogConsole("");
                    Program.LogFile.LogConsoleError($"Error processing : \"{fileinfo.FullName}\"");
                    errorcount ++;
                }
                else if (modified)
                    modifiedcount ++;

                // Cancel handler
                if (Program.Cancel.State)
                    return false;

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"Total files : {fileList.Count}");
            Program.LogFile.LogConsole($"Modified files : {modifiedcount}");
            Program.LogFile.LogConsole($"Error files : {errorcount}");
            Program.LogFile.LogConsole($"Processing time : {timer.Elapsed}");
            Program.LogFile.LogConsole("");

            return true;
        }

        public bool ProcessFolders(List<string> folderList)
        {
            // Create the file and directory list
            // Process the files
            return FileEx.EnumerateDirectories(folderList, out List<FileInfo> fileList, out _) && ProcessFiles(fileList);
        }

        public static bool DeleteEmptyFolders(List<string> folderList)
        {
            if (!Program.Config.ProcessOptions.DeleteEmptyFolders)
                return true;

            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole("Deleting empty folders ...");
            Program.LogFile.LogConsole("");

            // Delete all empty folders
            int deleted = 0;
            foreach (string folder in folderList)
            {
                ConsoleEx.WriteLine($"Looking for empty folders in \"{folder}\"");
                FileEx.DeleteEmptyDirectories(folder, ref deleted);
            }

            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"Deleted folders : {deleted}");
            Program.LogFile.LogConsole("");

            return true;
        }

        public bool ReMuxFiles(List<FileInfo> fileList)
        {
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole("ReMuxing files ...");
            Program.LogFile.LogConsole("");

            // Start the stopwatch
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Process all files
            int errorcount = 0;
            foreach (FileInfo fileinfo in fileList)
            {
                // Cancel handler
                if (Program.Cancel.State)
                    return false;

                // Handle only MKV files, and files in the remux extension list
                if (!MkvTool.IsMkvFile(fileinfo) &&
                    !ReMuxExtensions.Contains(fileinfo.Extension))
                    continue;

                // ReMux the file
                Program.LogFile.LogConsole($"ReMuxing : \"{fileinfo.FullName}\"");
                if (!Convert.ReMuxToMkv(fileinfo.FullName, out string _))
                {
                    Program.LogFile.LogConsole("");
                    Program.LogFile.LogConsoleError($"Error ReMuxing : \"{fileinfo.FullName}\"");
                    errorcount ++;
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"Total files : {fileList.Count}");
            Program.LogFile.LogConsole($"Error files : {errorcount}");
            Program.LogFile.LogConsole($"Processing time : {timer.Elapsed}");
            Program.LogFile.LogConsole("");

            return true;
        }

        public bool VerifyFiles(List<FileInfo> fileList)
        {
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole("Verifying files ...");
            Program.LogFile.LogConsole("");

            // Start the stopwatch
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Process all files
            int errorcount = 0;
            foreach (FileInfo fileinfo in fileList)
            {
                // Cancel handler
                if (Program.Cancel.State)
                    return false;

                // Handle only MKV files
                if (!MkvTool.IsMkvFile(fileinfo))
                    continue;

                // Process the file
                Program.LogFile.LogConsole($"Verifying : \"{fileinfo.FullName}\"");
                if (!FfMpegTool.VerifyMedia(fileinfo.FullName, out string error))
                {
                    Program.LogFile.LogConsole("");
                    Program.LogFile.LogConsoleError($"Error Verifying : \"{fileinfo.FullName}\"");
                    Program.LogFile.LogConsole(error);
                    errorcount ++;
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"Total files : {fileList.Count}");
            Program.LogFile.LogConsole($"Error files : {errorcount}");
            Program.LogFile.LogConsole($"Processing time : {timer.Elapsed}");
            Program.LogFile.LogConsole("");

            return true;
        }

        public bool ReEncodeFiles(List<FileInfo> fileList)
        {
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole("ReEncoding files ...");
            Program.LogFile.LogConsole("");

            // Start the stopwatch
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Process all files
            int errorcount = 0;
            foreach (FileInfo fileinfo in fileList)
            {
                // Cancel handler
                if (Program.Cancel.State)
                    return false;

                // Handle only MKV files
                // ReMux before re-encode, so the track attribute logic works as expected
                if (!MkvTool.IsMkvFile(fileinfo))
                    continue;

                // ReEncode the file
                Program.LogFile.LogConsole($"ReEncoding : \"{fileinfo.FullName}\"");
                if (!Convert.ConvertToMkv(fileinfo.FullName, out string _))
                {
                    Program.LogFile.LogConsole("");
                    Program.LogFile.LogConsoleError($"Error ReEncoding : \"{fileinfo.FullName}\"");
                    errorcount ++;
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"Total files : {fileList.Count}");
            Program.LogFile.LogConsole($"Error files : {errorcount}");
            Program.LogFile.LogConsole($"Processing time : {timer.Elapsed}");
            Program.LogFile.LogConsole("");

            return true;
        }

        public bool DeInterlaceFiles(List<FileInfo> fileList)
        {
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole("DeInterlacing files ...");
            Program.LogFile.LogConsole("");

            // Start the stopwatch
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Process all files
            int errorcount = 0;
            foreach (FileInfo fileinfo in fileList)
            {
                // Cancel handler
                if (Program.Cancel.State)
                    return false;

                // Handle only MKV files
                if (!MkvTool.IsMkvFile(fileinfo))
                    continue;

                // De-interlace the file
                Program.LogFile.LogConsole($"DeInterlacing : \"{fileinfo.FullName}\"");
                if (!Convert.DeInterlaceToMkv(fileinfo.FullName, out string _))
                {
                    Program.LogFile.LogConsole("");
                    Program.LogFile.LogConsoleError($"Error DeInterlacing : \"{fileinfo.FullName}\"");
                    errorcount ++;
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"Total files : {fileList.Count}");
            Program.LogFile.LogConsole($"Error files : {errorcount}");
            Program.LogFile.LogConsole($"Processing time : {timer.Elapsed}");
            Program.LogFile.LogConsole("");

            return true;
        }

        public bool CreateTagMapFiles(List<FileInfo> fileList)
        {
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole("Creating tag map ...");
            Program.LogFile.LogConsole("");

            // Start the stopwatch
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // We want to create a dictionary of ffprobe to mkvmerge and mediainfo tag strings
            // And how they map to each other for the same media file
            TagMapDictionary fftags = new TagMapDictionary();
            TagMapDictionary mktags = new TagMapDictionary();
            TagMapDictionary mitags = new TagMapDictionary();

            // Process all files
            int errorcount = 0;
            foreach (FileInfo fileinfo in fileList)
            {
                // Cancel handler
                if (Program.Cancel.State)
                    return false;

                // Handle only MKV files
                if (!MkvTool.IsMkvFile(fileinfo))
                    continue;

                // Use ProcessFile to get media info
                ProcessFile processFile = new ProcessFile(fileinfo);
                Program.LogFile.LogConsoleError($"Getting media info : \"{fileinfo.FullName}\"");
                if (!processFile.GetMediaInfo())
                {
                    Program.LogFile.LogConsole("");
                    Program.LogFile.LogConsoleError($"Error getting media info : \"{fileinfo.FullName}\"");
                    errorcount ++;

                    // Next file
                    continue;
                }

                // Add all the tags
                fftags.Add(processFile.FfProbeInfo, processFile.MkvMergeInfo, processFile.MediaInfoInfo);
                mktags.Add(processFile.MkvMergeInfo, processFile.FfProbeInfo, processFile.MediaInfoInfo);
                mitags.Add(processFile.MediaInfoInfo, processFile.FfProbeInfo, processFile.MkvMergeInfo);

                // Next file
            }

            // Print the results
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole("FFprobe:");
            fftags.WriteLine();
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole("MKVMerge:");
            mktags.WriteLine();
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole("MediaInfo:");
            mitags.WriteLine();
            Program.LogFile.LogConsole("");

            // Stop the timer
            timer.Stop();

            // Done
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"Total files : {fileList.Count}");
            Program.LogFile.LogConsole($"Error files : {errorcount}");
            Program.LogFile.LogConsole($"Processing time : {timer.Elapsed}");
            Program.LogFile.LogConsole("");

            return true;
        }

        public bool WriteSidecarFiles(List<FileInfo> fileList)
        {
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole("Writing sidecar files ...");
            Program.LogFile.LogConsole("");

            // Start the stopwatch
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Process all files
            int errorcount = 0;
            foreach (FileInfo fileinfo in fileList)
            {
                // Cancel handler
                if (Program.Cancel.State)
                    return false;

                // Handle only MKV files
                if (!MkvTool.IsMkvFile(fileinfo))
                    continue;

                // Write the sidecar files
                Program.LogFile.LogConsoleError($"Creating sidecar : \"{fileinfo.FullName}\"");
                if (!SidecarFile.CreateSidecarFile(fileinfo))
                {
                    Program.LogFile.LogConsole("");
                    Program.LogFile.LogConsoleError($"Error creating sidecar : \"{fileinfo.FullName}\"");
                    errorcount ++;
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"Total files : {fileList.Count}");
            Program.LogFile.LogConsole($"Error files : {errorcount}");
            Program.LogFile.LogConsole($"Processing time : {timer.Elapsed}");
            Program.LogFile.LogConsole("");

            return true;
        }

        public bool PrintInfo(List<FileInfo> fileList)
        {
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole("Printing file information ...");
            Program.LogFile.LogConsole("");

            // Start the stopwatch
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Process all files
            int errorcount = 0;
            foreach (FileInfo fileinfo in fileList)
            {
                // Cancel handler
                if (Program.Cancel.State)
                    return false;

                // Handle only MKV files
                if (!MkvTool.IsMkvFile(fileinfo))
                    continue;

                // Process the file
                ProcessFile processFile = new ProcessFile(fileinfo);
                Program.LogFile.LogConsoleError($"Getting media info : \"{fileinfo.FullName}\"");
                if (!processFile.GetMediaInfo())
                {
                    Program.LogFile.LogConsole("");
                    Program.LogFile.LogConsoleError($"Error getting media information : \"{fileinfo.FullName}\"");
                    errorcount ++;
                }
                else
                {
                    Program.LogFile.LogConsole("");
                    Program.LogFile.LogConsole(fileinfo.FullName);
                    processFile.FfProbeInfo.WriteLine("FFprobe");
                    processFile.MkvMergeInfo.WriteLine("MKVMerge");
                    processFile.MediaInfoInfo.WriteLine("MediaInfo");
                    Program.LogFile.LogConsole("");
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            Program.LogFile.LogConsole("");
            Program.LogFile.LogConsole($"Total files : {fileList.Count}");
            Program.LogFile.LogConsole($"Error files : {errorcount}");
            Program.LogFile.LogConsole($"Processing time : {timer.Elapsed}");
            Program.LogFile.LogConsole("");

            return true;
        }

        private bool ProcessFile(FileInfo fileinfo, out bool modified)
        {
            // Init
            modified = false;

            // Does the file still exist?
            if (!File.Exists(fileinfo.FullName))
                // Nothing to do
                return true;

            // Create file processor to hold state
            ProcessFile processFile = new ProcessFile(fileinfo);

            // Delete files not in our desired extensions lists
            if (!processFile.DeleteUnwantedExtensions(KeepExtensions, ReMuxExtensions, ref modified))
                return processFile.Result;

            // Delete the sidecar file if matching MKV file not found
            if (!processFile.DeleteMissingSidecarFiles(ref modified))
                return processFile.Result;

            // Nothing more to do for files in the keep extensions list
            // Except if it is a MKV file or a file to be remuxed
            if (!MkvTool.IsMkvFile(fileinfo) &&
                !ReMuxExtensions.Contains(fileinfo.Extension) &&
                KeepExtensions.Contains(fileinfo.Extension))
                return true;

            // ReMux undesirable containers matched by extension
            if (!processFile.RemuxByExtensions(ReMuxExtensions, ref modified))
                return processFile.Result;

            // Read the media info
            if (!processFile.GetMediaInfo())
                return processFile.Result;

            // Do we have any errors in the metadata
            processFile.MediaInfoErrors();

            // Remove tags
            // This may fix some of the FFprobe language tag errors
            if (!processFile.RemoveTags(ref modified))
                return processFile.Result;

            // Change all tracks with an unknown language to the default language
            if (!processFile.SetUnknownLanguage(ref modified))
                return processFile.Result;

            // Merge all remux operations into a single call
            // Remove all the unwanted language tracks
            // Remove all duplicate tracks
            // Remux if any tracks specifically need remuxing
            if (!processFile.ReMux(KeepLanguages, PreferredAudioFormats, ref modified))
                return processFile.Result;

            // De-interlace interlaced content
            if (!processFile.DeInterlace(ref modified))
                return processFile.Result;

            // Re-Encode formats that cannot be direct-played, e.g. MPEG2, WMAPro
            if (!processFile.ReEncode(ReEncodeVideoInfos, ReEncodeAudioFormats, ref modified))
                return processFile.Result;

            // FFmpeg and HandBrake can add tags or result in tracks witn no language set
            // Remove tags and set unknown languages again
            if (!processFile.RemoveTags(ref modified))
                return processFile.Result;
            if (!processFile.SetUnknownLanguage(ref modified))
                return processFile.Result;

            // Verify media
            if (!processFile.Verify(ref modified))
                return processFile.Result;

            // TODO : Why does the file timestamp change after writing sidecar files?
            // https://forums.unraid.net/topic/91800-file-modification-time-changes-after-last-write/
            //if (modified)
            //    processFile.MonitorFileTime(30);

            // Done
            return true;
        }

        private readonly HashSet<string> KeepExtensions;
        private readonly HashSet<string> ReMuxExtensions;
        private readonly HashSet<string> ReEncodeAudioFormats;
        private readonly HashSet<string> KeepLanguages;
        private readonly List<string> PreferredAudioFormats;
        private readonly List<VideoInfo> ReEncodeVideoInfos;
    }
}
