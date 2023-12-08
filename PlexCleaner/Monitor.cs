using System;
using System.Collections.Generic;
using System.IO;
using InsaneGenius.Utilities;
using Serilog;

namespace PlexCleaner;

internal class Monitor
{
    private static void LogMonitorMessage()
    {
        Log.Logger.Information("Monitoring folders ...");
        Program.LogInterruptMessage();
    }

    public bool MonitorFolders(List<string> folders)
    {
        LogMonitorMessage();

        // Create file system watcher for each folder
        foreach (string folder in folders)
        {
            // Must be a directory
            if (!Directory.Exists(folder))
            {
                Log.Logger.Error("Media path is not a valid directory : {Folder}", folder);
                return false;
            }

            // Create a file system watcher for the folder
            Log.Logger.Information("Monitoring : {Folder}", folder);
            FileSystemWatcher watch = new();
            Watcher.Add(watch);
            watch.Path = folder;
            watch.NotifyFilter = NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            watch.Filter = "*.*";
            watch.IncludeSubdirectories = true;
            watch.Changed += ChangeHandler;
            watch.Created += ChangeHandler;
            watch.Deleted += ChangeHandler;
            watch.Renamed += RenameHandler;
            watch.Error += ErrorHandler;
        }

        // Enable event watching
        Watcher.ForEach(item => item.EnableRaisingEvents = true);

        // Add monitor folders to the processing list
        if (Program.Options.PreProcess)
        {
            // Lock
            lock (WatchLock)
            {
                Log.Logger.Information("Pre-processing all monitored folders");
                foreach (string folder in folders)
                {
                    Log.Logger.Information("Adding folder to processing queue : {Folder}", folder);
                    WatchFolders.Add(folder, DateTime.UtcNow.AddSeconds(-Program.Config.MonitorOptions.MonitorWaitTime));
                }
            }
        }

        // Wait for exit to be signaled
        while (!Program.WaitForCancel(1000))
        {
            // Lock and process the list of folders
            List<string> watchList = new();
            List<string> removeList = new();
            lock (WatchLock)
            {
                // Anything to process
                if (WatchFolders.Count != 0)
                {
                    // Evaluate all folders in the watch list
                    DateTime settleTime = DateTime.UtcNow.AddSeconds(-Program.Config.MonitorOptions.MonitorWaitTime);
                    foreach ((string folder, DateTime timeStamp) in WatchFolders)
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
                            Log.Logger.Information("Folder deleted, removing from processing queue : {Folder}", folder);
                            removeList.Add(folder);
                            continue;
                        }

                        // All files in folder must be readable, e.g. not being written to
                        if (!FileEx.AreFilesInDirectoryReadable(folder))
                        {
                            Log.Logger.Information("Files in folder are not readable, delaying processing : {Folder}", folder);
                            WatchFolders[folder] = DateTime.UtcNow;
                            continue;
                        }

                        // Add to processing list
                        watchList.Add(folder);
                    }

                    // Remove deleted folders from watchlist
                    removeList.ForEach(item => WatchFolders.Remove(item));

                    // Remove watched folders from the watchlist
                    watchList.ForEach(item => WatchFolders.Remove(item));
                }
            }

            // Any work to do
            if (watchList.Count == 0)
            {
                continue;
            }

            // Process changes in the watched folders
            foreach (string folder in watchList)
            {
                Log.Logger.Information("Processing changes in : {Folder}", folder);
            }
            if (!Process.ProcessFolders(watchList) || !Process.DeleteEmptyFolders(watchList))
            {
                // Fatal error
                return false;
            }

            LogMonitorMessage();
        }

        // Disable event watching
        Watcher.ForEach(item => item.EnableRaisingEvents = false);
        Watcher.Clear();

        // Done
        return true;

        // Local function change handlers
        void ErrorHandler(object s, ErrorEventArgs e)
        {
            OnError(e);
        }
        void RenameHandler(object s, RenamedEventArgs e)
        {
            OnRenamed(e, this);
        }
        void ChangeHandler(object s, FileSystemEventArgs e)
        {
            OnChanged(e, this);
        }
    }

    private static void OnChanged(FileSystemEventArgs e, Monitor monitor)
    {
        // Call instance version
        monitor.OnChangedEx(e);
    }

    private void OnChangedEx(FileSystemEventArgs e)
    {
        Log.Logger.Verbose("OnChanged : {ChangeType} : {FullPath}", e.ChangeType, e.FullPath);
        switch (e.ChangeType)
        {
            case WatcherChangeTypes.Changed:
            case WatcherChangeTypes.Created:
                // Process new or modified content
                OnChanged(e.FullPath);
                break;
            case WatcherChangeTypes.Deleted:
                // Cleanup when a file or directory gets deleted
                OnDeleted();
                break;
            case WatcherChangeTypes.Renamed:
                break;
            case WatcherChangeTypes.All:
                break;
            default:
                throw new NotImplementedException();
        }
    }

    private static void OnRenamed(RenamedEventArgs e, Monitor monitor)
    {
        // Call instance version
        monitor.OnRenamedEx(e);
    }

    private void OnRenamedEx(RenamedEventArgs e)
    {
        Log.Logger.Verbose("OnRenamed : {ChangeType} : {OldFullPath} to {FullPath}", e.ChangeType, e.OldFullPath, e.FullPath);
        switch (e.ChangeType)
        {
            case WatcherChangeTypes.Renamed:
                // Treat the old file as a deleted file
                OnDeleted();
                // Treat the renamed file as a changed file
                OnChanged(e.FullPath);
                break;
            case WatcherChangeTypes.Created:
                break;
            case WatcherChangeTypes.Deleted:
                break;
            case WatcherChangeTypes.Changed:
                break;
            case WatcherChangeTypes.All:
                break;
            default:
                throw new NotImplementedException();
        }
    }

    private static void OnError(ErrorEventArgs e)
    {
        // Call the instance version
        OnErrorEx(e);
    }

    private static void OnErrorEx(ErrorEventArgs e)
    {
        // Cancel in case of error
        Log.Logger.Error(e.GetException(), "OnErrorEx()");
        Program.Cancel();
    }

    private void OnChanged(string pathname)
    {
        // File
        string folderName = null;
        if (File.Exists(pathname))
        {
            // Get the file details
            FileInfo fileInfo = new(pathname);

            // Ignore sidecar and temp files
            if (!ProcessFile.IsTempFile(fileInfo) &&
                !SidecarFile.IsSidecarFile(fileInfo))
            {
                folderName = fileInfo.DirectoryName;
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
        lock (WatchLock)
        {
            // Add new folder or update existing timestamp
            if (WatchFolders.ContainsKey(folderName))
            {
                // Update the modified time
                Log.Logger.Verbose("Updating timestamp for folder in processing queue : {Folder}", folderName);
                WatchFolders[folderName] = DateTime.UtcNow;
            }
            else
            {
                // Add the folder
                Log.Logger.Information("Adding folder to processing queue : {Folder}", folderName);
                WatchFolders.Add(folderName, DateTime.UtcNow);
            }
        }
    }

    private static void OnDeleted()
    {
        // The path we get no longer exists, it may be a file, or it may be a folder
        // TODO: Figure out how to accurately test if deleted path was a file or folder
    }

    private readonly List<FileSystemWatcher> Watcher = new();
    private readonly Dictionary<string, DateTime> WatchFolders = new(StringComparer.OrdinalIgnoreCase);
    private readonly object WatchLock = new();
}
