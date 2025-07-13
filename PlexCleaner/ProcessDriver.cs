using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
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

                        // Enumerate all files in the directory and its subdirectories
                        Log.Information("Enumerating files in {Directory} ...", fileOrFolder);
                        List<FileInfo> fileInfoList =
                        [
                            .. new DirectoryInfo(fileOrFolder).EnumerateFiles(
                                "*.*",
                                SearchOption.AllDirectories
                            ),
                        ];

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

                        // Skip non-MKV files
                        if (mkvFilesOnly && !SidecarFile.IsMkvFile(fileName))
                        {
                            processedPercentage = GetPercentage(
                                Interlocked.Increment(ref processedCount),
                                totalCount
                            );
                            Log.Information(
                                "{TaskName} ({Processed:F2}%) Skipping non-MKV file : {FileName}",
                                taskName,
                                processedPercentage,
                                fileName
                            );
                            continue;
                        }

                        Log.Information(
                            "{TaskName} ({Processed:F2}%) Before : {FileName}",
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
                if (!processFile.GetMediaProps())
                {
                    return false;
                }

                // Remove cover art in video tracks
                _ = processFile.MediaInfoProps.Video.RemoveAll(track => track.CoverArt);
                _ = processFile.FfProbeProps.Video.RemoveAll(track => track.CoverArt);
                _ = processFile.MkvMergeProps.Video.RemoveAll(track => track.CoverArt);

                // Skip media with errors
                if (
                    processFile.MediaInfoProps.HasErrors
                    || processFile.FfProbeProps.HasErrors
                    || processFile.MkvMergeProps.HasErrors
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
                        processFile.FfProbeProps,
                        processFile.MkvMergeProps,
                        processFile.MediaInfoProps
                    );
                    mkTags.Add(
                        processFile.MkvMergeProps,
                        processFile.FfProbeProps,
                        processFile.MediaInfoProps
                    );
                    miTags.Add(
                        processFile.MediaInfoProps,
                        processFile.FfProbeProps,
                        processFile.MkvMergeProps
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
                if (!processFile.GetMediaProps())
                {
                    return false;
                }

                // Print info
                Log.Information("{FileName}", fileName);
                processFile.MediaInfoProps.WriteLine();
                processFile.MkvMergeProps.WriteLine();
                processFile.FfProbeProps.WriteLine();

                return true;
            }
        );

    public static bool TestMediaInfo(List<string> fileList) =>
        ProcessFiles(
            fileList,
            nameof(TestMediaInfo),
            false,
            fileName =>
            {
                // Process MKV files or files in the Remux list
                FileInfo fileInfo = new(fileName);

                if (
                    !SidecarFile.IsMkvFile(fileName)
                    && !Program.Config.ProcessOptions.ReMuxExtensions.Contains(
                        Path.GetExtension(fileName)
                    )
                )
                {
                    return true;
                }

                // Get media information
                Log.Information("Reading media information : {FileName}", fileName);
                int ret = 0;
                if (Tools.MediaInfo.GetMediaProps(fileInfo.FullName, out MediaProps mediaInfoProps))
                {
                    mediaInfoProps.WriteLine();
                    ret++;
                }
                if (Tools.MkvMerge.GetMediaProps(fileInfo.FullName, out MediaProps mkvMergeProps))
                {
                    mkvMergeProps.WriteLine();
                    ret++;
                }
                if (Tools.FfProbe.GetMediaProps(fileInfo.FullName, out MediaProps ffProbeProps))
                {
                    ffProbeProps.WriteLine();
                    ret++;
                }
                if (ret != 3)
                {
                    return false;
                }

                // Skip further validation if any errors
                if (mediaInfoProps.HasErrors || ffProbeProps.HasErrors || mkvMergeProps.HasErrors)
                {
                    Log.Warning("Media metadata has errors : {File}", fileInfo.Name);
                    return true;
                }

                // Remove cover art in video tracks
                _ = mediaInfoProps.Video.RemoveAll(track => track.CoverArt);
                _ = ffProbeProps.Video.RemoveAll(track => track.CoverArt);
                _ = mkvMergeProps.Video.RemoveAll(track => track.CoverArt);

                // Do the track counts match
                if (
                    ffProbeProps.Audio.Count != mkvMergeProps.Audio.Count
                    || mkvMergeProps.Audio.Count != mediaInfoProps.Audio.Count
                    || ffProbeProps.Video.Count != mkvMergeProps.Video.Count
                    || mkvMergeProps.Video.Count != mediaInfoProps.Video.Count
                    || ffProbeProps.Subtitle.Count != mkvMergeProps.Subtitle.Count
                    || mkvMergeProps.Subtitle.Count != mediaInfoProps.Subtitle.Count
                )
                {
                    Log.Warning("Tool track count discrepancy : {File}", fileInfo.Name);
                }

                // If Matroska container then MkvMerge and MediaInfo track Uid's should match
                if (mkvMergeProps.IsContainerMkv())
                {
                    if (
                        mkvMergeProps.Video.Any(mkvItem =>
                            mediaInfoProps.Video.Find(mediaInfoItem =>
                                mediaInfoItem.Uid == mkvItem.Uid
                            ) == null
                        )
                    )
                    {
                        Log.Warning("MkvMerge video track Uid mismatch : {File}", fileInfo.Name);
                    }
                    if (
                        mkvMergeProps.Audio.Any(mkvItem =>
                            mediaInfoProps.Audio.Find(mediaInfoItem =>
                                mediaInfoItem.Uid == mkvItem.Uid
                            ) == null
                        )
                    )
                    {
                        Log.Warning("MkvMerge audio track Uid mismatch : {File}", fileInfo.Name);
                    }
                    if (
                        mkvMergeProps.Subtitle.Any(mkvItem =>
                            mediaInfoProps.Subtitle.Find(mediaInfoItem =>
                                mediaInfoItem.Uid == mkvItem.Uid
                            ) == null
                        )
                    )
                    {
                        Log.Warning("MkvMerge subtitle track Uid mismatch : {File}", fileInfo.Name);
                    }
                }

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
                    !Tools.MediaInfo.GetMediaPropsXml(fileName, out string mediaInfoXml)
                    || !Tools.MkvMerge.GetMediaPropsJson(fileName, out string mkvMergeJson)
                    || !Tools.FfProbe.GetMediaPropsJson(fileName, out string ffProbeJson)
                )
                {
                    Log.Error("Failed to read media tool info : {FileName}", fileName);
                    return false;
                }

                // Print and log info
                Log.Information("{FileName}", fileName);
                Log.Information("FfProbe: {FfProbeJson}", ffProbeJson);
                Console.Write(ffProbeJson);
                Log.Information("MkvMerge: {MkvMergeJson}", mkvMergeJson);
                Console.Write(mkvMergeJson);
                Log.Information("MediaInfo: {MediaInfoXml}", mediaInfoXml);
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
