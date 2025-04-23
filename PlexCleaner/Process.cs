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

public static class Process
{
    private static bool ProcessFile(
        string fileName,
        out bool modified,
        out SidecarFile.StatesType state,
        out string processName
    )
    {
        // Init
        modified = false;
        state = SidecarFile.StatesType.None;
        processName = fileName;
        ProcessFile processFile = null;
        bool result;

        // Process in jump loop
        for (; ; )
        {
            // Skip the file if it is in the ignore lists
            if (
                Program.Config.ProcessOptions.FileIgnoreList.Contains(fileName)
                || Program.Config.ProcessOptions.IsFileIgnoreMatch(fileName)
            )
            {
                Log.Warning("Skipping ignored file : {FileName}", fileName);
                // Ok
                result = true;
                break;
            }

            // Does the file exist and have access permissions
            if (!File.Exists(fileName))
            {
                Log.Error("Skipping inaccessible file : {FileName}", fileName);
                // Error
                result = false;
                break;
            }

            // Create file processor to hold state
            processFile = new ProcessFile(fileName);
            DateTime lastWriteTime = processFile.FileInfo.LastWriteTimeUtc;

            // Is the file writeable
            if (!processFile.IsWriteable())
            {
                Log.Error("Skipping read-only file : {FileName}", fileName);
                // Error
                result = false;
                break;
            }

            // Delete the sidecar file if matching MKV file not found
            if (!processFile.DeleteMismatchedSidecarFile(ref modified))
            {
                // Error
                result = false;
                break;
            }

            // Skip if this is a sidecar file
            if (SidecarFile.IsSidecarFile(processFile.FileInfo))
            {
                // Error
                result = true;
                break;
            }

            // ReMux non-MKV containers matched by extension
            // Conditional on ReMux option
            if (!processFile.RemuxByExtensions(ref modified) || Program.IsCancelled())
            {
                // Error
                result = false;
                break;
            }

            // Delete or skip non-MKV files
            // Conditional on DeleteUnwantedExtensions option
            if (!processFile.DeleteNonMkvFile(ref modified))
            {
                // Ok
                result = true;
                break;
            }

            // All files past this point are MKV files
            Debug.Assert(MkvMergeTool.IsMkvFile(processFile.FileInfo));

            // If a sidecar file exists for this MKV file it must be writable
            if (processFile.IsSidecarAvailable() && !processFile.IsSidecarWriteable())
            {
                Log.Error(
                    "Skipping media file due to read-only sidecar file : {FileName}",
                    fileName
                );
                // Error
                result = false;
                break;
            }

            // Make sure the file extension is lowercase for case sensitive filesystems
            // Always changes extension to lowercase
            if (!processFile.MakeExtensionLowercase(ref modified))
            {
                // Error
                result = false;
                break;
            }

            // Read the media info
            if (!processFile.GetMediaInfo() || Program.IsCancelled())
            {
                // Error
                result = false;
                break;
            }

            // If ReVerify is set reset the VerifyFailed and RepairFailed states
            if (!processFile.SetReVerifyState() || Program.IsCancelled())
            {
                // Error
                result = false;
                break;
            }

            // ReMux non-MKV containers using MKV file extensions
            // Conditional on ReMux option, fails if not Matroska and ReMux is not enabled
            if (!processFile.RemuxNonMkvContainer(ref modified) || Program.IsCancelled())
            {
                // Error
                result = false;
                break;
            }

            // ReMux to remove extra video tracks
            // Conditional on ReMux option, fails if more than one video track and ReMux not enabled
            if (!processFile.RemuxRemoveExtraVideoTracks(ref modified) || Program.IsCancelled())
            {
                // Error
                result = false;
                break;
            }

            // Verify track counts are supported
            // No more than one video track, audio or video track
            if (!processFile.VerifyTrackCounts() || Program.IsCancelled())
            {
                // Error
                result = false;
                break;
            }

            // Remove all cover art attachments or video tracks that interfere with processing logic
            // Conditional on ReMux option, fails if cover art is present and ReMux not enabled
            if (!processFile.RemoveCoverArt(ref modified) || Program.IsCancelled())
            {
                // Error
                result = false;
                break;
            }

            // Remove tags, titles, and attachments
            // Conditional on RemoveTags option
            if (!processFile.RemoveTags(ref modified) || Program.IsCancelled())
            {
                // Error
                result = false;
                break;
            }

            // Test that the file extension and container type is MKV and all media info should be valid
            if (!processFile.VerifyMediaInfo())
            {
                // Error
                result = false;
                break;
            }

            // Repair tracks with metadata errors
            // Conditional on AutoRepair option
            if (!processFile.RepairMetadataErrors(ref modified) || Program.IsCancelled())
            {
                // Error
                result = false;
                break;
            }

            // Remove EIA-EIA-608 and CTA-708 Closed Captions from the video stream
            // Conditional on RemoveClosedCaptions option
            if (!processFile.RemoveClosedCaptions(ref modified) || Program.IsCancelled())
            {
                // Error
                result = false;
                break;
            }

            // Deinterlace interlaced content
            // Conditional on DeInterlace option
            if (!processFile.DeInterlace(ref modified) || Program.IsCancelled())
            {
                // Error
                result = false;
                break;
            }

            // Change all tracks with an unknown language to the default language
            // Conditional on SetUnknownLanguage option
            if (!processFile.SetUnknownLanguageTracks(ref modified) || Program.IsCancelled())
            {
                // Error
                result = false;
                break;
            }

            // Remove all the unwanted language tracks
            // Conditional on RemoveUnwantedLanguageTracks option
            if (!processFile.RemoveUnwantedLanguageTracks(ref modified) || Program.IsCancelled())
            {
                // Error
                result = false;
                break;
            }

            // TODO: RemoveUnwantedLanguageTracks() and RemoveDuplicateTracks() both remux, are logically separate,
            // but could be combined to complete in one remux operation

            // Remove all the duplicate tracks
            // Conditional on RemoveDuplicateTracks option
            if (!processFile.RemoveDuplicateTracks(ref modified) || Program.IsCancelled())
            {
                // Error
                result = false;
                break;
            }

            // Re-Encode formats that cannot be direct-played
            // Conditional on ReEncode option
            if (!processFile.ReEncode(ref modified) || Program.IsCancelled())
            {
                // Error
                result = false;
                break;
            }

            // Verify media streams, and repair if possible
            // Conditional on Verify and AutoRepair options
            if (!processFile.VerifyAndRepair(ref modified) || Program.IsCancelled())
            {
                // Error
                result = false;
                break;
            }

            // FfMpeg or HandBrake could undo the previous cleanup, repeat cleanup
            if (
                !processFile.RepairMetadataErrors(ref modified)
                || !processFile.SetUnknownLanguageTracks(ref modified)
                || !processFile.RemoveTags(ref modified)
                || Program.IsCancelled()
            )
            {
                // Error
                result = false;
                break;
            }

            // Restore the file timestamp
            if (!processFile.SetLastWriteTimeUtc(lastWriteTime) || Program.IsCancelled())
            {
                // Error
                result = false;
                break;
            }

            // Re-verify the tool info is correctly recorded
            if (!processFile.VerifyMediaInfo())
            {
                // Error
                result = false;
                break;
            }

            // Success
            result = true;
            break;
        }

        // Update state if we opened a file for processing
        if (processFile != null)
        {
            // Delete files that failed processing
            // Conditional on DeleteInvalidFiles
            if (!result)
            {
                _ = processFile.DeleteFailedFile();
            }

            // Return current state and file info
            state = processFile.State;
            processName = processFile.FileInfo.FullName;
        }
        return result;
    }

