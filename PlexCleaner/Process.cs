using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
        if (!FileEx.EnumerateDirectories(folderList, out List<FileInfo> fileInfoList, out _))
        { 
            return false;
        }

        // Process the files
        List<string> fileList = new();
        fileInfoList.ForEach(item => fileList.Add(item.FullName));
        return ProcessFiles(fileList);
    }

    public static bool DeleteEmptyFolders(IEnumerable<string> folderList)
    {
        if (!Program.Config.ProcessOptions.DeleteEmptyFolders)
        {
            return true;
        }

        Log.Logger.Information("Deleting empty folders ...");

        // Delete all empty folders
        int totalDeleted = 0;
        try
        { 
            folderList.AsParallel()
                .WithDegreeOfParallelism(Program.Options.ThreadCount)
                .WithCancellation(Program.CancelToken())
                .ForAll(folder =>
                {
                    int deleted = 0;
                    Log.Logger.Information("Looking for empty folders in {Folder}", folder);
                    // Ignore errors
                    FileEx.DeleteEmptyDirectories(folder, ref deleted);
                    Interlocked.Add(ref totalDeleted, deleted);
                });
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            // Error
            return false;
        }

        Log.Logger.Information("Deleted folders : {Deleted}", totalDeleted);

        return true;
    }

    public bool ProcessFiles(List<string> fileList)
    {
        // Log active options
        Log.Logger.Information("Process Options: TestSnippets: {TestSnippets}, TestNoModify: {TestNoModify}, ReProcess: {ReProcess}, ReVerify: {ReVerify}, FileIgnoreList: {FileIgnoreList}",
                               Program.Options.TestSnippets,
                               Program.Options.TestNoModify,
                               Program.Options.ReProcess,
                               Program.Options.ReVerify,
                               Program.Config.ProcessOptions.FileIgnoreList.Count);

        // Ignore count before
        int ignoreCount = FileIgnoreList.Count;

        // Process all the files
        List<ProcessTuple> errorInfo = new();
        List<ProcessTuple> modifiedInfo = new();
        List<ProcessTuple> failedInfo = new();
        var lockObject = new Object();
        bool ret = ProcessFilesDriver(fileList, "Process", fileName =>
        {
            // Process the file
            bool processResult = ProcessFile(fileName, out bool modified, out SidecarFile.States state, out string processName);
            if (!processResult &&
                Program.IsCancelled())
            {
                // Cancelled
                return false;
            }

            // Error
            if (!processResult)
            { 
                lock (lockObject)
                { 
                    errorInfo.Add(new ProcessTuple(processName, state));
                }
            }

            // Modified
            if (modified)
            {
                lock (lockObject)
                { 
                    modifiedInfo.Add(new ProcessTuple(processName, state));
                }
            }
            
            // Verify failed
            if (state.HasFlag(SidecarFile.States.VerifyFailed))
            {
                lock (lockObject)
                { 
                    failedInfo.Add(new ProcessTuple(processName, state));
                }

                // Add the failed file to the ignore list
                if (Program.Config.VerifyOptions.RegisterInvalidFiles)
                {
                    lock (lockObject)
                    { 
                        Program.Config.ProcessOptions.AddIgnoreEntry(processName);
                    }
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
    private bool ProcessFile(string fileName, out bool modified, out SidecarFile.States state, out string processName)
    {
        // Init
        modified = false;
        state = SidecarFile.States.None;
        processName = fileName;
        ProcessFile processFile = null;
        bool result;

        // Process in jump loop
        for (; ; )
        {
            // Skip the file if it is in the ignore list
            if (FileIgnoreList.Contains(fileName))
            {
                Log.Logger.Warning("Skipping ignored file : {FileName}", fileName);
                result = true;
                break;
            }

            // Does the file still exist
            if (!File.Exists(fileName))
            {
                Log.Logger.Warning("Skipping missing file : {FileName}", fileName);
                result = false;
                break;
            }

            // Create file processor to hold state
            processFile = new ProcessFile(fileName);
            DateTime lastWriteTime = processFile.FileInfo.LastWriteTimeUtc;

            // Skip the file if the extension is in the keep extensions list
            if (KeepExtensions.Contains(processFile.FileInfo.Extension))
            {
                Log.Logger.Warning("Skipping keep extensions file : {FileName}", fileName);
                result = true;
                break;
            }

            // Is the file writeable
            if (!processFile.IsWriteable())
            {
                Log.Logger.Error("Skipping read-only file : {FileName}", fileName);
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
            if (SidecarFile.IsSidecarFile(processFile.FileInfo))
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
            if (processFile.IsSidecarAvailable() &&
                !processFile.IsSidecarWriteable())
            {
                Log.Logger.Error("Skipping media file due to read-only sidecar file : {FileName}", fileName);
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

            // Re-verify and repair media in VerifyFailed state
            if (!processFile.SetReVerifyState() ||
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
            // Save the state but do not break yet, if file was modified cleanup could still happen
            bool verified = processFile.Verify(ref modified);
            if (Program.IsCancelled())
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
            result = verified;
            break;
        }

        // Return current state and fileinfo
        if (processFile != null)
        {
            state = processFile.State;
            processName = processFile.FileInfo.FullName;
        }
        return result;
    }

    public bool ReMuxFiles(List<string> fileList)
    {
        return ProcessFilesDriver(fileList, "ReMux", fileName =>
        {
            // Handle only MKV files, and files in the remux extension list
            if (!MkvMergeTool.IsMkvFile(fileName) &&
                !ReMuxExtensions.Contains(Path.GetExtension(fileName)))
            {
                return true;
            }

            // ReMux
            return Convert.ReMuxToMkv(fileName, out string _);
        });
    }

    public static bool ReEncodeFiles(List<string> fileList)
    {
        return ProcessFilesDriver(fileList, "ReEncode", fileName =>
        {
            // Handle only MKV files
            // ReMux before re-encode, so the track attribute logic works as expected
            if (!MkvMergeTool.IsMkvFile(fileName))
            {
                return true;
            }

            // Re-encode
            return Convert.ConvertToMkv(fileName, out string _);
        });
    }

    public static bool DeInterlaceFiles(List<string> fileList)
    {
        return ProcessFilesDriver(fileList, "DeInterlace", fileName =>
        {
            // Handle only MKV files
            if (!MkvMergeTool.IsMkvFile(fileName))
            {
                return true;
            }

            // Deinterlace
            return Convert.DeInterlaceToMkv(fileName, out string _);
        });
    }

    public static bool GetTagMapFiles(List<string> fileList)
    {
        // Create a dictionary of ffprobe to mkvmerge and mediainfo tag strings
        TagMapDictionary fftags = new();
        TagMapDictionary mktags = new();
        TagMapDictionary mitags = new();
        var lockObject = new Object();
        if (!ProcessFilesDriver(fileList, "Create Tag Map", fileName =>
            {
                // Handle only MKV files
                if (!MkvMergeTool.IsMkvFile(fileName))
                {
                    return true;
                }

                // Get media information
                ProcessFile processFile = new(fileName);
                if (!processFile.GetMediaInfo())
                {
                    return false;
                }

                // Add all the tags
                lock (lockObject)
                { 
                    fftags.Add(processFile.FfProbeInfo, processFile.MkvMergeInfo, processFile.MediaInfoInfo);
                    mktags.Add(processFile.MkvMergeInfo, processFile.FfProbeInfo, processFile.MediaInfoInfo);
                    mitags.Add(processFile.MediaInfoInfo, processFile.FfProbeInfo, processFile.MkvMergeInfo);
                }

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

    public static bool CreateSidecarFiles(List<string> fileList)
    {
        return ProcessFilesDriver(fileList, "Create Sidecar Files", fileName =>
        {
            // Handle only MKV files
            if (!MkvMergeTool.IsMkvFile(fileName))
            {
                return true;
            }

            // Create the sidecar file
            SidecarFile sidecarfile = new(fileName);
            return sidecarfile.Create();
        });
    }

    public static bool GetSidecarFiles(List<string> fileList)
    {
        return ProcessFilesDriver(fileList, "Get Sidecar Information", fileName =>
        {
            // Handle only sidecar files
            if (!SidecarFile.IsSidecarFile(fileName))
            {
                return true;
            }

            // Get sidecar information
            SidecarFile sidecarfile = new(fileName);
            if (!sidecarfile.Read())
            {
                return false;
            }

            // Print info
            sidecarfile.WriteLine();

            return true;
        });
    }

    public static bool GetMediaInfoFiles(List<string> fileList)
    {
        return ProcessFilesDriver(fileList, "Get Media Information", fileName =>
        {
            // Handle only MKV files
            if (!MkvMergeTool.IsMkvFile(fileName))
            {
                return true;
            }

            // Get media information
            ProcessFile processFile = new(fileName);
            if (!processFile.GetMediaInfo())
            {
                return false;
            }

            // Print info
            Log.Logger.Information("{FileName}", fileName);
            processFile.FfProbeInfo.WriteLine("FfProbe");
            processFile.MkvMergeInfo.WriteLine("MkvMerge");
            processFile.MediaInfoInfo.WriteLine("MediaInfo");

            return true;
        });
    }

    public static bool GetToolInfoFiles(List<string> fileList)
    {
        return ProcessFilesDriver(fileList, "Get Tool Information", fileName =>
        {
            // Skip sidecar files
            if (SidecarFile.IsSidecarFile(fileName))
            {
                return true;
            }

            // Get tool information
            // Read the tool info text
            if (!Tools.MediaInfo.GetMediaInfoXml(fileName, out string mediaInfoXml) ||
                !Tools.MkvMerge.GetMkvInfoJson(fileName, out string mkvMergeInfoJson) ||
                !Tools.FfProbe.GetFfProbeInfoJson(fileName, out string ffProbeInfoJson))
            {
                Log.Logger.Error("Failed to read tool info : {FileName}", fileName);
                return false;
            }

            // Print and log info
            Log.Logger.Information("{FileName}", fileName);
            Log.Logger.Information("FfProbe: {FfProbeText}", ffProbeInfoJson);
            Console.Write(ffProbeInfoJson);
            Log.Logger.Information("MkvMerge: {MkvMergeText}", mkvMergeInfoJson);
            Console.Write(mkvMergeInfoJson);
            Log.Logger.Information("MediaInfo: {MediaInfoText}", mediaInfoXml);
            Console.Write(mediaInfoXml);

            return true;
        });
    }

    public static bool RemoveSubtitlesFiles(List<string> fileList)
    {
        return ProcessFilesDriver(fileList, "Remove Subtitles", fileName =>
        {
            // Handle only MKV files
            if (!MkvMergeTool.IsMkvFile(fileName))
            {
                return true;
            }

            // Get media information
            ProcessFile processFile = new(fileName);
            if (!processFile.GetMediaInfo())
            {
                return false;
            }

            // Remove subtitles
            bool modified = false;
            return processFile.RemoveSubtitles(ref modified);
        });
    }

    private static bool ProcessFilesDriver(IReadOnlyCollection<string> fileList, string taskName, Func<string, bool> taskFunc)
    {
        // Start
        Log.Logger.Information("Starting {TaskName}, processing {Count} files ...", taskName, fileList.Count);
        Stopwatch timer = new();
        timer.Start();

        // Process all files in parallel
        int totalCount = fileList.Count;
        int processedCount = 0;
        int errorCount = 0;
        try
        {
            // Group files by path ignoring extensions
            // This prevents files with the same name being modified by different threads
            // E.g. when remuxing from AVI to MKV, or when testing for existance of MKV for SideCar files
            var groupedFiles = fileList.GroupBy(path => Path.ChangeExtension(path, null), StringComparer.OrdinalIgnoreCase);

            // Use a single item partitioner
            // This prevents a long running task in one thread from starving outstanding work that is assigned to the same thread
            // E.g. a long running FFmpeg task with waiting tasks that could have been completed on the idle threads
            var partitioner = Partitioner.Create(groupedFiles, EnumerablePartitionerOptions.NoBuffering);

            // Process groups in parallel
            partitioner.AsParallel()
                .WithDegreeOfParallelism(Program.Options.ThreadCount)
                .WithCancellation(Program.CancelToken())
                .ForAll(keyPair =>
            {
                // Process all files in the group in this thread
                foreach (string fileName in keyPair)
                { 
                    // Log completion % before task starts
                    double processedPercentage = GetPercentage(Interlocked.CompareExchange(ref processedCount, 0, 0), totalCount);
                    Log.Logger.Information("{TaskName} ({Processed:N2}%) Before : {FileName}", taskName, processedPercentage, fileName);

                    // Perform the task
                    if (!taskFunc(fileName) &&
                        !Program.IsCancelled())
                    {
                        // Error
                        Log.Logger.Error("{TaskName} Error : {FileName}", taskName, fileName);
                        Interlocked.Increment(ref errorCount);
                    }

                    // Log completion % after task completes
                    processedPercentage = GetPercentage(Interlocked.Increment(ref processedCount), totalCount);
                    Log.Logger.Information("{TaskName} ({Processed:N2}%) After : {FileName}", taskName, processedPercentage, fileName);
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            // Error
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

    private static double GetPercentage(int dividend, int divisor)
    {
        // Calculate double digit precision avoiding 100% until really complete
        if (dividend == 0)
        {
            return 0.0;
        }
        if (dividend == divisor)
        {
            return 100.0;
        }
        double percentage = System.Convert.ToDouble(dividend) / System.Convert.ToDouble(divisor) * 100.0;
        percentage = Math.Round(percentage, 2);
        if (percentage.Equals(100.0))
        { 
            percentage = 99.99;
        }
        return percentage;
    }

    private readonly HashSet<string> FileIgnoreList;
    private readonly HashSet<string> KeepExtensions;
    private readonly HashSet<string> ReMuxExtensions;
    private readonly HashSet<string> ReEncodeAudioFormats;
    private readonly HashSet<string> KeepLanguages;
    private readonly List<string> PreferredAudioFormats;
    private readonly List<VideoInfo> ReEncodeVideoInfos;
}
