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

            // Extensions of otehr files to skip and keep
            // TODO : Add support for ignoring FUSE files e.g. .fuse_hidden191817c5000c5ee7, will need wildcard support
            List<string> stringlist = Program.Config.ProcessOptions.KeepExtensions.Split(',').ToList();
            KeepExtensions = new HashSet<string>(stringlist, StringComparer.OrdinalIgnoreCase);

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
            for (int i = 0; i < codeclist.Count; i ++)
            {
                // We match against the format and profile
                // Match the logic in VideoInfo.CompareVideo
                VideoInfo videoinfo = new()
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

        public bool ProcessFiles(List<FileInfo> fileList)
        {
            // Keep a List of failed and modified files to print when done
            List<(string fileName, SidecarFile.States state)> modifiedInfo = new();
            List<string> errorFiles = new();

            // Process all the files
            bool ret = ProcessFilesDriver(fileList, "Process", fileInfo =>
            {
                // Process the file
                if (!ProcessFile(fileInfo, out bool modified, out SidecarFile.States state, out FileInfo processInfo))
                {
                    if (!Program.IsCancelled())
                        errorFiles.Add(fileInfo.FullName);
                    return false;
                }

                // Modified
                if (modified)
                {
                    modifiedInfo.Add(new ValueTuple<string, SidecarFile.States>(processInfo.FullName, state));
                }

                return true;
            });

            // Summary
            // ProcessFilesDriver() does primary logging
            Log.Logger.Information("Modified files : {Count}", modifiedInfo.Count);
            if (errorFiles.Count > 0)
            {
                Log.Logger.Information("Error files:");
                foreach (string file in errorFiles)
                    Log.Logger.Information("{FileName}", file);
            }
            if (modifiedInfo.Count > 0)
            {
                Log.Logger.Information("Modified files:");
                foreach ((string fileName, SidecarFile.States state) in modifiedInfo)
                    Log.Logger.Information("{State} : {FileName}", state, fileName);
            }

            // Write the updated ignore file list
            if (Program.Config.VerifyOptions.RegisterInvalidFiles &&
                Program.Config.ProcessOptions.FileIgnoreList.Count != IgnoreList.Count)
            {
                Log.Logger.Information("Updating settings file : {SettingsFile}", Program.Options.SettingsFile);
                Program.Config.ProcessOptions.FileIgnoreList.Sort();
                ConfigFileJsonSchema.ToFile(Program.Options.SettingsFile, Program.Config);
            }

            return ret;
        }
        private bool ProcessFile(FileInfo fileinfo, out bool modified, out SidecarFile.States state, out FileInfo processInfo)
        {
            // Init
            modified = false;
            state = SidecarFile.States.None;
            processInfo = fileinfo;

            // Skip the file if it is in the ignore list
            if (IgnoreList.Contains(fileinfo.FullName))
            {
                Log.Logger.Warning("Skipping ignored file : {Name}", fileinfo.FullName);
                return true;
            }

            // Skip the file if it is in the keep extensions list
            if (KeepExtensions.Contains(fileinfo.Extension))
            {
                Log.Logger.Warning("Skipping keep extensions file : {Name}", fileinfo.FullName);
                return true;
            }

            // Does the file still exist
            if (!File.Exists(fileinfo.FullName))
            {
                Log.Logger.Warning("Skipping missing file : {Name}", fileinfo.FullName);
                return false;
            }

            // Create file processor to hold state
            ProcessFile processFile = new(fileinfo);

            // Is the file writeable
            if (!processFile.IsWriteable())
            {
                Log.Logger.Error("Skipping read-only file : {Name}", fileinfo.FullName);
                return false;
            }

            // Delete the sidecar file if matching MKV file not found
            if (!processFile.DeleteMismatchedSidecarFile(ref modified))
                return false;

            // Skip if this a sidecar file
            if (SidecarFile.IsSidecarFile(fileinfo))
                return true;

            // ReMux non-MKV containers matched by extension
            if (!processFile.RemuxByExtensions(ReMuxExtensions, ref modified) ||
                Program.IsCancelled())
                return false;

            // All files past this point are MKV files
            if (!processFile.DeleteNonMkvFile(ref modified))
            {
                // File may have been deleted or skipped
                state = processFile.State;
                return true;
            }

            // If a sidecar file exists for this MKV file it must be writable
            if (!processFile.IsSidecarWriteable())
            {
                Log.Logger.Error("Skipping media file due to read-only sidecar file : {Name}", fileinfo.FullName);
                return false;
            }

            // Read the media info
            if (!processFile.GetMediaInfo() ||
                Program.IsCancelled())
                return false;

            // Make sure the file extension is lowercase
            // Case sensitive on Linux, i.e. .MKV != .mkv
            if (!processFile.MakeExtensionLowercase(ref modified))
                return false;

            // ReMux non-MKV containers using MKV filenames
            if (!processFile.RemuxNonMkvContainer(ref modified) ||
                Program.IsCancelled())
                return false;

            // Try to ReMux metadata errors away
            if (!processFile.ReMuxMediaInfoErrors(ref modified) ||
                Program.IsCancelled())
                return false;

            // Change all tracks with an unknown language to the default language
            // Merge operation uses language tags, make sure they are set
            if (!processFile.SetUnknownLanguage(ref modified) ||
                Program.IsCancelled())
                return false;

            // Merge all remux operations into a single call
            // Remove all the unwanted language tracks
            // Remove all duplicate tracks
            if (!processFile.ReMux(KeepLanguages, PreferredAudioFormats, ref modified) ||
                Program.IsCancelled())
                return false;

            // De-interlace interlaced content
            if (!processFile.DeInterlace(ref modified) ||
                Program.IsCancelled())
                return false;

            // Re-Encode formats that cannot be direct-played
            if (!processFile.ReEncode(ReEncodeVideoInfos, ReEncodeAudioFormats, ref modified) ||
                Program.IsCancelled())
                return false;

            // Verify media streams
            // Repair if possible
            if (!processFile.Verify(true, ref modified) ||
                Program.IsCancelled())
                return false;

            // Remove tags and titles
            if (!processFile.RemoveTags(ref modified) ||
                Program.IsCancelled())
                return false;

            // Return state and current fileinfo
            state = processFile.State;
            processInfo = processFile.FileInfo;

            // TODO: Fix processing so we do not need to double clear tags or set track languages

            // Cancel handler
            return !Program.IsCancelled();
        }

        public bool ReMuxFiles(List<FileInfo> fileList)
        {
            return ProcessFilesDriver(fileList, "ReMux", fileInfo =>
            {
                // Handle only MKV files, and files in the remux extension list
                if (!MkvMergeTool.IsMkvFile(fileInfo) &&
                    !ReMuxExtensions.Contains(fileInfo.Extension))
                    return true;

                // ReMux
                return Convert.ReMuxToMkv(fileInfo.FullName, out string _);
            });
        }

        public static bool VerifyFiles(List<FileInfo> fileList)
        {
            return ProcessFilesDriver(fileList, "Verify", fileInfo =>
            {
                // Handle only MKV files
                if (!MkvMergeTool.IsMkvFile(fileInfo))
                    return true;

                // Get media information
                ProcessFile processFile = new(fileInfo);
                if (!processFile.GetMediaInfo())
                    return false;

                // Verify
                bool modified = false;
                return processFile.Verify(false, ref modified);
            });
        }

        public static bool ReEncodeFiles(List<FileInfo> fileList)
        {
            return ProcessFilesDriver(fileList, "ReEncode", fileInfo =>
            {
                // Handle only MKV files
                // ReMux before re-encode, so the track attribute logic works as expected
                if (!MkvMergeTool.IsMkvFile(fileInfo))
                    return true;

                // Re-encode
                return Convert.ConvertToMkv(fileInfo.FullName, out string _);
            });
        }

        public static bool DeInterlaceFiles(List<FileInfo> fileList)
        {
            return ProcessFilesDriver(fileList, "DeInterlace", fileInfo =>
            {
                // Handle only MKV files
                if (!MkvMergeTool.IsMkvFile(fileInfo))
                    return true;

                // De-interlace
                return Convert.DeInterlaceToMkv(fileInfo.FullName, out string _);
            });
        }

        public static bool GetTagMapFiles(List<FileInfo> fileList)
        {
            // Create a dictionary of ffprobe to mkvmerge and mediainfo tag strings
            TagMapDictionary fftags = new();
            TagMapDictionary mktags = new();
            TagMapDictionary mitags = new();

            // Process all the files
            if (!ProcessFilesDriver(fileList, "Create Tag Map", fileInfo =>
            {
                // Handle only MKV files
                if (!MkvMergeTool.IsMkvFile(fileInfo))
                    return true;

                // Get media information
                ProcessFile processFile = new(fileInfo);
                if (!processFile.GetMediaInfo())
                    return false;

                // Add all the tags
                fftags.Add(processFile.FfProbeInfo, processFile.MkvMergeInfo, processFile.MediaInfoInfo);
                mktags.Add(processFile.MkvMergeInfo, processFile.FfProbeInfo, processFile.MediaInfoInfo);
                mitags.Add(processFile.MediaInfoInfo, processFile.FfProbeInfo, processFile.MkvMergeInfo);

                return true;
            }))
                return false;

            // Print the tags
            Log.Logger.Information("FFprobe:");
            fftags.WriteLine();
            Log.Logger.Information("MKVMerge:");
            mktags.WriteLine();
            Log.Logger.Information("MediaInfo:");
            mitags.WriteLine();

            return true;
        }

        public static bool CreateSidecarFiles(List<FileInfo> fileList)
        {
            return ProcessFilesDriver(fileList, "Create Sidecar Files", fileInfo =>
            {
                // Handle only MKV files
                if (!MkvMergeTool.IsMkvFile(fileInfo))
                    return true;

                // Create the sidecar file
                SidecarFile sidecarfile = new(fileInfo);
                return sidecarfile.Create();
            });
        }

        public static bool UpgradeSidecarFiles(List<FileInfo> fileList)
        {
            return ProcessFilesDriver(fileList, "Upgrade Sidecar Files", fileInfo =>
            {
                // Handle only MKV files
                if (!MkvMergeTool.IsMkvFile(fileInfo))
                    return true;

                // Upgrade the sidecar file
                SidecarFile sidecarfile = new(fileInfo);
                return sidecarfile.Upgrade();
            });
        }

        public static bool GetSidecarFiles(List<FileInfo> fileList)
        {
            return ProcessFilesDriver(fileList, "Get Sidecar Information", fileInfo =>
            {
                // Handle only sidecar files
                if (!SidecarFile.IsSidecarFile(fileInfo))
                    return true;

                // Get sidecar information
                SidecarFile sidecarfile = new(fileInfo);
                if (!sidecarfile.Read())
                    return false;

                // Print info
                sidecarfile.WriteLine();

                return true;
            });
        }

        public static bool GetMediaInfoFiles(List<FileInfo> fileList)
        {
            return ProcessFilesDriver(fileList, "Get Media Information", fileInfo =>
            {
                // Handle only MKV files
                if (!MkvMergeTool.IsMkvFile(fileInfo))
                    return true;

                // Get media information
                ProcessFile processFile = new(fileInfo);
                if (!processFile.GetMediaInfo())
                    return false;

                // Print info
                Log.Logger.Information("{Name}", fileInfo.FullName);
                processFile.MediaInfoInfo.WriteLine("MediaInfo");
                processFile.MkvMergeInfo.WriteLine("MKVMerge");
                processFile.FfProbeInfo.WriteLine("FFprobe");

                return true;
            });
        }

        public static bool GetToolInfoFiles(List<FileInfo> fileList)
        {
            return ProcessFilesDriver(fileList, "Get Tool Information", fileInfo =>
            {
                // Don't limit to MKV only

                // Get tool information
                ProcessFile processFile = new(fileInfo);
                if (!processFile.GetToolInfo())
                    return false;

                // Print info
                Log.Logger.Information("{Name}", fileInfo.FullName);
                Log.Logger.Information("FFprobe:");
                Console.Write(processFile.FfProbeText);
                Log.Logger.Information("MKVMerge:");
                Console.Write(processFile.MkvMergeText);
                Log.Logger.Information("MediaInfo:");
                Console.Write(processFile.MediaInfoText);

                return true;
            });
        }

        public static bool GetBitrateInfoFiles(List<FileInfo> fileList)
        {
            return ProcessFilesDriver(fileList, "Get Bitrate Information", fileInfo =>
            {
                // Handle only MKV files
                if (!MkvMergeTool.IsMkvFile(fileInfo))
                    return true;

                // Get bitrate info
                ProcessFile processFile = new(fileInfo);
                if (!processFile.GetBitrateInfo(out BitrateInfo bitrateInfo))
                    return false;

                // Print bitrate info
                bitrateInfo.WriteLine();

                return true;
            });
        }

        public static bool RemoveSubtitlesFiles(List<FileInfo> fileList)
        {
            return ProcessFilesDriver(fileList, "Remove Subtitles", fileInfo =>
            {
                // Handle only MKV files
                if (!MkvMergeTool.IsMkvFile(fileInfo))
                    return true;

                // Get media information
                ProcessFile processFile = new(fileInfo);
                if (!processFile.GetMediaInfo())
                    return false;

                // Remove subtitles
                bool modified = false;
                return processFile.RemoveSubtitles(ref modified);
            });
        }

        private static bool ProcessFilesDriver(List<FileInfo> fileList, string taskName, Func<FileInfo, bool> taskFunc)
        {
            // Start
            Log.Logger.Information("Starting {TaskName}, processing {Count} files ...", taskName, fileList.Count);
            Stopwatch timer = new();
            timer.Start();

            // Process all files
            int totalCount = fileList.Count;
            int processedCount = 0;
            int errorCount = 0;
            double processedPercentage = 0.0;
            foreach (FileInfo fileInfo in fileList)
            {
                // Cancel handler
                if (Program.IsCancelled())
                    return false;

                // Perform the task
                processedCount ++;
                processedPercentage = System.Convert.ToDouble(processedCount) / System.Convert.ToDouble(totalCount);
                Log.Logger.Information("{TaskName} ({Processed:P}) : {FileName}", taskName, processedPercentage, fileInfo.FullName);
                if (!taskFunc(fileInfo) &&
                    !Program.IsCancelled())
                {
                    Log.Logger.Error("{TaskName} Error : {FileName}", taskName, fileInfo.FullName);
                    errorCount ++;
                }

                // Next file
            }

            // Stop the timer
            timer.Stop();

            // Done
            Log.Logger.Information("Completed {TaskName}", taskName);
            Log.Logger.Information("Processing time : {Elapsed}", timer.Elapsed);
            Log.Logger.Information("Total files : {Count}", totalCount);
            Log.Logger.Information("Error files : {Count}", errorCount);

            return errorCount == 0;
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