    public static bool ProcessFolders(List<string> folderList)
    {
        // Create the file and directory list
        if (!FileEx.EnumerateDirectories(folderList, out List<FileInfo> fileInfoList, out _))
        {
            return false;
        }

        // Process the files
        List<string> fileList = [.. fileInfoList.Select(item => item.FullName)];
        return ProcessFiles(fileList);
    }

    public static bool DeleteEmptyFolders(IEnumerable<string> folderList)
    {
        // Conditional
        if (!Program.Config.ProcessOptions.DeleteEmptyFolders)
        {
            return true;
        }

        Log.Information("Deleting empty folders ...");

        // Delete all empty folders
        bool fatalError = false;
        int totalDeleted = 0;
        try
        {
            folderList
                .AsParallel()
                .WithDegreeOfParallelism(Program.Options.ThreadCount)
                .WithCancellation(Program.CancelToken())
                .ForAll(folder =>
                {
                    // Handle cancel request
                    Program.CancelToken().ThrowIfCancellationRequested();

                    // Delete empty folders
                    int deleted = 0;
                    Log.Information("Looking for empty folders in {Folder}", folder);
                    _ = FileEx.DeleteEmptyDirectories(folder, ref deleted);
                    _ = Interlocked.Add(ref totalDeleted, deleted);
                });
        }
        catch (OperationCanceledException)
        {
            // Cancelled
            fatalError = true;
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            // Error
            fatalError = true;
        }

        Log.Information("Deleted folders : {Deleted}", totalDeleted);

        return !fatalError;
    }

