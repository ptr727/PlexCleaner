using System.Diagnostics;

namespace PlexCleaner;

// TODO: Cleanup chapters
// Emby.Server.Implementations.Chapters.ChapterManager: Stopping chapter extraction for foo because a chapter was found with a position greater than the runtime.
// Emby.Server.Implementations.Chapters.ChapterManager: Skipping chapter image extraction for foo as the average chapter duration 0 was lower than the minimum threshold 10000000

// TODO: Change encoding on external subtitle files to UTF8
// 'ISO-8859-10' is not a supported encoding name. For information on defining a custom encoding, see the documentation for the Encoding.RegisterProvider method.

public static class Process
{
    internal static bool ProcessFile(
        string fileName,
        out bool modified,
        out bool ignored,
        out SidecarFile.StatesType state,
        out string processName,
        out string? failedOperation
    )
    {
        // Init
        modified = false;
        ignored = false;
        state = SidecarFile.StatesType.None;
        processName = fileName;
        failedOperation = null;
        ProcessFile? processFile = null;
        bool result;
        bool skipped = false;
        bool modifiedBeforeCleanup = false;

        // Current step, reported as the failure reason on error
        string? operation = null;

        // Scoped logging session to continue logging after any error or warning; only has an effect
        // when --logelevate installed the PerFileLogLevel filter, otherwise the session is inert. Use
        // the same effective level as the filter so the deprecated --logwarning floor stays consistent
        using IDisposable logScope = PerFileLogLevel.BeginScope(
            LoggerFactory.EffectiveLevel(Program.Options)
        );

        // Process in jump loop
        try
        {
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
                operation = $"{nameof(File)}.{nameof(File.Exists)}";
                if (!File.Exists(fileName))
                {
                    Log.Error("Skipping inaccessible file : {FileName}", fileName);
                    // Error
                    result = false;
                    break;
                }

                // Create file processor to hold state
                operation = nameof(ProcessFile);
                processFile = new ProcessFile(fileName);
                DateTime lastWriteTime = processFile.FileInfo.LastWriteTimeUtc;

                // Is the file writeable
                operation = nameof(processFile.IsWriteable);
                if (!processFile.IsWriteable())
                {
                    Log.Error("Skipping read-only file : {FileName}", fileName);
                    // Error
                    skipped = true;
                    result = false;
                    break;
                }

                // Delete the sidecar file if matching MKV file not found
                operation = nameof(processFile.DeleteMismatchedSidecarFile);
                if (!processFile.DeleteMismatchedSidecarFile(ref modified))
                {
                    result = true;
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
                operation = nameof(processFile.RemuxByExtension);
                if (!processFile.RemuxByExtension(true, ref modified) || Program.IsCancelled())
                {
                    // Error
                    result = false;
                    break;
                }

                // Delete or skip non-MKV files
                // Conditional on DeleteUnwantedExtensions option
                operation = nameof(processFile.DeleteNonMkvFile);
                if (!processFile.DeleteNonMkvFile(ref modified))
                {
                    // Ok
                    result = true;
                    break;
                }

                // All files past this point are MKV files
                Debug.Assert(SidecarFile.IsMkvFile(processFile.FileInfo));

                // If a sidecar file exists for this MKV file it must be writable
                operation = nameof(processFile.IsSidecarWriteable);
                if (processFile.IsSidecarAvailable() && !processFile.IsSidecarWriteable())
                {
                    Log.Error(
                        "Skipping media file due to read-only sidecar file : {FileName}",
                        fileName
                    );
                    // Error
                    skipped = true;
                    result = false;
                    break;
                }

                // Make sure the file extension is lowercase for case sensitive filesystems
                // Always changes extension to lowercase
                operation = nameof(processFile.MakeExtensionLowercase);
                if (!processFile.MakeExtensionLowercase(ref modified))
                {
                    // Error
                    result = false;
                    break;
                }

                // Read the media info
                operation = nameof(processFile.GetMediaProps);
                if (!processFile.GetMediaProps() || Program.IsCancelled())
                {
                    // Error
                    result = false;
                    break;
                }

                // ReMux non-MKV containers using MKV file extensions
                // Conditional on ReMux option, fails if not Matroska and ReMux is not enabled
                operation = nameof(processFile.RemuxNonMkvContainer);
                if (!processFile.RemuxNonMkvContainer(ref modified) || Program.IsCancelled())
                {
                    // Error
                    result = false;
                    break;
                }

                // ReMux to remove extra video tracks
                // Conditional on ReMux option, fails if more than one video track and ReMux not enabled
                operation = nameof(processFile.RemuxRemoveExtraVideoTracks);
                if (!processFile.RemuxRemoveExtraVideoTracks(ref modified) || Program.IsCancelled())
                {
                    // Error
                    result = false;
                    break;
                }

                // Verify track counts are supported
                // No more than one video track, audio or video track
                operation = nameof(processFile.VerifyTrackCounts);
                if (!processFile.VerifyTrackCounts() || Program.IsCancelled())
                {
                    // Error
                    result = false;
                    break;
                }

                modifiedBeforeCleanup = modified;
                modified = false;

                // Remove all cover art attachments or video tracks that interfere with processing logic
                // Conditional on ReMux option, fails if cover art is present and ReMux not enabled
                operation = nameof(processFile.RemoveCoverArt);
                if (!processFile.RemoveCoverArt(ref modified) || Program.IsCancelled())
                {
                    // Error
                    result = false;
                    break;
                }

                // Remove tags, titles, and attachments
                // Conditional on RemoveTags option
                operation = nameof(processFile.RemoveTags);
                if (!processFile.RemoveTags(ref modified) || Program.IsCancelled())
                {
                    // Error
                    result = false;
                    break;
                }

                // Test that the file extension and container type is MKV and all media info should be valid
                operation = nameof(processFile.VerifyMediaInfo);
                if (!processFile.VerifyMediaInfo())
                {
                    // Error
                    result = false;
                    break;
                }

                // Repair tracks with metadata errors
                // Conditional on AutoRepair option
                operation = nameof(processFile.RepairMetadataErrors);
                if (!processFile.RepairMetadataErrors(ref modified) || Program.IsCancelled())
                {
                    // Error
                    result = false;
                    break;
                }

                // Repair Matroska container structure that cannot be Direct Played
                // Conditional on Verify option
                operation = nameof(processFile.RepairMatroskaStructure);
                if (!processFile.RepairMatroskaStructure(ref modified) || Program.IsCancelled())
                {
                    // Error
                    result = false;
                    break;
                }

                // Remove EIA-EIA-608 and CTA-708 Closed Captions from the video stream
                // Conditional on RemoveClosedCaptions option
                operation = nameof(processFile.RemoveClosedCaptions);
                if (!processFile.RemoveClosedCaptions(true, ref modified) || Program.IsCancelled())
                {
                    // Error
                    result = false;
                    break;
                }

                // DeInterlace interlaced content
                // Conditional on DeInterlace option
                operation = nameof(processFile.DeInterlace);
                if (!processFile.DeInterlace(true, ref modified) || Program.IsCancelled())
                {
                    // Error
                    result = false;
                    break;
                }

                // Change all tracks with an unknown language to the default language
                // Conditional on SetUnknownLanguage option
                operation = nameof(processFile.SetUnknownLanguageTracks);
                if (!processFile.SetUnknownLanguageTracks(ref modified) || Program.IsCancelled())
                {
                    // Error
                    result = false;
                    break;
                }

                // Remove all the unwanted language tracks
                // Conditional on RemoveUnwantedLanguageTracks option
                operation = nameof(processFile.RemoveUnwantedLanguageTracks);
                if (
                    !processFile.RemoveUnwantedLanguageTracks(ref modified) || Program.IsCancelled()
                )
                {
                    // Error
                    result = false;
                    break;
                }

                // TODO: RemoveUnwantedLanguageTracks() and RemoveDuplicateTracks() both remux and are logically separate.
                // They could be combined to complete in one remux operation.

                // Remove all the duplicate tracks
                // Conditional on RemoveDuplicateTracks option
                operation = nameof(processFile.RemoveDuplicateTracks);
                if (!processFile.RemoveDuplicateTracks(ref modified) || Program.IsCancelled())
                {
                    // Error
                    result = false;
                    break;
                }

                // Clear redundant Default track flags
                // Conditional on SetTrackFlags option
                operation = nameof(processFile.RepairDefaultFlags);
                if (!processFile.RepairDefaultFlags(ref modified) || Program.IsCancelled())
                {
                    // Error
                    result = false;
                    break;
                }

                // Re-Encode formats that cannot be direct-played
                // Conditional on ReEncode option
                operation = nameof(processFile.ReEncode);
                if (!processFile.ReEncode(true, ref modified) || Program.IsCancelled())
                {
                    // Error
                    result = false;
                    break;
                }

                // Verify media streams, and repair if possible
                // Conditional on Verify and AutoRepair options
                operation = nameof(processFile.VerifyAndRepair);
                if (!processFile.VerifyAndRepair(ref modified) || Program.IsCancelled())
                {
                    // Error
                    result = false;
                    break;
                }

                // FfMpeg or HandBrake could undo the previous cleanup, repeat cleanup
                // Split so the reported operation names the actual failing step
                if (modified)
                {
                    operation = nameof(processFile.RemoveCoverArt);
                    if (!processFile.RemoveCoverArt(ref modified) || Program.IsCancelled())
                    {
                        result = false;
                        break;
                    }
                    operation = nameof(processFile.RemoveTags);
                    if (!processFile.RemoveTags(ref modified) || Program.IsCancelled())
                    {
                        result = false;
                        break;
                    }
                    operation = nameof(processFile.RepairMetadataErrors);
                    if (!processFile.RepairMetadataErrors(ref modified) || Program.IsCancelled())
                    {
                        result = false;
                        break;
                    }
                    operation = nameof(processFile.RepairMatroskaStructure);
                    if (!processFile.RepairMatroskaStructure(ref modified) || Program.IsCancelled())
                    {
                        result = false;
                        break;
                    }
                    operation = nameof(processFile.RemoveClosedCaptions);
                    if (
                        !processFile.RemoveClosedCaptions(true, ref modified)
                        || Program.IsCancelled()
                    )
                    {
                        result = false;
                        break;
                    }
                    operation = nameof(processFile.SetUnknownLanguageTracks);
                    if (
                        !processFile.SetUnknownLanguageTracks(ref modified) || Program.IsCancelled()
                    )
                    {
                        result = false;
                        break;
                    }
                    operation = nameof(processFile.RemoveUnwantedLanguageTracks);
                    if (
                        !processFile.RemoveUnwantedLanguageTracks(ref modified)
                        || Program.IsCancelled()
                    )
                    {
                        result = false;
                        break;
                    }
                    operation = nameof(processFile.RemoveDuplicateTracks);
                    if (!processFile.RemoveDuplicateTracks(ref modified) || Program.IsCancelled())
                    {
                        result = false;
                        break;
                    }
                    operation = nameof(processFile.RepairDefaultFlags);
                    if (!processFile.RepairDefaultFlags(ref modified) || Program.IsCancelled())
                    {
                        result = false;
                        break;
                    }
                }

                // Restore the file timestamp
                operation = nameof(processFile.SetLastWriteTimeUtc);
                if (!processFile.SetLastWriteTimeUtc(lastWriteTime) || Program.IsCancelled())
                {
                    // Error
                    result = false;
                    break;
                }

                // Re-verify the tool info is correctly recorded
                operation = nameof(processFile.VerifyMediaInfo);
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
        }
        catch (Exception e) when (!Program.IsCancelled() && Log.Logger.LogAndHandle(e))
        {
            // An exception says nothing about the media, so fail the file without deleting it
            result = false;
            skipped = true;
        }

        modified |= modifiedBeforeCleanup;
        if (!result)
        {
            failedOperation = operation;
        }

        // Update state if we opened a file for processing
        if (processFile != null)
        {
            // Delete files that failed processing
            // Conditional on DeleteInvalidFiles
            if (!result && !skipped)
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
                    Log.Debug("Looking for empty folders in {Folder}", folder);
                    DeleteEmptyDirectories(folder, ref deleted);
                    _ = Interlocked.Add(ref totalDeleted, deleted);
                });
        }
        catch (OperationCanceledException)
        {
            // Cancelled
            fatalError = true;
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e))
        {
            // Error
            fatalError = true;
        }

        Log.Logger.LogOverrideContext().Information("Deleted folders : {Deleted}", totalDeleted);

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

    public static bool ProcessFiles(List<string> fileList, out bool writeFailed)
    {
        // Log active options
        Log.Logger.LogOverrideContext()
            .Information(
                "Process Options: TestSnippets: {TestSnippets}, QuickScan: {QuickScan}, FileIgnoreList: {FileIgnoreList}, ThreadCount: {ThreadCount}, Process file count: {Count}",
                Program.Options.TestSnippets,
                Program.Options.QuickScan,
                Program.Config.ProcessOptions.FileIgnoreList.Count,
                Program.Options.ThreadCount,
                fileList.Count
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
                    out string processName,
                    out string? failedOperation
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

                // Per-outcome metrics: this is the only place the State flags are known
                Metrics.RecordStates(state);
                if (modified)
                {
                    Metrics.RecordModified();
                }
                if ((state & SidecarFile.StatesType.VerifyFailed) != 0)
                {
                    Metrics.RecordVerifyFailed();
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
                            FailedOperation = failedOperation,
                        }
                    );
                }

                return processResult;
            }
        );

        writeFailed = !FinishProcessing(resultsJson);
        return ret;
    }

    internal static bool FinishProcessing(ProcessResultJsonSchema resultsJson)
    {
        bool writesSucceeded = true;

        // Errors
        List<ProcessResultJsonSchema.ProcessResult> errorResults =
        [
            .. resultsJson.Results.Results.Where(item => !item.Result),
        ];
        Log.Logger.LogOverrideContext().Information("Error files : {Count}", errorResults.Count);
        errorResults.ForEach(item =>
            Log.Logger.LogOverrideContext()
                .Information(
                    "Error: {Reason} : {FileName}",
                    item.State == SidecarFile.StatesType.None ? item.FailedOperation ?? "Unknown"
                        : item.FailedOperation == null ? item.State.ToString()
                        : $"{item.State}, {item.FailedOperation}",
                    item.NewFileName
                )
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
        if (
            Program.Config.VerifyOptions.RegisterInvalidFiles
            && !RegisterInvalidFiles(failedResults, Program.Options.SettingsFile)
        )
        {
            writesSucceeded = false;
        }

        // Write the process results to file
        if (!string.IsNullOrEmpty(Program.Options.ResultsFile))
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
            Log.Information("Writing results file : {ResultsFile}", Program.Options.ResultsFile);
            try
            {
                ProcessResultJsonSchema.ToFile(Program.Options.ResultsFile, resultsJson);
            }
            catch (Exception e) when (Log.Logger.LogAndHandle(e))
            {
                Log.Error(
                    "Failed to write results file : {ResultsFile}",
                    Program.Options.ResultsFile
                );
                writesSucceeded = false;
            }
        }

        return writesSucceeded;
    }

    // Set when a settings write fails, so the next call retries it
    internal static bool s_ignoreListWritePending;

    internal static bool RegisterInvalidFiles(
        List<ProcessResultJsonSchema.ProcessResult> failedResults,
        string settingsFile
    )
    {
        // Add failed items to the ignore list, a deleted file no longer exists to be ignored
        HashSet<string> ignoreList = Program.Config.ProcessOptions.FileIgnoreList;
        bool modified = false;
        foreach (
            ProcessResultJsonSchema.ProcessResult item in failedResults.Where(item =>
                !item.State.HasFlag(SidecarFile.StatesType.FileDeleted)
            )
        )
        {
            modified |= ignoreList.Add(item.NewFileName);
        }

        // Prune entries for missing files, but only where the parent folder exists, an unmounted share keeps its entries
        modified |= ignoreList.RemoveWhere(IsMissingFile) > 0;

        // Update JSON settings if modified, or if an earlier write failed
        if (!modified && !s_ignoreListWritePending)
        {
            return true;
        }
        Log.Information(
            "Updating FileIgnoreList entries ({Count}) in settings file : {SettingsFile}",
            ignoreList.Count,
            settingsFile
        );
        try
        {
            ConfigFileJsonSchema.ToFile(settingsFile, Program.Config);
            s_ignoreListWritePending = false;
            return true;
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e))
        {
            Log.Error("Failed to write settings file : {SettingsFile}", settingsFile);
            s_ignoreListWritePending = true;
            return false;
        }
    }

    private static bool IsMissingFile(string fileName)
    {
        string? folder = Path.GetDirectoryName(fileName);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder) || File.Exists(fileName))
        {
            return false;
        }

        // File.Exists hides access and IO errors, and is case sensitive on Linux, so list the folder
        // Compare names directly, a pattern would treat * and ? in a name as wildcards
        try
        {
            string name = Path.GetFileName(fileName);
            EnumerationOptions options = new() { AttributesToSkip = 0 };
            return !Directory
                .EnumerateFiles(folder, "*", options)
                .Any(item =>
                    Path.GetFileName(item).Equals(name, StringComparison.OrdinalIgnoreCase)
                );
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e))
        {
            return false;
        }
    }
}
