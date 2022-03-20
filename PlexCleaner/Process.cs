using InsaneGenius.Utilities;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PlexCleaner;

internal class Process
{
    public Process()
    {
        // Convert List<string> to HashSet<string>
        KeepExtensions = new HashSet<string>(Program.Config.ProcessOptions.KeepExtensions, StringComparer.OrdinalIgnoreCase);
        ReMuxExtensions = new HashSet<string>(Program.Config.ProcessOptions.ReMuxExtensions, StringComparer.OrdinalIgnoreCase);
        ReEncodeAudioFormats = new HashSet<string>(Program.Config.ProcessOptions.ReEncodeAudioFormats, StringComparer.OrdinalIgnoreCase);
        FileIgnoreList = new HashSet<string>(Program.Config.ProcessOptions.FileIgnoreList, StringComparer.OrdinalIgnoreCase);

        // Maintain order, keep in List<string>
        PreferredAudioFormats = Program.Config.ProcessOptions.PreferredAudioFormats;

        // Default to eng if language not set
        if (string.IsNullOrEmpty(Program.Config.ProcessOptions.DefaultLanguage))
        {
            Program.Config.ProcessOptions.DefaultLanguage = "eng";
        }

        // Always keep zxx no linguistic content and the default language
        KeepLanguages = new HashSet<string>(Program.Config.ProcessOptions.KeepLanguages, StringComparer.OrdinalIgnoreCase)
        {
            "zxx",
            Program.Config.ProcessOptions.DefaultLanguage
        };

        // Convert VideoFormat to VideoInfo
        ReEncodeVideoInfos = new List<VideoInfo>();
        foreach (VideoInfo videoInfo in Program.Config.ProcessOptions.ReEncodeVideo.Select(format =>
            new VideoInfo
            {
                Codec = format.Codec,
                Format = format.Format,
                Profile = format.Profile
            }))
        {
            ReEncodeVideoInfos.Add(videoInfo);
        }
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
        {
            return true;
        }

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

        // Warn if reprocessing
        // TODO: Find a better way to scale conditional processing, e.g. flags
        if (Program.Options.ReProcess > 0)
        {
            Log.Logger.Warning("Re-processing level is {ReProcess}", Program.Options.ReProcess);
        }

        // Process all the files
        bool ret = ProcessFilesDriver(fileList, "Process", fileInfo =>
        {
            // Process the file
            if (!ProcessFile(fileInfo, out bool modified, out SidecarFile.States state, out FileInfo processInfo))
            {
                if (!Program.IsCancelled())
                {
                    errorFiles.Add(fileInfo.FullName);
                }

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
            errorFiles.ForEach(item => Log.Logger.Information("{FileName}", item));
        }
        if (modifiedInfo.Count > 0)
        {
            Log.Logger.Information("Modified files:");
            foreach ((string fileName, SidecarFile.States state) in modifiedInfo)
            {
                Log.Logger.Information("{State} : {FileName}", state, fileName);
            }
        }

        // Write the updated ignore file list
        if (Program.Config.VerifyOptions.RegisterInvalidFiles &&
            Program.Config.ProcessOptions.FileIgnoreList.Count != FileIgnoreList.Count)
        {
            Log.Logger.Information("Updating settings file : {SettingsFile}", Program.Options.SettingsFile);
            Program.Config.ProcessOptions.FileIgnoreList.Sort();
            ConfigFileJsonSchema.ToFile(Program.Options.SettingsFile, Program.Config);
        }

        return ret;
    }
    private bool ProcessFile(FileInfo fileInfo, out bool modified, out SidecarFile.States state, out FileInfo processInfo)
    {
        // Init
        modified = false;
        state = SidecarFile.States.None;
        processInfo = fileInfo;
        DateTime lastWriteTime = fileInfo.LastWriteTimeUtc;

        // Skip the file if it is in the ignore list
        if (FileIgnoreList.Contains(fileInfo.FullName))
        {
            Log.Logger.Warning("Skipping ignored file : {FileName}", fileInfo.FullName);
            return true;
        }

        // Skip the file if it is in the keep extensions list
        if (KeepExtensions.Contains(fileInfo.Extension))
        {
            Log.Logger.Warning("Skipping keep extensions file : {FileName}", fileInfo.FullName);
            return true;
        }

        // Does the file still exist
        if (!File.Exists(fileInfo.FullName))
        {
            Log.Logger.Warning("Skipping missing or access denied file : {FileName}", fileInfo.FullName);
            return false;
        }

        // Create file processor to hold state
        ProcessFile processFile = new(fileInfo);

        // Is the file writeable
        if (!processFile.IsWriteable())
        {
            Log.Logger.Error("Skipping read-only file : {FileName}", fileInfo.FullName);
            return false;
        }

        // Delete the sidecar file if matching MKV file not found
        if (!processFile.DeleteMismatchedSidecarFile(ref modified))
        {
            return false;
        }

        // Skip if this a sidecar file
        if (SidecarFile.IsSidecarFile(fileInfo))
        {
            return true;
        }

        // ReMux non-MKV containers matched by extension
        if (!processFile.RemuxByExtensions(ReMuxExtensions, ref modified) ||
            Program.IsCancelled())
        {
            return false;
        }

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
            Log.Logger.Error("Skipping media file due to read-only sidecar file : {FileName}", fileInfo.FullName);
            return false;
        }

        // Make sure the file extension is lowercase for case sensitive filesystems
        if (!processFile.MakeExtensionLowercase(ref modified))
        {
            return false;
        }

        // Read the media info
        if (!processFile.GetMediaInfo() ||
            Program.IsCancelled())
        {
            return false;
        }

        // ReMux non-MKV containers using MKV file extensions
        if (!processFile.RemuxNonMkvContainer(ref modified) ||
            Program.IsCancelled())
        {
            return false;
        }

        // Remove all attachments, they can interfere and show up as video streams
        if (!processFile.RemoveAttachments(ref modified) ||
            Program.IsCancelled())
        {
            return false;
        }

        // The file extension and container type is MKV and all media info should be valid
        if (!processFile.VerifyMediaInfo())
        {
            return false;
        }

        // ReMux to repair metadata errors
        if (!processFile.ReMuxMediaInfoErrors(ref modified) ||
            Program.IsCancelled())
        {
            return false;
        }

        // Remove EIA-608 / Closed Captions from the video stream
        if (!processFile.RemoveClosedCaptions(ref modified) ||
            Program.IsCancelled())
        {
            return false;
        }

        // Deinterlace interlaced content
        if (!processFile.DeInterlace(ref modified) ||
            Program.IsCancelled())
        {
            return false;
        }

        // Change all tracks with an unknown language to the default language
        if (!processFile.SetUnknownLanguage(ref modified) ||
            Program.IsCancelled())
        {
            return false;
        }

        // Remove all the unwanted and duplicate language tracks
        if (!processFile.ReMux(KeepLanguages, PreferredAudioFormats, ref modified) ||
            Program.IsCancelled())
        {
            return false;
        }

        // Re-Encode formats that cannot be direct-played
        if (!processFile.ReEncode(ReEncodeVideoInfos, ReEncodeAudioFormats, ref modified) ||
            Program.IsCancelled())
        {
            return false;
        }

        // Verify media streams, and repair if possible
        if (!processFile.Verify(ref modified) ||
            Program.IsCancelled())
        {
            return false;
        }

        // Repair may have modified track tags, reset the default language again
        if (!processFile.SetUnknownLanguage(ref modified) ||
            Program.IsCancelled())
        {
            return false;
        }

        // Remove tags and titles
        if (!processFile.RemoveTags(ref modified) ||
            Program.IsCancelled())
        {
            return false;
        }

        // Restore the file timestamp
        if (!processFile.SetLastWriteTimeUtc(lastWriteTime) ||
            Program.IsCancelled())
        {
            return false;
        }

        // Re-verify the tool info is correctly recorded
        if (!processFile.VerifyMediaInfo())
        {
            return false;
        }

        // Return state and current fileinfo
        state = processFile.State;
        processInfo = processFile.FileInfo;

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
            {
                return true;
            }

            // ReMux
            return Convert.ReMuxToMkv(fileInfo.FullName, out string _);
        });
    }

    public static bool ReEncodeFiles(List<FileInfo> fileList)
    {
        return ProcessFilesDriver(fileList, "ReEncode", fileInfo =>
        {
            // Handle only MKV files
            // ReMux before re-encode, so the track attribute logic works as expected
            if (!MkvMergeTool.IsMkvFile(fileInfo))
            {
                return true;
            }

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
            {
                return true;
            }

            // Deinterlace
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
                {
                    return true;
                }

                // Get media information
                ProcessFile processFile = new(fileInfo);
                if (!processFile.GetMediaInfo())
                {
                    return false;
                }

                // Add all the tags
                fftags.Add(processFile.FfProbeInfo, processFile.MkvMergeInfo, processFile.MediaInfoInfo);
                mktags.Add(processFile.MkvMergeInfo, processFile.FfProbeInfo, processFile.MediaInfoInfo);
                mitags.Add(processFile.MediaInfoInfo, processFile.FfProbeInfo, processFile.MkvMergeInfo);

                return true;
            }))
        {
            return false;
        }

        // Print the tags
        Log.Logger.Information("FfProbe:");
        fftags.WriteLine();
        Log.Logger.Information("MkvMerge:");
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
            {
                return true;
            }

            // Create the sidecar file
            SidecarFile sidecarfile = new(fileInfo);
            return sidecarfile.Create();
        });
    }

    public static bool GetSidecarFiles(List<FileInfo> fileList)
    {
        return ProcessFilesDriver(fileList, "Get Sidecar Information", fileInfo =>
        {
            // Handle only sidecar files
            if (!SidecarFile.IsSidecarFile(fileInfo))
            {
                return true;
            }

            // Get sidecar information
            SidecarFile sidecarfile = new(fileInfo);
            if (!sidecarfile.Read())
            {
                return false;
            }

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
            {
                return true;
            }

            // Get media information
            ProcessFile processFile = new(fileInfo);
            if (!processFile.GetMediaInfo())
            {
                return false;
            }

            // Print info
            Log.Logger.Information("{FileName}", fileInfo.FullName);
            processFile.FfProbeInfo.WriteLine("FfProbe");
            processFile.MkvMergeInfo.WriteLine("MkvMerge");
            processFile.MediaInfoInfo.WriteLine("MediaInfo");

            return true;
        });
    }

    public static bool GetToolInfoFiles(List<FileInfo> fileList)
    {
        return ProcessFilesDriver(fileList, "Get Tool Information", fileInfo =>
        {
            // Skip sidecar files
            if (SidecarFile.IsSidecarFile(fileInfo))
            {
                return true;
            }

            // Get tool information
            // Read the tool info text
            if (!Tools.MediaInfo.GetMediaInfoXml(fileInfo.FullName, out string mediaInfoXml) ||
                !Tools.MkvMerge.GetMkvInfoJson(fileInfo.FullName, out string mkvMergeInfoJson) ||
                !Tools.FfProbe.GetFfProbeInfoJson(fileInfo.FullName, out string ffProbeInfoJson))
            {
                Log.Logger.Error("Failed to read tool info : {FileName}", fileInfo.Name);
                return false;
            }

            // Print and log info
            Log.Logger.Information("{FileName}", fileInfo.FullName);
            Log.Logger.Information("FfProbe: {FfProbeText}", ffProbeInfoJson);
            Console.Write(ffProbeInfoJson);
            Log.Logger.Information("MkvMerge: {MkvMergeText}", mkvMergeInfoJson);
            Console.Write(mkvMergeInfoJson);
            Log.Logger.Information("MediaInfo: {MediaInfoText}", mediaInfoXml);
            Console.Write(mediaInfoXml);

            return true;
        });
    }

    public static bool RemoveSubtitlesFiles(List<FileInfo> fileList)
    {
        return ProcessFilesDriver(fileList, "Remove Subtitles", fileInfo =>
        {
            // Handle only MKV files
            if (!MkvMergeTool.IsMkvFile(fileInfo))
            {
                return true;
            }

            // Get media information
            ProcessFile processFile = new(fileInfo);
            if (!processFile.GetMediaInfo())
            {
                return false;
            }

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
        foreach (FileInfo fileInfo in fileList)
        {
            // Cancel handler
            if (Program.IsCancelled())
            {
                return false;
            }

            // Perform the task
            processedCount++;
            double processedPercentage = System.Convert.ToDouble(processedCount) / System.Convert.ToDouble(totalCount);
            Log.Logger.Information("{TaskName} ({Processed:P}) : {FileName}", taskName, processedPercentage, fileInfo.FullName);
            if (!taskFunc(fileInfo) &&
                !Program.IsCancelled())
            {
                Log.Logger.Error("{TaskName} Error : {FileName}", taskName, fileInfo.FullName);
                errorCount++;
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

    private readonly HashSet<string> FileIgnoreList;
    private readonly HashSet<string> KeepExtensions;
    private readonly HashSet<string> ReMuxExtensions;
    private readonly HashSet<string> ReEncodeAudioFormats;
    private readonly HashSet<string> KeepLanguages;
    private readonly List<string> PreferredAudioFormats;
    private readonly List<VideoInfo> ReEncodeVideoInfos;
}