    public static bool ProcessFiles(List<string> fileList)
    {
        // Log active options
        Log.Information(
            "Process Options: TestSnippets: {TestSnippets}, TestNoModify: {TestNoModify}, ReVerify: {ReVerify}, FileIgnoreList: {FileIgnoreList}",
            Program.Options.TestSnippets,
            Program.Options.TestNoModify,
            Program.Options.ReVerify,
            Program.Config.ProcessOptions.FileIgnoreList.Count
        );

        // Process all the files
        ProcessResultJsonSchema resultsJson = new();
        Lock resultLock = new();
        bool ret = ProcessFilesDriver(
            fileList,
            "Process",
            fileName =>
            {
                // Process the file
                bool processResult = ProcessFile(
                    fileName,
                    out bool modified,
                    out SidecarFile.StatesType state,
                    out string processName
                );

                // Cancelled
                if (Program.IsCancelled())
                {
                    return false;
                }

                // Save results
                lock (resultLock)
                {
                    resultsJson.Results.Add(
                        new()
                        {
                            Result = processResult,
                            OriginalFileName = fileName,
                            NewFileName = processName,
                            Modified = modified,
                            State = state,
                        }
                    );
                }

                return processResult;
            }
        );

        // Errors
        // Log.Information("Error files : {Count}", errorCount);
        List<ProcessResultJsonSchema.ProcessResult> errorResults =
        [
            .. resultsJson.Results.Where(item => !item.Result),
        ];
        errorResults.ForEach(item =>
            Log.Information("Error: {State} : {FileName}", item.State, item.NewFileName)
        );

        // Modified
        List<ProcessResultJsonSchema.ProcessResult> modifiedResults =
        [
            .. resultsJson.Results.Where(item => item.Modified),
        ];
        Log.Information("Modified files : {Count}", modifiedResults.Count);
        modifiedResults.ForEach(item =>
            Log.Information("Modified: {State} : {FileName}", item.State, item.NewFileName)
        );

        // Verify failed
        List<ProcessResultJsonSchema.ProcessResult> failedResults =
        [
            .. resultsJson.Results.Where(item =>
                item.State.HasFlag(SidecarFile.StatesType.VerifyFailed)
            ),
        ];
        Log.Information("VerifyFailed files : {Count}", failedResults.Count);
        failedResults.ForEach(item =>
            Log.Information("VerifyFailed: {State} : {FileName}", item.State, item.NewFileName)
        );

        // Updated ignore file list
        if (Program.Config.VerifyOptions.RegisterInvalidFiles)
        {
            // Add all failed items to the ignore list
            bool newItems = false;
            foreach (ProcessResultJsonSchema.ProcessResult item in failedResults)
            {
                if (Program.Config.ProcessOptions.FileIgnoreList.Add(item.NewFileName))
                {
                    // New item added
                    newItems = true;
                }
            }
            if (newItems)
            {
                Log.Information(
                    "Updating FileIgnoreList entries ({Count}) in settings file : {SettingsFile}",
                    Program.Config.ProcessOptions.FileIgnoreList.Count,
                    Program.Options.SettingsFile
                );
                ConfigFileJsonSchema.ToFile(Program.Options.SettingsFile, Program.Config);
            }
        }

        // Write the process results to file
        if (Program.Options.ResultsFile != null)
        {
            // Sort by file name to simplify comparison with previous results
            resultsJson.Results.Sort(
                (x, y) =>
                    string.Compare(x.OriginalFileName, y.OriginalFileName, StringComparison.Ordinal)
            );
            resultsJson.SetVersionInfo();
            Log.Information(
                "Writing results file : {Program.Options.ResultFile}",
                Program.Options.ResultsFile
            );
            ProcessResultJsonSchema.ToFile(Program.Options.ResultsFile, resultsJson);
        }

        return ret;
    }

    public static bool ReMuxFiles(List<string> fileList) =>
        ProcessFilesDriver(
            fileList,
            "ReMux",
            fileName =>
            {
                // Handle only MKV files, and files in the remux extension list
                if (
                    !MkvMergeTool.IsMkvFile(fileName)
                    && !Program.Config.ProcessOptions.ReMuxExtensions.Contains(
                        Path.GetExtension(fileName)
                    )
                )
                {
                    return true;
                }

                // ReMux
                return Convert.ReMuxToMkv(fileName, out string _);
            }
        );

