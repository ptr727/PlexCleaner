using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Serilog;

namespace PlexCleaner;

public class Monitor
{
    private readonly List<FileSystemWatcher> _watcher = [];

    private readonly Dictionary<string, DateTime> _watchFolders = new(
        StringComparer.OrdinalIgnoreCase
    );

    private readonly Lock _watchLock = new();

    private static void LogMonitorMessage()
    {
        Log.Information("Monitoring folders ...");
        Program.LogInterruptMessage();
    }

    public bool MonitorFolders(List<string> folders)
    {
        const int MonitorWaitTime = 60;

        LogMonitorMessage();

        // Trim quotes around input paths
        folders = [.. folders.Select(file => file.Trim('"'))];

        // Create file system watcher for each folder
        foreach (string folder in folders)
        {
            // Must be a directory
            if (!Directory.Exists(folder))
            {
                Log.Error("Media path is not a valid directory : {Folder}", folder);
                return false;
            }

            // Create a file system watcher for the folder
            Log.Information("Monitoring : {Folder}", folder);
            FileSystemWatcher watch = new();
            _watcher.Add(watch);
            watch.Path = folder;
            watch.NotifyFilter =
                NotifyFilters.Size
                | NotifyFilters.CreationTime
                | NotifyFilters.LastWrite
                | NotifyFilters.FileName
                | NotifyFilters.DirectoryName
                | NotifyFilters.Size;
            watch.Filter = "*.*";
            watch.IncludeSubdirectories = true;
            watch.Changed += (_, e) => OnChanged(e);
            watch.Created += (_, e) => OnChanged(e);
            watch.Deleted += (_, e) => OnChanged(e);
            watch.Renamed += (_, e) => OnRenamed(e);
            watch.Error += (_, e) => OnError(e);
        }

        // Enable event watching
        _watcher.ForEach(item => item.EnableRaisingEvents = true);

        // Add monitor folders to the processing list
        if (Program.Options.PreProcess)
        {
            // Lock
            lock (_watchLock)
            {
                Log.Information("Pre-processing all monitored folders");
                foreach (string folder in folders)
                {
                    Log.Information("Adding folder to processing queue : {Folder}", folder);
                    _watchFolders.Add(folder, DateTime.UtcNow.AddSeconds(-MonitorWaitTime));
                }
            }
        }

        // Wait for exit to be signaled
        while (!Program.WaitForCancel(1000))
        {
            // Lock and process the list of folders
            List<string> watchList = [];
            List<string> removeList = [];
            lock (_watchLock)
            {
                // Anything to process
                if (_watchFolders.Count != 0)
                {
                    // Evaluate all folders in the watch list
                    DateTime settleTime = DateTime.UtcNow.AddSeconds(-MonitorWaitTime);
                    foreach ((string folder, DateTime timeStamp) in _watchFolders)
                    {
                        // Settled down, i.e. not modified in last wait time
                        if (timeStamp >= settleTime)
                        {
                            // Not yet
                            continue;
                        }

                        // Directory must still exist, e.g. not deleted
                        if (!Directory.Exists(folder))
                        {
                            Log.Information(
                                "Folder deleted, removing from processing queue : {Folder}",
                                folder
                            );
                            removeList.Add(folder);
                            continue;
                        }

                        // All files in folder must be readable, e.g. not being written to
                        DirectoryInfo dirInfo = new(folder);
                        if (
                            !dirInfo
                                .EnumerateFiles("*.*", SearchOption.TopDirectoryOnly)
                                .All(IsFileReadable)
                        )
                        {
                            Log.Information(
                                "Files in folder are not readable, delaying processing : {Folder}",
                                folder
                            );
                            _watchFolders[folder] = DateTime.UtcNow;
                            continue;
                        }

                        // Add to processing list
                        watchList.Add(folder);
                    }

                    // Remove deleted folders from watchlist
                    removeList.ForEach(item => _watchFolders.Remove(item));

                    // Remove watched folders from the watchlist
                    watchList.ForEach(item => _watchFolders.Remove(item));
                }
            }

            // Any work to do
            if (watchList.Count == 0)
            {
                continue;
            }

            // Process changes in the watched folders
            if (!ProcessChanges(watchList))
            {
                // Fatal error
                return false;
            }

            LogMonitorMessage();
        }

        // Disable event watching
        _watcher.ForEach(item => item.EnableRaisingEvents = false);
        _watcher.Clear();

        // Done
        return true;
    }

