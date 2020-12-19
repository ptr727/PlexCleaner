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

            // File ignore list
            IgnoreList = new HashSet<string>(Program.Config.ProcessOptions.FileIgnoreList, StringComparer.OrdinalIgnoreCase);
        }

        public bool ProcessFiles(List<FileInfo> fileList)
        {
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole($"Processing {fileList.Count} files ...");

            // Keep a List of failed and modified files to print when done
            // This makes followup easier vs. looking the logs
            List<string> modifiedFiles = new List<string>();
            List<string> errorFiles = new List<string>();

            // Start the stopwatch
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Process all files
            int totalCount = fileList.Count;
            int processedCount = 0;
            int errorCount = 0;
            int modifiedCount = 0;
            foreach (FileInfo fileInfo in fileList)
            {
                // Prevent sleep
                KeepAwake.PreventSleep();

                // Percentage
                processedCount ++;
                double done = System.Convert.ToDouble(processedCount) / System.Convert.ToDouble(totalCount);

                // Process the file
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLine($"Processing ({done:P}) : \"{fileInfo.FullName}\"");
                if (!ProcessFile(fileInfo, out bool modified))
                {
                    ConsoleEx.WriteLine("");
                    Program.LogFile.LogConsoleError($"Error processing : \"{fileInfo.FullName}\"");
                    errorFiles.Add(fileInfo.FullName);
                    errorCount ++;
                }
                else if (modified)
                {
                    modifiedFiles.Add(fileInfo.FullName);
                    modifiedCount ++;
                }

                // Cancel handler
                if (Program.IsCancelled())
                    return false;

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole($"Total files : {fileList.Count}");
            Program.LogFile.LogConsole($"Modified files : {modifiedCount}");
            Program.LogFile.LogConsole($"Error files : {errorCount}");
            Program.LogFile.LogConsole($"Processing time : {timer.Elapsed}");

            // Print summary of failed and modified files
            if (modifiedFiles.Count > 0)
            {
                ConsoleEx.WriteLine("");
                Program.LogFile.LogConsole("Modified files :");
                foreach (string file in modifiedFiles)
                {
                    Program.LogFile.LogConsole(file);
                }
            }
            if (errorFiles.Count > 0)
            { 
                ConsoleEx.WriteLine("");
                Program.LogFile.LogConsole("Error files :");
                foreach (string file in errorFiles)
                { 
                    Program.LogFile.LogConsole(file);
                }
            }
            
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

            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole("Deleting empty folders ...");

            // Delete all empty folders
            int deleted = 0;
            foreach (string folder in folderList)
            {
                ConsoleEx.WriteLine("");
                ConsoleEx.WriteLine($"Looking for empty folders in \"{folder}\"");
                FileEx.DeleteEmptyDirectories(folder, ref deleted);
            }

            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole($"Deleted folders : {deleted}");

            return true;
        }

        public bool ReMuxFiles(List<FileInfo> fileList)
        {
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole("ReMuxing files ...");

            // Start the stopwatch
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Process all files
            int errorcount = 0;
            foreach (FileInfo fileinfo in fileList)
            {
                // Cancel handler
                if (Program.IsCancelled())
                    return false;

                // Handle only MKV files, and files in the remux extension list
                if (!MkvMergeTool.IsMkvFile(fileinfo) &&
                    !ReMuxExtensions.Contains(fileinfo.Extension))
                    continue;

                // ReMux the file
                ConsoleEx.WriteLine("");
                Program.LogFile.LogConsole($"ReMuxing : \"{fileinfo.FullName}\"");
                if (!Convert.ReMuxToMkv(fileinfo.FullName, out string _))
                {
                    ConsoleEx.WriteLine("");
                    Program.LogFile.LogConsoleError($"Error ReMuxing : \"{fileinfo.FullName}\"");
                    errorcount ++;
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole($"Total files : {fileList.Count}");
            Program.LogFile.LogConsole($"Error files : {errorcount}");
            Program.LogFile.LogConsole($"Processing time : {timer.Elapsed}");

            return true;
        }

        public static bool VerifyFiles(List<FileInfo> fileList)
        {
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole("Verifying files ...");

            // Start the stopwatch
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Process all files
            int errorcount = 0;
            foreach (FileInfo fileinfo in fileList)
            {
                // Cancel handler
                if (Program.IsCancelled())
                    return false;

                // Handle only MKV files
                if (!MkvMergeTool.IsMkvFile(fileinfo))
                    continue;

                // Process the file
                // TODO : Consolidate the logic with ProcessFile.Verify()
                ConsoleEx.WriteLine("");
                Program.LogFile.LogConsole($"Verifying : \"{fileinfo.FullName}\"");
                if (!Tools.FfMpeg.VerifyMedia(fileinfo.FullName, out string error))
                {
                    ConsoleEx.WriteLine("");
                    Program.LogFile.LogConsoleError($"Error Verifying : \"{fileinfo.FullName}\"");
                    Program.LogFile.LogConsole(error);
                    errorcount++;
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole($"Total files : {fileList.Count}");
            Program.LogFile.LogConsole($"Error files : {errorcount}");
            Program.LogFile.LogConsole($"Processing time : {timer.Elapsed}");

            return true;
        }

        public static bool ReEncodeFiles(List<FileInfo> fileList)
        {
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole("ReEncoding files ...");

            // Start the stopwatch
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Process all files
            int errorcount = 0;
            foreach (FileInfo fileinfo in fileList)
            {
                // Cancel handler
                if (Program.IsCancelled())
                    return false;

                // Handle only MKV files
                // ReMux before re-encode, so the track attribute logic works as expected
                if (!MkvMergeTool.IsMkvFile(fileinfo))
                    continue;

                // ReEncode the file
                ConsoleEx.WriteLine("");
                Program.LogFile.LogConsole($"ReEncoding : \"{fileinfo.FullName}\"");
                if (!Convert.ConvertToMkv(fileinfo.FullName, out string _))
                {
                    ConsoleEx.WriteLine("");
                    Program.LogFile.LogConsoleError($"Error ReEncoding : \"{fileinfo.FullName}\"");
                    errorcount++;
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole($"Total files : {fileList.Count}");
            Program.LogFile.LogConsole($"Error files : {errorcount}");
            Program.LogFile.LogConsole($"Processing time : {timer.Elapsed}");

            return true;
        }

        public static bool DeInterlaceFiles(List<FileInfo> fileList)
        {
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole("DeInterlacing files ...");

            // Start the stopwatch
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Process all files
            int errorcount = 0;
            foreach (FileInfo fileinfo in fileList)
            {
                // Cancel handler
                if (Program.IsCancelled())
                    return false;

                // Handle only MKV files
                if (!MkvMergeTool.IsMkvFile(fileinfo))
                    continue;

                // De-interlace the file
                ConsoleEx.WriteLine("");
                Program.LogFile.LogConsole($"DeInterlacing : \"{fileinfo.FullName}\"");
                if (!Convert.DeInterlaceToMkv(fileinfo.FullName, out string _))
                {
                    ConsoleEx.WriteLine("");
                    Program.LogFile.LogConsoleError($"Error DeInterlacing : \"{fileinfo.FullName}\"");
                    errorcount++;
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole($"Total files : {fileList.Count}");
            Program.LogFile.LogConsole($"Error files : {errorcount}");
            Program.LogFile.LogConsole($"Processing time : {timer.Elapsed}");

            return true;
        }

        public static bool GetTagMapFiles(List<FileInfo> fileList)
        {
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole("Creating tag map ...");

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
                if (Program.IsCancelled())
                    return false;

                // Handle only MKV files
                if (!MkvMergeTool.IsMkvFile(fileinfo))
                    continue;

                // Use ProcessFile to get media info
                ConsoleEx.WriteLine("");
                Program.LogFile.LogConsole($"Getting media info : \"{fileinfo.FullName}\"");
                ProcessFile processFile = new ProcessFile(fileinfo);
                if (!processFile.GetMediaInfo())
                {
                    ConsoleEx.WriteLine("");
                    Program.LogFile.LogConsoleError($"Error getting media info : \"{fileinfo.FullName}\"");
                    errorcount++;

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
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole("FFprobe:");
            fftags.WriteLine();
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole("MKVMerge:");
            mktags.WriteLine();
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole("MediaInfo:");
            mitags.WriteLine();

            // Stop the timer
            timer.Stop();

            // Done
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole($"Total files : {fileList.Count}");
            Program.LogFile.LogConsole($"Error files : {errorcount}");
            Program.LogFile.LogConsole($"Processing time : {timer.Elapsed}");

            return true;
        }

        public static bool CreateSidecarFiles(List<FileInfo> fileList)
        {
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole("Creating sidecar files ...");

            // Start the stopwatch
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Process all files
            int errorcount = 0;
            foreach (FileInfo fileinfo in fileList)
            {
                // Cancel handler
                if (Program.IsCancelled())
                    return false;

                // Handle only MKV files
                if (!MkvMergeTool.IsMkvFile(fileinfo))
                    continue;

                // Write the sidecar files
                ConsoleEx.WriteLine("");
                Program.LogFile.LogConsole($"Creating sidecar : \"{fileinfo.FullName}\"");
                if (!SidecarFile.CreateSidecarFile(fileinfo))
                {
                    ConsoleEx.WriteLine("");
                    Program.LogFile.LogConsoleError($"Error creating sidecar : \"{fileinfo.FullName}\"");
                    errorcount++;
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole($"Total files : {fileList.Count}");
            Program.LogFile.LogConsole($"Error files : {errorcount}");
            Program.LogFile.LogConsole($"Processing time : {timer.Elapsed}");

            return true;
        }

        public static bool GetSidecarFiles(List<FileInfo> fileList)
        {
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole("Reading sidecar files ...");

            // Start the stopwatch
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Process all files
            int errorcount = 0;
            foreach (FileInfo fileinfo in fileList)
            {
                // Cancel handler
                if (Program.IsCancelled())
                    return false;

                // Handle only sidecar files
                if (!SidecarFile.IsSidecarFile(fileinfo))
                    continue;

                // Read the sidecar files
                ConsoleEx.WriteLine("");
                Program.LogFile.LogConsole($"Reading sidecar : \"{fileinfo.FullName}\"");
                SidecarFile sidecarfile = new SidecarFile();
                if (!sidecarfile.ReadSidecarJson(fileinfo))
                {
                    ConsoleEx.WriteLine("");
                    Program.LogFile.LogConsoleError($"Error reading sidecar : \"{fileinfo.FullName}\"");
                    errorcount++;
                }
                else 
                {
                    ConsoleEx.WriteLine("");
                    ConsoleEx.WriteLine(sidecarfile.ToString());
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole($"Total files : {fileList.Count}");
            Program.LogFile.LogConsole($"Error files : {errorcount}");
            Program.LogFile.LogConsole($"Processing time : {timer.Elapsed}");

            return true;
        }

        public static bool GetMediaInfoFiles(List<FileInfo> fileList)
        {
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole("Getting media information ...");

            // Start the stopwatch
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Process all files
            int errorcount = 0;
            foreach (FileInfo fileinfo in fileList)
            {
                // Cancel handler
                if (Program.IsCancelled())
                    return false;

                // Handle only MKV files
                if (!MkvMergeTool.IsMkvFile(fileinfo))
                    continue;

                // Process the file
                ConsoleEx.WriteLine("");
                Program.LogFile.LogConsole($"Getting media information : \"{fileinfo.FullName}\"");
                ProcessFile processFile = new ProcessFile(fileinfo);
                if (!processFile.GetMediaInfo())
                {
                    ConsoleEx.WriteLine("");
                    Program.LogFile.LogConsoleError($"Error getting media information : \"{fileinfo.FullName}\"");
                    errorcount++;
                }
                else
                {
                    ConsoleEx.WriteLine("");
                    Program.LogFile.LogConsole(fileinfo.FullName);
                    processFile.FfProbeInfo.WriteLine("FFprobe");
                    processFile.MkvMergeInfo.WriteLine("MKVMerge");
                    processFile.MediaInfoInfo.WriteLine("MediaInfo");
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole($"Total files : {fileList.Count}");
            Program.LogFile.LogConsole($"Error files : {errorcount}");
            Program.LogFile.LogConsole($"Processing time : {timer.Elapsed}");

            return true;
        }

        public static bool GetBitrateFiles(List<FileInfo> fileList)
        {
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole("Getting bitrate information ...");

            // Start the stopwatch
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Process all files
            int errorcount = 0;
            foreach (FileInfo fileinfo in fileList)
            {
                // Cancel handler
                if (Program.IsCancelled())
                    return false;

                // Handle only MKV files
                if (!MkvMergeTool.IsMkvFile(fileinfo))
                    continue;

                // Process the file
                ConsoleEx.WriteLine("");
                Program.LogFile.LogConsole($"Getting bitrate information : \"{fileinfo.FullName}\"");
                ProcessFile processFile = new ProcessFile(fileinfo);
                if (!processFile.GetBitrateInfo(out BitrateInfo bitrateInfo))
                {
                    ConsoleEx.WriteLine("");
                    Program.LogFile.LogConsoleError($"Error getting bitrate information : \"{fileinfo.FullName}\"");
                    errorcount++;
                }
                else
                {
                    ConsoleEx.WriteLine("");
                    Program.LogFile.LogConsole(fileinfo.FullName);
                    Program.LogFile.LogConsole(bitrateInfo.ToString());
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            ConsoleEx.WriteLine("");
            Program.LogFile.LogConsole($"Total files : {fileList.Count}");
            Program.LogFile.LogConsole($"Error files : {errorcount}");
            Program.LogFile.LogConsole($"Processing time : {timer.Elapsed}");

            return true;
        }

        private bool ProcessFile(FileInfo fileinfo, out bool modified)
        {
            // Init
            modified = false;

            // Skip the file if it is in the ignore list
            if (IgnoreList.Contains(fileinfo.FullName))
            {
                ConsoleEx.WriteLine("");
                Program.LogFile.LogConsole($"Warning : Skipping ignored file : \"{fileinfo.FullName}\"");
                return true;
            }

            // Does the file still exist
            if (!File.Exists(fileinfo.FullName))
            {
                ConsoleEx.WriteLine("");
                Program.LogFile.LogConsole($"Warning : Skipping missing file : \"{fileinfo.FullName}\"");
                return false;
            }

            // Is the file read-only
            if ((fileinfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                ConsoleEx.WriteLine("");
                Program.LogFile.LogConsole($"Warning : Skipping read-only file : \"{fileinfo.FullName}\" : {fileinfo.Attributes}");
                return false;
            }

            // Create file processor to hold state
            ProcessFile processFile = new ProcessFile(fileinfo);

            // Cancel handler
            if (Program.IsCancelled())
                return false;

            // Delete files not in our desired extensions lists
            if (!processFile.DeleteUnwantedExtensions(KeepExtensions, ReMuxExtensions, ref modified))
                return processFile.Result;

            // Cancel handler
            if (Program.IsCancelled())
                return false;

            // Delete the sidecar file if matching MKV file not found
            if (!processFile.DeleteMissingSidecarFiles(ref modified))
                return processFile.Result;

            // Cancel handler
            if (Program.IsCancelled())
                return false;

            // Nothing more to do for files in the keep extensions list
            // Except if it is a MKV file or a file to be remuxed to MKV
            if (!MkvMergeTool.IsMkvFile(fileinfo) &&
                !ReMuxExtensions.Contains(fileinfo.Extension) &&
                KeepExtensions.Contains(fileinfo.Extension))
                return true;

            // Cancel handler
            if (Program.IsCancelled())
                return false;

            // ReMux non-MKV containers matched by extension
            if (!processFile.RemuxByExtensions(ReMuxExtensions, ref modified))
                return processFile.Result;

            // Cancel handler
            if (Program.IsCancelled())
                return false;

            // Read the media info
            if (!processFile.GetMediaInfo())
                return processFile.Result;

            // ReMux non-MKV containers using MKV filenames
            if (!processFile.RemuxNonMkvContainer(ref modified))
                return processFile.Result;

            // Cancel handler
            if (Program.IsCancelled())
                return false;

            // Do we have any errors in the metadata
            // This will only print warnings
            processFile.MediaInfoErrors();

            // Cancel handler
            if (Program.IsCancelled())
                return false;

            // Remove tags
            // This may fix some of the FFprobe language tag errors
            if (!processFile.RemoveTags(ref modified))
                return processFile.Result;

            // Cancel handler
            if (Program.IsCancelled())
                return false;

            // Change all tracks with an unknown language to the default language
            if (!processFile.SetUnknownLanguage(ref modified))
                return processFile.Result;

            // Cancel handler
            if (Program.IsCancelled())
                return false;

            // Merge all remux operations into a single call
            // Remove all the unwanted language tracks
            // Remove all duplicate tracks
            // TODO: Remux if any tracks specifically need remuxing
            if (!processFile.ReMux(KeepLanguages, PreferredAudioFormats, ref modified))
                return processFile.Result;

            // Cancel handler
            if (Program.IsCancelled())
                return false;

            // De-interlace interlaced content
            if (!processFile.DeInterlace(ref modified))
                return processFile.Result;

            // Cancel handler
            if (Program.IsCancelled())
                return false;

            // Re-Encode formats that cannot be direct-played, e.g. MPEG2, WMAPro
            if (!processFile.ReEncode(ReEncodeVideoInfos, ReEncodeAudioFormats, ref modified))
                return processFile.Result;

            // Cancel handler
            if (Program.IsCancelled())
                return false;

            // Verify media
            if (!processFile.Verify(ref modified))
                return processFile.Result;

            // Cancel handler
            if (Program.IsCancelled())
                return false;

            // FFmpeg and HandBrake can add tags or result in tracks witn no language set
            // Remove tags and set unknown languages again
            // TODO: Can we avoid double processing?
            if (!processFile.RemoveTags(ref modified))
                return processFile.Result;
            if (!processFile.SetUnknownLanguage(ref modified))
                return processFile.Result;

            // Cancel handler
            if (Program.IsCancelled())
                return false;

            // Removing the tags and setting the unknown languages will invalidate verified
            // Re-verify media to remember verified flag
            // TODO: Can we avoid double processing?
            if (!processFile.Verify(ref modified))
                return processFile.Result;

            // Cancel handler
            if (Program.IsCancelled())
                return false;

            // TODO: Why does the media file timestamp change after processing?
            // Speculating there is caching between Windows and Samba and ZFS and timestamps are not synced?
            // https://docs.microsoft.com/en-us/dotnet/api/system.io.filesysteminfo.lastwritetimeutc
            // 10/4/2020 12:03:28 PM : Information : MonitorFileTime : 10/4/2020 7:02:55 PM : "Grand Designs Australia - S07E10 - Daylesford Long House, VIC.mkv"
            // 10/4/2020 12:10:44 PM : Warning : MonitorFileTime : 10/4/2020 7:03:24 PM != 10/4/2020 7:02:55 PM : "Grand Designs Australia - S07E10 - Daylesford Long House, VIC.mkv"
            // 10/4/2020 12:12:04 PM : Warning : MonitorFileTime : 10/4/2020 7:03:35 PM != 10/4/2020 7:03:24 PM : "Grand Designs Australia - S07E10 - Daylesford Long House, VIC.mkv"
            /*
            if (modified)
            {
                // Sleep for a few seconds
                const int sleepTime = 5;
                Thread.Sleep(sleepTime * 1000);

                // Force another sidecar refresh
                processFile.GetMediaInfo();

                // Test for timestamp changes
                Debug.Assert(!processFile.MonitorFileTime(60));
            }
            */

            // Done
            return true;
        }

        private readonly HashSet<string> IgnoreList;
        private readonly HashSet<string> KeepExtensions;
        private readonly HashSet<string> ReMuxExtensions;
        private readonly HashSet<string> ReEncodeAudioFormats;
        private readonly HashSet<string> KeepLanguages;
        private readonly List<string> PreferredAudioFormats;
        private readonly List<VideoInfo> ReEncodeVideoInfos;
    }
}