    public static bool ReEncodeFiles(List<string> fileList) =>
        ProcessFilesDriver(
            fileList,
            "ReEncode",
            fileName =>
            {
                // Handle only MKV files
                // ReMux before re-encode, so the track attribute logic works as expected
                if (!MkvMergeTool.IsMkvFile(fileName))
                {
                    return true;
                }

                // Re-encode
                return Convert.ConvertToMkv(fileName, out string _);
            }
        );

    public static bool DeInterlaceFiles(List<string> fileList) =>
        ProcessFilesDriver(
            fileList,
            "DeInterlace",
            fileName =>
            {
                // Handle only MKV files
                if (!MkvMergeTool.IsMkvFile(fileName))
                {
                    return true;
                }

                // Deinterlace
                return Convert.DeInterlaceToMkv(fileName, out string _);
            }
        );

    public static bool VerifyFiles(List<string> fileList) =>
        ProcessFilesDriver(
            fileList,
            "Verify",
            fileName =>
            {
                // Handle only MKV files
                if (!MkvMergeTool.IsMkvFile(fileName))
                {
                    return true;
                }

                // Verify media streams
                // Track count, bitrate, and HDR profiles are not evaluated here
                return PlexCleaner.ProcessFile.VerifyMediaStreams(new FileInfo(fileName));
            }
        );

    public static bool GetTagMapFiles(List<string> fileList)
    {
        // Create a dictionary of ffprobe to mkvmerge and mediainfo tag strings
        TagMapSet ffTags = new();
        TagMapSet mkTags = new();
        TagMapSet miTags = new();
        Lock tagLock = new();
        bool ret = ProcessFilesDriver(
            fileList,
            "Create Tag Map",
            fileName =>
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
                lock (tagLock)
                {
                    ffTags.Add(
                        processFile.FfProbeInfo,
                        processFile.MkvMergeInfo,
                        processFile.MediaInfoInfo
                    );
                    mkTags.Add(
                        processFile.MkvMergeInfo,
                        processFile.FfProbeInfo,
                        processFile.MediaInfoInfo
                    );
                    miTags.Add(
                        processFile.MediaInfoInfo,
                        processFile.FfProbeInfo,
                        processFile.MkvMergeInfo
                    );
                }

                return true;
            }
        );
        if (!ret)
        {
            return false;
        }

        // Print the tags
        Log.Information("FfProbe:");
        ffTags.WriteLine();
        Log.Information("MkvMerge:");
        mkTags.WriteLine();
        Log.Information("MediaInfo:");
        miTags.WriteLine();

