#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Serilog;

#endregion

namespace PlexCleaner;

public static class Process
{
    private static bool ProcessFile(
        string fileName,
        out bool modified,
        out bool ignored,
        out SidecarFile.StatesType state,
        out string processName
    )
    {
        // Init
        modified = false;
        ignored = false;
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
                Log.Information("Skipping ignored file : {FileName}", fileName);
                // Ok
                ignored = true;
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
                // Ok
                ignored = true;
                result = true;
                break;
            }

            // ReMux non-MKV containers matched by extension
            // Conditional on ReMux option
            if (!processFile.RemuxByExtension(true, ref modified) || Program.IsCancelled())
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
            Debug.Assert(SidecarFile.IsMkvFile(processFile.FileInfo));

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
            if (!processFile.GetMediaProps() || Program.IsCancelled())
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
            if (!processFile.RemoveClosedCaptions(true, ref modified) || Program.IsCancelled())
            {
                // Error
                result = false;
                break;
            }

            // DeInterlace interlaced content
            // Conditional on DeInterlace option
            if (!processFile.DeInterlace(true, ref modified) || Program.IsCancelled())
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
            if (!processFile.ReEncode(true, ref modified) || Program.IsCancelled())
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
                    DeleteEmptyDirectories(folder, ref deleted);
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

    private static void DeleteEmptyDirectories(string directory, ref int deleted)
    {
        // Find all directories in this directory, not all subdirectories, we will call recursively
        DirectoryInfo parentInfo = new(directory);
        foreach (
            DirectoryInfo dirInfo in parentInfo.EnumerateDirectories(
                "*",
                SearchOption.TopDirectoryOnly
            )
        )
        {
            // Call recursively for this directory
            DeleteEmptyDirectories(dirInfo.FullName, ref deleted);

            // Test for files and directories, if none, delete this directory
            if (dirInfo.GetFiles().Length != 0 || dirInfo.GetDirectories().Length != 0)
            {
                continue;
            }
            Directory.Delete(dirInfo.FullName);

            deleted++;
        }
    }

    public static bool ProcessFiles(List<string> fileList)
    {
        // Log active options
        Log.Logger.LogOverrideContext()
            .Information(
                "Process Options: TestSnippets: {TestSnippets}, QuickScan: {QuickScan}, FileIgnoreList: {FileIgnoreList}",
                Program.Options.TestSnippets,
                Program.Options.QuickScan,
                Program.Config.ProcessOptions.FileIgnoreList.Count
            );

        // Process all the files
        ProcessResultJsonSchema resultsJson = new();
        Lock resultLock = new();
        bool ret = ProcessDriver.ProcessFiles(
            fileList,
            nameof(ProcessFiles),
            false,
            fileName =>
            {
                // Process the file
                bool processResult = ProcessFile(
                    fileName,
                    out bool modified,
                    out bool ignored,
                    out SidecarFile.StatesType state,
                    out string processName
                );

                // Cancelled
                if (Program.IsCancelled())
                {
                    return false;
                }

                // Ignored do not add to results
                if (ignored)
                {
                    return processResult;
                }

                // Save result
                lock (resultLock)
                {
                    resultsJson.Results.Results.Add(
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
        List<ProcessResultJsonSchema.ProcessResult> errorResults =
        [
            .. resultsJson.Results.Results.Where(item => !item.Result),
        ];
        Log.Logger.LogOverrideContext().Information("Error files : {Count}", errorResults.Count);
        errorResults.ForEach(item =>
            Log.Logger.LogOverrideContext()
                .Information("Error: {State} : {FileName}", item.State, item.NewFileName)
        );

        // Modified
        List<ProcessResultJsonSchema.ProcessResult> modifiedResults =
        [
            .. resultsJson.Results.Results.Where(item => item.Modified),
        ];
        Log.Logger.LogOverrideContext()
            .Information("Modified files : {Count}", modifiedResults.Count);
        modifiedResults.ForEach(item =>
            Log.Logger.LogOverrideContext()
                .Information("Modified: {State} : {FileName}", item.State, item.NewFileName)
        );

        // Verify failed
        List<ProcessResultJsonSchema.ProcessResult> failedResults =
        [
            .. resultsJson.Results.Results.Where(item =>
                item.State.HasFlag(SidecarFile.StatesType.VerifyFailed)
            ),
        ];
        Log.Logger.LogOverrideContext()
            .Information("VerifyFailed files : {Count}", failedResults.Count);
        failedResults.ForEach(item =>
            Log.Logger.LogOverrideContext()
                .Information("VerifyFailed: {State} : {FileName}", item.State, item.NewFileName)
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

            // Update JSON settings if modified
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
            // Add result summaries
            errorResults.ForEach(item => resultsJson.Results.Errors.Files.Add(item.NewFileName));
            resultsJson.Results.Errors.Total = errorResults.Count;
            modifiedResults.ForEach(item =>
                resultsJson.Results.Modified.Files.Add(item.NewFileName)
            );
            resultsJson.Results.Modified.Total = modifiedResults.Count;
            failedResults.ForEach(item =>
                resultsJson.Results.VerifyFailed.Files.Add(item.NewFileName)
            );
            resultsJson.Results.VerifyFailed.Total = failedResults.Count;

            // Sort by file name to simplify comparison with previous results
            resultsJson.Results.Results.Sort(
                (x, y) =>
                    string.Compare(x.OriginalFileName, y.OriginalFileName, StringComparison.Ordinal)
            );
            resultsJson.Results.Errors.Files.Sort(StringComparer.Ordinal);
            resultsJson.Results.Modified.Files.Sort(StringComparer.Ordinal);
            resultsJson.Results.VerifyFailed.Files.Sort(StringComparer.Ordinal);

            // Set version info
            resultsJson.SetVersionInfo();

            // Write to results JSON file
            Log.Information(
                "Writing results file : {Program.Options.ResultFile}",
                Program.Options.ResultsFile
            );
            ProcessResultJsonSchema.ToFile(Program.Options.ResultsFile, resultsJson);
        }

        return ret;
    }
}
