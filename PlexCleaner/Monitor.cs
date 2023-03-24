using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InsaneGenius.Utilities;
using Serilog;

namespace PlexCleaner;

internal class Monitor
{
    public Monitor()
    {
        Watcher = new List<FileSystemWatcher>();
        WatchFolders = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        WatchLock = new object();
    }

    public bool MonitorFolders(List<string> folders)
    {
        Log.Logger.Information("Monitoring folders ...");

        void ChangeHandler(object s, FileSystemEventArgs e)
        {
            OnChanged(e, this);
        }

        void RenameHandler(object s, RenamedEventArgs e)
        {
            OnRenamed(e, this);
        }

        void ErrorHandler(object s, ErrorEventArgs e)
        {
            OnError(e);
        }

        // Create file system watcher for each folder
        foreach (string folder in folders)
        {
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

        // Wait for exit to be signalled
        while (!Program.IsCancelled(1000))
        {
            // Lock and process the list of folders
            List<string> watchlist = new();
            lock (WatchLock)
            {
                if (WatchFolders.Any())
                {
                    // Remove root folders from the watchlist
                    // TODO : Should we not process sub-directories?
                    // foreach (string folder in folders)
                    //     WatchFolders.Remove(folder);

                    // Find folders that have settled down, i.e. not modified in last wait time
                    DateTime settleTime = DateTime.UtcNow.AddSeconds(-Program.Config.MonitorOptions.MonitorWaitTime);
                    foreach ((string key, DateTime value) in WatchFolders)
                    {
                        // If not recently modified and all files in the folder are readable
                        if (value < settleTime)
                        {
                            if (!FileEx.AreFilesInDirectoryReadable(key))
                            {
                                Log.Logger.Information("Folder not readable : {Folder}", key);
                            }
                            else
                            {
                                watchlist.Add(key);
                            }
                        }
                    }

                    // Remove watched folders from the watchlist
                    watchlist.ForEach(item => WatchFolders.Remove(item));
                }
            }

            // Any work to do
            if (!watchlist.Any())
            {
                continue;
            }

            // Process changes in the watched folders
            foreach (string folder in watchlist)
            {
                Log.Logger.Information("Monitored changes in : {Folder}", folder);
            }

            Process process = new();
            process.ProcessFolders(watchlist);
            Process.DeleteEmptyFolders(watchlist);
        }

        // Disable event watching
        Watcher.ForEach(item => item.EnableRaisingEvents = false);
        Watcher.Clear();

        return true;
    }

    private static void OnChanged(FileSystemEventArgs e, Monitor monitor)
    {
        // Call instance version
        monitor.OnChangedEx(e);
    }

    private void OnChangedEx(FileSystemEventArgs e)
    {
        Log.Logger.Information("OnChanged : {ChangeType} : {FullPath}", e.ChangeType, e.FullPath);
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
        Log.Logger.Information("OnRenamed : {ChangeType} : {OldFullPath} to {FullPath}", e.ChangeType, e.OldFullPath, e.FullPath);
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

            // Ignore our own sidecar and *.tmp files being created
            if (!fileInfo.Extension.Equals(".tmp", StringComparison.OrdinalIgnoreCase) &&
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
                Log.Logger.Information("Updating folder for processing by {MonitorWaitTime} : {Folder}", DateTime.Now.AddSeconds(Program.Config.MonitorOptions.MonitorWaitTime), folderName);
                WatchFolders[folderName] = DateTime.UtcNow;
            }
            else
            {
                // Add the folder
                Log.Logger.Information("Adding folder for processing by {MonitorWaitTime} : {Folder}", DateTime.Now.AddSeconds(Program.Config.MonitorOptions.MonitorWaitTime), folderName);
                WatchFolders.Add(folderName, DateTime.UtcNow);
            }
        }
    }

    private static void OnDeleted()
    {
        // The path we get no longer exists, it may be a file, or it may be a folder
        // TODO: Figure out how to accurately test if deleted path was a file or folder
    }

    private readonly List<FileSystemWatcher> Watcher;
    private readonly Dictionary<string, DateTime> WatchFolders;
    private readonly object WatchLock;
}