    private static bool IsFileReadable(FileInfo fileInfo)
    {
        try
        {
            // Try to open the file for read access with read/write sharing
            using FileStream stream = fileInfo.Open(
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite
            );
            stream.Close();
        }
        // Only handle expected IO exceptions
        catch (IOException)
        {
            return false;
        }
        return true;
    }

    private static bool ProcessChanges(List<string> folderList)
    {
        // Get file and directory list
        if (
            !ProcessDriver.GetFiles(
                folderList,
                out List<string> directoryList,
                out List<string> fileList
            )
        )
        {
            return false;
        }
        directoryList.ForEach(item => Log.Information("Processing changes in : {Folder}", item));

        // Process files
        if (!Process.ProcessFiles(fileList))
        {
            return false;
        }

        // Delete empty folders
        if (!Process.DeleteEmptyFolders(directoryList))
        {
            return false;
        }

        // Done
        return true;
    }

    private void OnChanged(FileSystemEventArgs e)
    {
        // Registered for Changed, Created, Deleted
        Log.Verbose("OnChanged : {ChangeType} : {FullPath}", e.ChangeType, e.FullPath);
        switch (e.ChangeType)
        {
            case WatcherChangeTypes.Changed:
            case WatcherChangeTypes.Created:
                // Process new or modified content
                OnChanged(e.FullPath);
                break;
            case WatcherChangeTypes.Deleted:
                // Cleanup when a file or directory gets deleted
                OnDeleted(e.FullPath);
                break;
            case WatcherChangeTypes.Renamed:
            case WatcherChangeTypes.All:
            default:
                // Ignore
                break;
        }
    }

    private void OnRenamed(RenamedEventArgs e)
    {
        // Registered for Renamed
        Log.Verbose(
            "OnRenamed : {ChangeType} : {OldFullPath} to {FullPath}",
            e.ChangeType,
            e.OldFullPath,
            e.FullPath
        );
        switch (e.ChangeType)
        {
            case WatcherChangeTypes.Renamed:
                // Treat the old file as a deleted file
                OnDeleted(e.OldFullPath);
                // Treat the renamed file as a changed file
                OnChanged(e.FullPath);
                break;
            case WatcherChangeTypes.Created:
            case WatcherChangeTypes.Deleted:
            case WatcherChangeTypes.Changed:
            case WatcherChangeTypes.All:
            default:
                // Ignore
                break;
        }
    }

    private static void OnError(ErrorEventArgs e)
    {
        // Cancel in case of error
        Log.Error(e.GetException(), "OnError()");
        Program.Cancel();
    }

    private void OnChanged(string pathname)
    {
        // File
        string folderName = string.Empty;
        if (File.Exists(pathname))
        {
            // Get the file details
            FileInfo fileInfo = new(pathname);

            // Ignore sidecar and temp files
            if (!ProcessFile.IsTempFile(fileInfo) && !SidecarFile.IsSidecarFile(fileInfo))
            {
                folderName = fileInfo.DirectoryName ?? string.Empty;
            }
        }
        // Or directory
        else if (Directory.Exists(pathname))
        {
            folderName = pathname;
        }

        // Did we get a folder
        if (string.IsNullOrEmpty(folderName))
        {
            return;
        }

        // Lock
        lock (_watchLock)
        {
            // Add new folder or update existing timestamp
            if (_watchFolders.ContainsKey(folderName))
            {
                // Update the modified time
                Log.Verbose(
                    "Updating timestamp for folder in processing queue : {Folder}",
                    folderName
                );
                _watchFolders[folderName] = DateTime.UtcNow;
            }
            else
            {
                // Add the folder
                Log.Information("Adding folder to processing queue : {Folder}", folderName);
                _watchFolders.Add(folderName, DateTime.UtcNow);
            }
        }
    }

    private static void OnDeleted(string pathname) =>
        // The path we get no longer exists, it may be a file, or it may be a folder
        // TODO: How to determine if the deleted path was a file or folder?
        Log.Verbose("OnDeleted : {PathName}", pathname);
}
