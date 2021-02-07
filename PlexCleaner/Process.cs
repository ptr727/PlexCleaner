using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using InsaneGenius.Utilities;
using Serilog;

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
            Log.Logger.Information("Processing {Count} files ...", fileList.Count);

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
                // Percentage
                processedCount ++;
                double done = System.Convert.ToDouble(processedCount) / System.Convert.ToDouble(totalCount);

                // Process the file
                Log.Logger.Information("Processing ({Done:P}) : {Name}", done, fileInfo.FullName);
                if (!ProcessFile(fileInfo, out bool modified) &&
                    !Program.IsCancelled())
                {
                    Log.Logger.Error("Error processing : {Name}", fileInfo.FullName);
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
                    // Break, don't return, complete the cleanup logic
                    break;

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            Log.Logger.Information("Total files : {Count}", fileList.Count);
            Log.Logger.Information("Modified files : {Count}", modifiedCount);
            Log.Logger.Information("Error files : {Count}", errorCount);
            Log.Logger.Information("Processing time : {Elapsed}", timer.Elapsed);

            // Print summary of failed and modified files
            if (modifiedFiles.Count > 0)
            {
                Log.Logger.Information("Modified files :");
                foreach (string file in modifiedFiles)
                    Log.Logger.Information("{Name}", file);
            }
            if (errorFiles.Count > 0)
            {
                Log.Logger.Information("Error files :");
                foreach (string file in errorFiles)
                    Log.Logger.Information("{Name}", file);
            }

            // Write the updated ignore file list
            // Compare the item counts to know if modifications were made
            if (Program.Config.VerifyOptions.RegisterInvalidFiles &&
                Program.Config.ProcessOptions.FileIgnoreList.Count != IgnoreList.Count)
            {
                Log.Logger.Information("Updating settings file : {SettingsFile}", Program.Options.SettingsFile);
                Program.Config.ProcessOptions.FileIgnoreList.Sort();
                ConfigFileJsonSchema.ToFile(Program.Options.SettingsFile, Program.Config);
            }

            return !Program.IsCancelled();
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

            Log.Logger.Information("Deleting empty folders ...");

            // Delete all empty folders
            int deleted = 0;
            foreach (string folder in folderList)
            {
                Log.Logger.Information("Looking for empty folders in {Folder}", folder);
                FileEx.DeleteEmptyDirectories(folder, ref deleted);
            }

            Log.Logger.Information("Deleted folders : {Deleted}", deleted);

            return true;
        }

        public bool ReMuxFiles(List<FileInfo> fileList)
        {
            Log.Logger.Information("ReMuxing files ...");

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
                Log.Logger.Information("ReMuxing : {Name}", fileinfo.FullName);
                if (!Convert.ReMuxToMkv(fileinfo.FullName, out string _))
                {
                    Log.Logger.Error("Error ReMuxing : {Name}", fileinfo.FullName);
                    errorcount ++;
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            Log.Logger.Information("Total files : {Count}", fileList.Count);
            Log.Logger.Information("Error files : {Count}", errorcount);
            Log.Logger.Information("Processing time : {Elapsed}", timer.Elapsed);

            return true;
        }

        public static bool VerifyFiles(List<FileInfo> fileList)
        {
            Log.Logger.Information("Verifying files ...");

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
                Log.Logger.Information("Verifying : {Name}", fileinfo.FullName);
                if (!Tools.FfMpeg.VerifyMedia(fileinfo.FullName, out string error))
                {
                    Log.Logger.Error("Error Verifying : {Name}", fileinfo.FullName);
                    Log.Logger.Error("{Error}", error);
                    errorcount ++;
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            Log.Logger.Information("Total files : {Count}", fileList.Count);
            Log.Logger.Information("Error files : {Count}", errorcount);
            Log.Logger.Information("Processing time : {Elapsed}", timer.Elapsed);

            return true;
        }

        public static bool ReEncodeFiles(List<FileInfo> fileList)
        {
            Log.Logger.Information("ReEncoding files ...");

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
                Log.Logger.Information("ReEncoding : {Name}", fileinfo.FullName);
                if (!Convert.ConvertToMkv(fileinfo.FullName, out string _))
                {
                    Log.Logger.Error("Error ReEncoding : {Name}", fileinfo.FullName);
                    errorcount ++;
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            Log.Logger.Information("Total files : {Count}", fileList.Count);
            Log.Logger.Information("Error files : {Count}", errorcount);
            Log.Logger.Information("Processing time : {Elapsed}", timer.Elapsed);

            return true;
        }

        public static bool DeInterlaceFiles(List<FileInfo> fileList)
        {
            Log.Logger.Information("DeInterlacing files ...");

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
                Log.Logger.Information("DeInterlacing : {Name}", fileinfo.FullName);
                if (!Convert.DeInterlaceToMkv(fileinfo.FullName, out string _))
                {
                    Log.Logger.Error("Error DeInterlacing : {Name}", fileinfo.FullName);
                    errorcount ++;
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            Log.Logger.Information("Total files : {Count}", fileList.Count);
            Log.Logger.Information("Error files : {Count}", errorcount);
            Log.Logger.Information("Processing time : {Elapsed}", timer.Elapsed);

            return true;
        }

        public static bool GetTagMapFiles(List<FileInfo> fileList)
        {
            Log.Logger.Information("Creating tag map ...");

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
                Log.Logger.Information("Getting media info : {Name}", fileinfo.FullName);
                ProcessFile processFile = new ProcessFile(fileinfo);
                if (!processFile.GetMediaInfo())
                {
                    Log.Logger.Error("Error getting media info : {Name}", fileinfo.FullName);
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
            Log.Logger.Information("FFprobe:");
            fftags.WriteLine();
            Log.Logger.Information("MKVMerge:");
            mktags.WriteLine();
            Log.Logger.Information("MediaInfo:");
            mitags.WriteLine();

            // Stop the timer
            timer.Stop();

            // Done
            Log.Logger.Information("Total files : {Count}", fileList.Count);
            Log.Logger.Information("Error files : {Count}", errorcount);
            Log.Logger.Information("Processing time : {Elapsed}", timer.Elapsed);

            return true;
        }

        public static bool CreateSidecarFiles(List<FileInfo> fileList)
        {
            Log.Logger.Information("Creating sidecar files ...");

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
                Log.Logger.Information("Creating sidecar : {Name}", fileinfo.FullName);
                if (!SidecarFile.CreateSidecarFile(fileinfo))
                {
                    Log.Logger.Error("Error creating sidecar : {Name}", fileinfo.FullName);
                    errorcount ++;
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            Log.Logger.Information("Total files : {Count}", fileList.Count);
            Log.Logger.Information("Error files : {Count}", errorcount);
            Log.Logger.Information("Processing time : {Elapsed}", timer.Elapsed);

            return true;
        }

        public static bool GetSidecarFiles(List<FileInfo> fileList)
        {
            Log.Logger.Information("Reading sidecar files ...");

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
                if (!SidecarFile.IsSidecarFileName(fileinfo))
                    continue;

                // Read the sidecar files
                Log.Logger.Information("Reading sidecar file : {Name}", fileinfo.FullName);
                SidecarFile sidecarfile = new SidecarFile(fileinfo);
                if (!sidecarfile.Read())
                {
                    Log.Logger.Error("Error reading sidecar file : {Name}", fileinfo.FullName);
                    errorcount ++;
                }
                else 
                {
                    sidecarfile.WriteLine();
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            Log.Logger.Information("Total files : {Count}", fileList.Count);
            Log.Logger.Information("Error files : {Count}", errorcount);
            Log.Logger.Information("Processing time : {Elapsed}", timer.Elapsed);

            return true;
        }

        public static bool GetMediaInfoFiles(List<FileInfo> fileList)
        {
            Log.Logger.Information("Getting media information ...");

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
                Log.Logger.Information("Getting media information : {Name}", fileinfo.FullName);
                ProcessFile processFile = new ProcessFile(fileinfo);
                if (!processFile.GetMediaInfo())
                {
                    Log.Logger.Error("Error getting media information : {Name}", fileinfo.FullName);
                    errorcount ++;
                }
                else
                {
                    Log.Logger.Information("{Name}", fileinfo.FullName);
                    processFile.FfProbeInfo.WriteLine("FFprobe");
                    processFile.MkvMergeInfo.WriteLine("MKVMerge");
                    processFile.MediaInfoInfo.WriteLine("MediaInfo");
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            Log.Logger.Information("Total files : {Count}", fileList.Count);
            Log.Logger.Information("Error files : {Count}", errorcount);
            Log.Logger.Information("Processing time : {Elapsed}", timer.Elapsed);

            return true;
        }

        public static bool GetBitrateFiles(List<FileInfo> fileList)
        {
            Log.Logger.Information("Getting bitrate information ...");

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
                Log.Logger.Information("Getting bitrate information : {Name}", fileinfo.FullName);
                ProcessFile processFile = new ProcessFile(fileinfo);
                if (!processFile.GetBitrateInfo(out BitrateInfo bitrateInfo))
                {
                    Log.Logger.Error("Error getting bitrate information : {Name}", fileinfo.FullName);
                    errorcount ++;
                }
                else
                {
                    bitrateInfo.WriteLine();
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            Log.Logger.Information("Total files : {Count}", fileList.Count);
            Log.Logger.Information("Error files : {Count}", errorcount);
            Log.Logger.Information("Processing time : {Elapsed}", timer.Elapsed);

            return true;
        }

        private bool ProcessFile(FileInfo fileinfo, out bool modified)
        {
            // Init
            modified = false;

            // Skip the file if it is in the ignore list
            if (IgnoreList.Contains(fileinfo.FullName))
            {
                Log.Logger.Warning("Skipping ignored file : {Name}", fileinfo.FullName);
                return true;
            }

            // Does the file still exist
            if (!File.Exists(fileinfo.FullName))
            {
                Log.Logger.Warning("Skipping missing file : {Name}", fileinfo.FullName);
                return false;
            }

            // Is the file read-only
            if (fileinfo.Attributes.HasFlag(FileAttributes.ReadOnly) ||
                !FileEx.IsFileReadWriteable(fileinfo))
            {
                Log.Logger.Error("Skipping read-only file : {Name}", fileinfo.FullName);
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

            // All files past this point are MKV files

            // Cancel handler
            if (Program.IsCancelled())
                return false;

            // If a sidecar file exists for this MKV file it must be writable
            if (!processFile.IsSidecarWriteable())
            {
                Log.Logger.Error("Skipping media file due to read-only sidecar file : {Name}", fileinfo.FullName);
                return false;
            }

            // Read the media info
            if (!processFile.GetMediaInfo())
                return processFile.Result;

            // ReMux non-MKV containers using MKV filenames
            if (!processFile.RemuxNonMkvContainer(ref modified))
                return processFile.Result;

            // Cancel handler
            if (Program.IsCancelled())
                return false;

            // Try to ReMux metadata errors away
            if (!processFile.ReMuxMediaInfoErrors(ref modified))
                return processFile.Result;

            // Cancel handler
            if (Program.IsCancelled())
                return false;

            // Remove tags and unwanted metadata
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

        public static bool UpgradeSidecarFiles(List<FileInfo> fileList)
        {
            Log.Logger.Information("Upgrading sidecar files ...");

            // Start the stopwatch
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Process all files
            int totalCount = fileList.Count;
            int processedCount = 0;
            int errorCount = 0;
            foreach (FileInfo fileInfo in fileList)
            {
                // Percentage
                processedCount ++;
                double done = System.Convert.ToDouble(processedCount) / System.Convert.ToDouble(totalCount);

                // Cancel handler
                if (Program.IsCancelled())
                    return false;

                // Handle only MKV files
                if (!SidecarFile.IsMediaFileName(fileInfo))
                    continue;

                // Upgrade the sidecar files
                Log.Logger.Information("Upgrading sidecar file ({Done:P}) : {Name}", done, fileInfo.FullName);
                SidecarFile sidecarfile = new SidecarFile(fileInfo);
                if (!sidecarfile.Upgrade())
                {
                    Log.Logger.Error("Error upgrading sidecar file : {Name}", fileInfo.FullName);
                    errorCount ++;
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            Log.Logger.Information("Total files : {Count}", fileList.Count);
            Log.Logger.Information("Error files : {Count}", errorCount);
            Log.Logger.Information("Processing time : {Elapsed}", timer.Elapsed);

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
