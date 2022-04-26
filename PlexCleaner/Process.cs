using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using InsaneGenius.Utilities;
using Serilog;

namespace PlexCleaner;

// Filename, State
using ProcessTuple = ValueTuple<string, SidecarFile.States>;

internal class Process
{
    // Processing tasks
    public enum Tasks
    {
        ClearTags,
        ClearAttachments,
        IdetFilter,
        FindClosedCaptions,
        Repair,
        VerifyMetadata,
        VerifyStream
    }

    public static bool CanReProcess(Tasks task)
    {
        // 0: No re-processing
        if (Program.Options.ReProcess == 0)
        {
            return false;
        }

        // Compare type of task with level of re-processing
        switch (task)
        {
            // 1+: Metadata processing
            case Tasks.ClearTags:
            case Tasks.ClearAttachments:
            case Tasks.FindClosedCaptions:
            case Tasks.VerifyMetadata:
                return Program.Options.ReProcess >= 1;
            // 2+: Stream processing
            case Tasks.IdetFilter:
            case Tasks.VerifyStream:
            case Tasks.Repair:
                return Program.Options.ReProcess >= 2;
            default:
                return false;
        }
    }

    public Process()
    {
        // Convert List<string> to HashSet<string>
        // Case insensitive, duplicates with different case is not supported
        KeepExtensions = new HashSet<string>(Program.Config.ProcessOptions.KeepExtensions, StringComparer.OrdinalIgnoreCase);
        ReMuxExtensions = new HashSet<string>(Program.Config.ProcessOptions.ReMuxExtensions, StringComparer.OrdinalIgnoreCase);
        ReEncodeAudioFormats = new HashSet<string>(Program.Config.ProcessOptions.ReEncodeAudioFormats, StringComparer.OrdinalIgnoreCase);
        FileIgnoreList = new HashSet<string>(Program.Config.ProcessOptions.FileIgnoreList, StringComparer.OrdinalIgnoreCase);
        if (FileIgnoreList.Count != Program.Config.ProcessOptions.FileIgnoreList.Count)
        {
            Log.Logger.Warning("FileIgnoreList contains duplicates, {Set} != {List}", FileIgnoreList.Count, Program.Config.ProcessOptions.FileIgnoreList.Count);
            Program.Config.ProcessOptions.RemoveIgnoreDuplicates();
        }

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
        // Log active options
        Log.Logger.Information("Process Options: TestSnippets: {TestSnippets}, TestNoModify: {TestNoModify}, ReProcess: {ReProcess}, FileIgnoreList: {FileIgnoreList}",
                               Program.Options.TestSnippets,
                               Program.Options.TestNoModify,
                               Program.Options.ReProcess,
                               Program.Config.ProcessOptions.FileIgnoreList.Count);

        // Ignore count before
        int ignoreCount = FileIgnoreList.Count;

        // Process all the files
        List<ProcessTuple> errorInfo = new();
        List<ProcessTuple> modifiedInfo = new();
        List<ProcessTuple> failedInfo = new();
        bool ret = ProcessFilesDriver(fileList, "Process", fileInfo =>
        {
            // Process the file
            bool processResult = ProcessFile(fileInfo, out bool modified, out SidecarFile.States state, out FileInfo processInfo);
            if (!processResult &&
                Program.IsCancelled())
            {
                // Cancelled
                return false;
            }

            // Error
            if (!processResult)
            { 
                errorInfo.Add(new ProcessTuple(processInfo.FullName, state));
            }

            // Modified
            if (modified)
            {
                modifiedInfo.Add(new ProcessTuple(processInfo.FullName, state));
            }
            
            // Verify failed
            if (state.HasFlag(SidecarFile.States.VerifyFailed))
            {
                failedInfo.Add(new ProcessTuple(processInfo.FullName, state));

                // Add the failed file to the ignore list
                if (Program.Config.VerifyOptions.RegisterInvalidFiles)
                {
                    Program.Config.ProcessOptions.AddIgnoreEntry(processInfo.FullName);
                }
            }

            return processResult;
        });

        // Summary
        // Log.Logger.Information("Error files : {Count}", errorCount);
        errorInfo.ForEach(item => Log.Logger.Information("Error: {State} : {FileName}", item.Item2, item.Item1));

        Log.Logger.Information("Modified files : {Count}", modifiedInfo.Count);
        modifiedInfo.ForEach(item => Log.Logger.Information("Modified: {State} : {FileName}", item.Item2, item.Item1));

        Log.Logger.Information("VerifyFailed files : {Count}", failedInfo.Count);
        failedInfo.ForEach(item => Log.Logger.Information("VerifyFailed: {State} : {FileName}", item.Item2, item.Item1));

        // Write the updated ignore file list
        if (Program.Config.VerifyOptions.RegisterInvalidFiles &&
            Program.Config.ProcessOptions.FileIgnoreList.Count != ignoreCount)
        {
            Log.Logger.Information("Updating FileIgnoreList entries ({Count}) in settings file : {SettingsFile}", 
                                    Program.Config.ProcessOptions.FileIgnoreList.Count, 
                                    Program.Options.SettingsFile);
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
        ProcessFile processFile = null;
        bool result;

        // Process in jump loop
        for (; ; )
        {
            // Skip the file if it is in the ignore list
            if (FileIgnoreList.Contains(fileInfo.FullName))
            {
                Log.Logger.Warning("Skipping ignored file : {FileName}", fileInfo.FullName);
                result = true;
                break;
            }

            // Skip the file if it is in the keep extensions list
            if (KeepExtensions.Contains(fileInfo.Extension))
            {
                Log.Logger.Warning("Skipping keep extensions file : {FileName}", fileInfo.FullName);
                result = true;
                break;
            }

            // Does the file still exist
            if (!File.Exists(fileInfo.FullName))
            {
                Log.Logger.Warning("Skipping missing or access denied file : {FileName}", fileInfo.FullName);
                result = false;
                break;
            }

            // Create file processor to hold state
            processFile = new ProcessFile(fileInfo);

            // Is the file writeable
            // TODO: Rare sharing violations on SMB right after opening FileInfo in the ProcessFile constructor
            if (!processFile.IsWriteable())
            {
                Log.Logger.Error("Skipping read-only file : {FileName}", fileInfo.FullName);
                result = false;
                break;
            }

            // Delete the sidecar file if matching MKV file not found
            if (!processFile.DeleteMismatchedSidecarFile(ref modified))
            {
                result = false;
                break;
            }

            // Skip if this is a sidecar file
            if (SidecarFile.IsSidecarFile(fileInfo))
            {
                result = true;
                break;
            }

            // ReMux non-MKV containers matched by extension
            if (!processFile.RemuxByExtensions(ReMuxExtensions, ref modified) ||
                Program.IsCancelled())
            {
                result = false;
                break;
            }

            // Delete or skip non-MKV files
            if (!processFile.DeleteNonMkvFile(ref modified))
            {
                result = true;
                break;
            }

            // All files past this point are MKV files

            // If a sidecar file exists for this MKV file it must be writable
            if (!processFile.IsSidecarWriteable())
            {
                Log.Logger.Error("Skipping media file due to read-only sidecar file : {FileName}", fileInfo.FullName);
                result = false;
                break;
            }

            // Make sure the file extension is lowercase for case sensitive filesystems
            if (!processFile.MakeExtensionLowercase(ref modified))
            {
                result = false;
                break;
            }

            // Read the media info
            if (!processFile.GetMediaInfo() ||
                Program.IsCancelled())
            {
                result = false;
                break;
            }

            // ReMux non-MKV containers using MKV file extensions
            if (!processFile.RemuxNonMkvContainer(ref modified) ||
                Program.IsCancelled())
            {
                result = false;
                break;
            }

            // Remove all attachments, they can interfere and show up as video streams
            if (!processFile.RemoveAttachments(ref modified) ||
                Program.IsCancelled())
            {
                result = false;
                break;
            }

            // The file extension and container type is MKV and all media info should be valid
            if (!processFile.VerifyMediaInfo())
            {
                result = false;
                break;
            }

            // ReMux to repair metadata errors
            if (!processFile.ReMuxMediaInfoErrors(ref modified) ||
                Program.IsCancelled())
            {
                result = false;
                break;
            }

            // Remove EIA-608 / Closed Captions from the video stream
            if (!processFile.RemoveClosedCaptions(ref modified) ||
                Program.IsCancelled())
            {
                result = false;
                break;
            }

            // Deinterlace interlaced content
            if (!processFile.DeInterlace(ref modified) ||
                Program.IsCancelled())
            {
                result = false;
                break;
            }

            // Change all tracks with an unknown language to the default language
            if (!processFile.SetUnknownLanguage(ref modified) ||
                Program.IsCancelled())
            {
                result = false;
                break;
            }

            // Remove all the unwanted and duplicate language tracks
            if (!processFile.ReMux(KeepLanguages, PreferredAudioFormats, ref modified) ||
                Program.IsCancelled())
            {
                result = false;
                break;
            }

            // Re-Encode formats that cannot be direct-played
            if (!processFile.ReEncode(ReEncodeVideoInfos, ReEncodeAudioFormats, ref modified) ||
                Program.IsCancelled())
            {
                result = false;
                break;
            }

            // Verify media streams, and repair if possible
            if (!processFile.Verify(ref modified) ||
                Program.IsCancelled())
            {
                result = false;
                break;
            }

            // Repair may have modified track tags, reset the default language again
            if (!processFile.SetUnknownLanguage(ref modified) ||
                Program.IsCancelled())
            {
                result = false;
                break;
            }

            // Remove tags and titles
            if (!processFile.RemoveTags(ref modified) ||
                Program.IsCancelled())
            {
                result = false;
                break;
            }

            // Restore the file timestamp
            if (!processFile.SetLastWriteTimeUtc(lastWriteTime) ||
                Program.IsCancelled())
            {
                result = false;
                break;
            }

            // Re-verify the tool info is correctly recorded
            if (!processFile.VerifyMediaInfo())
            {
                result = false;
                break;
            }

            // Done
            result = true;
            break;
        }

        // Return current state and fileinfo
        if (processFile != null)
        {
            state = processFile.State;
            processInfo = processFile.FileInfo;
        }
        return result;
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
