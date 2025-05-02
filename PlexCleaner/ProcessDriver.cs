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

public static class ProcessDriver
{
    public static bool GetFiles(
        List<string> mediaFiles,
        out List<string> directoryList,
        out List<string> fileList
    )
    {
        // Init
        directoryList = [];
        fileList = [];

        // Trim quotes around input paths
        mediaFiles = [.. mediaFiles.Select(file => file.Trim('"'))];

        Log.Information("Creating file and folder list ...");

        bool error = false;
        List<string> localDirectoryList = [];
        List<string> localFileList = [];
        try
        {
            // No need for concurrent collections, number of items are small, and added in bulk, just lock when adding results
            Lock listLock = new();

            // Process each input in parallel
            mediaFiles
                .AsParallel()
                .WithDegreeOfParallelism(Program.Options.ThreadCount)
                .WithCancellation(Program.CancelToken())
                .ForAll(fileOrFolder =>
                {
                    // Handle cancel request
                    Program.CancelToken().ThrowIfCancellationRequested();

                    // Test if input is a file or a directory
                    FileAttributes fileAttributes = File.GetAttributes(fileOrFolder);
                    if (fileAttributes.HasFlag(FileAttributes.Directory))
                    {
                        // Add this item as directory
                        lock (listLock)
                        {
                            localDirectoryList.Add(fileOrFolder);
                        }

                        // Create the file list from the directory
                        Log.Information("Enumerating files in {Directory} ...", fileOrFolder);
                        if (
                            !FileEx.EnumerateDirectory(
                                fileOrFolder,
                                out List<FileInfo> fileInfoList,
                                out _
                            )
                        )
                        {
                            // Abort
                            Log.Error(
                                "Failed to enumerate files in directory {Directory}",
                                fileOrFolder
                            );
                            Program.Cancel();
                            Program.CancelToken().ThrowIfCancellationRequested();
                        }

                        // Add files to file list
                        lock (listLock)
                        {
                            fileInfoList.ForEach(item => localFileList.Add(item.FullName));
                        }
                    }
                    else
                    {
                        // Add this as a file
                        lock (listLock)
                        {
                            localFileList.Add(fileOrFolder);
                        }
                    }
                });
        }
        catch (OperationCanceledException)
        {
            // Cancelled
            error = true;
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            // Error
            error = true;
        }

        // Assign results
        directoryList = localDirectoryList;
        fileList = localFileList;

        // Report
        Log.Information(
            "Discovered {FileCount} files from {DirectoryCount} directories",
            fileList.Count,
            directoryList.Count
        );

        return !error;
    }

    public static bool ProcessFiles(
        List<string> fileList,
        string taskName,
        bool mkvFilesOnly,
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
        bool error = false;
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
                            "{TaskName} ({Processed:F2}%) Before : {FileName}",
                            taskName,
                            processedPercentage,
                            fileName
                        );

                        // Skip non-MKV files
                        if (mkvFilesOnly && !SidecarFile.IsMkvFile(fileName))
                        {
                            Log.Verbose(
                                "{TaskName} Skipped non-MKV file : {FileName}",
                                taskName,
                                fileName
                            );
                            continue;
                        }

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
                            "{TaskName} ({Processed:F2}%) After : {FileName}",
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
            error = true;
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
        {
            // Error
            error = true;
        }

        // Stop the timer
        timer.Stop();

        // Done
        Log.Information("Completed {TaskName}", taskName);
        Log.Information("Processing time : {Elapsed}", timer.Elapsed);
        Log.Information("Total files : {Count}", totalCount);
        Log.Information("Error files : {Count}", errorCount);

        return !error;
    }

    public static bool GetTagMap(List<string> fileList)
    {
        // Create a dictionary of ffprobe to mkvmerge and mediainfo tag strings
        TagMapSet ffTags = new();
        TagMapSet mkTags = new();
        TagMapSet miTags = new();
        Lock tagLock = new();
        bool ret = ProcessFiles(
            fileList,
            nameof(GetTagMap),
            true,
            fileName =>
            {
                // Get media information
                ProcessFile processFile = new(fileName);
                if (!processFile.GetMediaInfo())
                {
                    return false;
                }

                // TODO: Remove cover art in video tracks during load
                _ = processFile.MediaInfoInfo.Video.RemoveAll(track => track.IsCoverArt);
                _ = processFile.FfProbeInfo.Video.RemoveAll(track => track.IsCoverArt);
                _ = processFile.MkvMergeInfo.Video.RemoveAll(track => track.IsCoverArt);

                // Skip media with errors
                if (
                    processFile.MediaInfoInfo.HasErrors
                    || processFile.FfProbeInfo.HasErrors
                    || processFile.MkvMergeInfo.HasErrors
                )
                {
                    Log.Warning(
                        "Skipping media with errors : {FileName}",
                        processFile.FileInfo.Name
                    );
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

    public static bool GetMediaInfo(List<string> fileList) =>
        ProcessFiles(
            fileList,
            nameof(GetMediaInfo),
            true,
            fileName =>
            {
                // Get media information
                ProcessFile processFile = new(fileName);
                if (!processFile.GetMediaInfo())
                {
                    return false;
                }

                // Print info
                Log.Information("{FileName}", fileName);
                processFile.MediaInfoInfo.WriteLine();
                processFile.MkvMergeInfo.WriteLine();
                processFile.FfProbeInfo.WriteLine();

                return true;
            }
        );

    public static bool GetToolInfo(List<string> fileList) =>
        ProcessFiles(
            fileList,
            nameof(GetToolInfo),
            true,
            fileName =>
            {
                // Get media tool information
                if (
                    !Tools.MediaInfo.GetMediaInfoXml(fileName, out string mediaInfoXml)
                    || !Tools.MkvMerge.GetMkvInfoJson(fileName, out string mkvMergeInfoJson)
                    || !Tools.FfProbe.GetFfProbeInfoJson(fileName, out string ffProbeInfoJson)
                )
                {
                    Log.Error("Failed to read media tool info : {FileName}", fileName);
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