        return true;
    }

    public static bool CreateSidecarFiles(List<string> fileList) =>
        ProcessFilesDriver(
            fileList,
            "Create Sidecar Files",
            fileName =>
            {
                // Handle only MKV files
                if (!MkvMergeTool.IsMkvFile(fileName))
                {
                    return true;
                }

                // Create new or overwrite existing sidecar file
                return SidecarFile.Create(fileName);
            }
        );

    public static bool GetSidecarFiles(List<string> fileList) =>
        ProcessFilesDriver(
            fileList,
            "Get Sidecar Information",
            fileName =>
            {
                // Handle only MKV files
                if (!MkvMergeTool.IsMkvFile(fileName))
                {
                    return true;
                }

                // Print info
                return SidecarFile.PrintInformation(fileName);
            }
        );

    public static bool UpdateSidecarFiles(List<string> fileList) =>
        ProcessFilesDriver(
            fileList,
            "Update Sidecar Files",
            fileName =>
            {
                // Handle only MKV files
                if (!MkvMergeTool.IsMkvFile(fileName))
                {
                    return true;
                }

                // Create new or update existing sidecar file
                return SidecarFile.Update(fileName);
            }
        );

    public static bool GetMediaInfoFiles(List<string> fileList) =>
        ProcessFilesDriver(
            fileList,
            "Get Media Information",
            fileName =>
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
                Log.Information("{FileName}", fileName);
                processFile.FfProbeInfo.WriteLine("FfProbe");
                processFile.MkvMergeInfo.WriteLine("MkvMerge");
                processFile.MediaInfoInfo.WriteLine("MediaInfo");

                return true;
            }
        );

    public static bool GetToolInfoFiles(List<string> fileList) =>
        ProcessFilesDriver(
            fileList,
            "Get Tool Information",
            fileName =>
            {
                // Skip sidecar files
                if (SidecarFile.IsSidecarFile(fileName))
                {
                    return true;
                }

                // Get tool information
                // Read the tool info text
                if (
                    !Tools.MediaInfo.GetMediaInfoXml(fileName, out string mediaInfoXml)
                    || !Tools.MkvMerge.GetMkvInfoJson(fileName, out string mkvMergeInfoJson)
                    || !Tools.FfProbe.GetFfProbeInfoJson(fileName, out string ffProbeInfoJson)
                )
                {
                    Log.Error("Failed to read tool info : {FileName}", fileName);
                    return false;
                }

                // Print and log info
                Log.Information("{FileName}", fileName);
                Log.Information("FfProbe: {FfProbeText}", ffProbeInfoJson);
                Console.Write(ffProbeInfoJson);
                Log.Information("MkvMerge: {MkvMergeText}", mkvMergeInfoJson);
                Console.Write(mkvMergeInfoJson);
                Log.Information("MediaInfo: {MediaInfoText}", mediaInfoXml);
                Console.Write(mediaInfoXml);

                return true;
            }
        );

    public static bool RemoveSubtitlesFiles(List<string> fileList) =>
        ProcessFilesDriver(
            fileList,
            "Remove Subtitles",
            fileName =>
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
            }
        );

    public static bool ProcessFilesDriver(
        List<string> fileList,
        string taskName,
        Func<string, bool> taskFunc
    )
    {
        // Start
        Log.Information(
            "Starting {TaskName}, processing {Count} files ...",
            taskName,
            fileList.Count
        );
        Stopwatch timer = new();
        timer.Start();

        // Process all files in parallel
        int totalCount = fileList.Count;
        int processedCount = 0;
        int errorCount = 0;
        bool fatalError = false;
        try
        {
            // Group files by path ignoring extensions
            // This prevents files with the same name being modified by different threads
            // E.g. when remuxing from AVI to MKV, or when testing for existence of MKV for Sidecar files
            IEnumerable<IGrouping<string, string>> groupedFiles = fileList.GroupBy(
                path => Path.ChangeExtension(path, null),
                StringComparer.OrdinalIgnoreCase
            );

            // Use a single item partitioner
            // This prevents a long running task in one thread from starving outstanding work that is assigned to the same thread
            // E.g. a long running FFmpeg task with waiting tasks that could have been completed on the idle threads
            OrderablePartitioner<IGrouping<string, string>> partitioner = Partitioner.Create(
                groupedFiles,
                EnumerablePartitionerOptions.NoBuffering
            );

            // Process groups in parallel
            partitioner
                .AsParallel()
                .WithDegreeOfParallelism(Program.Options.ThreadCount)
                .WithCancellation(Program.CancelToken())
                .ForAll(keyPair =>
                {
                    // Process all files in the group in this thread
                    foreach (string fileName in keyPair)
                    {
                        // Log completion % before task starts
                        double processedPercentage = GetPercentage(
                            Interlocked.CompareExchange(ref processedCount, 0, 0),
                            totalCount
                        );
                        Log.Information(
                            "{TaskName} ({Processed:N2}%) Before : {FileName}",
                            taskName,
                            processedPercentage,
                            fileName
                        );

                        // Perform the task
                        bool taskResult = taskFunc(fileName);

                        // Handle cancel request
                        Program.CancelToken().ThrowIfCancellationRequested();

                        // Handle result
                        if (!taskResult)
                        {
                            // Error
                            Log.Error("{TaskName} Error : {FileName}", taskName, fileName);
                            _ = Interlocked.Increment(ref errorCount);
                        }

                        // Log completion % after task completes
                        processedPercentage = GetPercentage(
                            Interlocked.Increment(ref processedCount),
                            totalCount
                        );
                        Log.Information(
                            "{TaskName} ({Processed:N2}%) After : {FileName}",
                            taskName,
                            processedPercentage,
                            fileName
                        );
                    }
                });
        }
        catch (OperationCanceledException)
        {
            // Cancelled
            fatalError = true;
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            // Error
            fatalError = true;
        }

        // Stop the timer
        timer.Stop();

        // Done
        Log.Information("Completed {TaskName}", taskName);
        Log.Information("Processing time : {Elapsed}", timer.Elapsed);
        Log.Information("Total files : {Count}", totalCount);
        Log.Information("Error files : {Count}", errorCount);

        return !fatalError;
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
        double percentage =
            System.Convert.ToDouble(dividend) / System.Convert.ToDouble(divisor) * 100.0;
        percentage = Math.Round(percentage, 2);
        if (percentage.Equals(100.0))
        {
            percentage = 99.99;
        }
        return percentage;
    }
}
