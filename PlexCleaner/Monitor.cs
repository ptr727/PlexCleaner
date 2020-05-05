using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InsaneGenius.Utilities;

namespace PlexCleaner
{
    internal class Monitor
    {
        public Monitor()
        {
            Watcher = new List<FileSystemWatcher>();
            WatchFolders = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            WatchLock = new object();
            LastWriteLine = string.Empty;
            LastWriteLineLock = new object();
        }

        public bool MonitorFolders(List<string> folders)
        {
            ConsoleEx.WriteLine("");
            ConsoleEx.WriteLine("Monitoring folders ...");

            void Changehandler(object s, FileSystemEventArgs e) => OnChanged(e, this);
            void Renamehandler(object s, RenamedEventArgs e) => OnRenamed(e, this);
            void Errorhandler(object s, ErrorEventArgs e) => OnError(e, this);

            // Create file system watcher for each folder
            foreach (string folder in folders)
            {
                // Create a file system watcher for the folder
                ConsoleEx.WriteLine($"Monitoring : \"{folder}\".");
                FileSystemWatcher watch = new FileSystemWatcher();
                Watcher.Add(watch);
                watch.Path = folder;
                watch.NotifyFilter = NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
                watch.Filter = "*.*";
                watch.IncludeSubdirectories = true;
                watch.Changed += Changehandler;
                watch.Created += Changehandler;
                watch.Deleted += Changehandler;
                watch.Renamed += Renamehandler;
                watch.Error += Errorhandler;
            }

            // Enable event watching
            foreach (FileSystemWatcher watch in Watcher)
                watch.EnableRaisingEvents = true;

            // Wait for exit to be signalled
            while (!Program.Cancel.WaitForSet(1000))
            {
                // Lock and process the list of folders
                List<string> watchlist = new List<string>();
                lock (WatchLock)
                {
                    if (WatchFolders.Any())
                    {
                        // Remove root folders from the watchlist
                        // TODO : Maybe we need a way to not process sub-directories?
                        //foreach (string folder in folders)
                        //    WatchFolders.Remove(folder);

                        // Find folders that have settled down, i.e. not modified in last wait time
                        DateTime settletime = DateTime.UtcNow.AddSeconds(-Program.Config.MonitorOptions.MonitorWaitTime);
                        foreach ((string key, DateTime value) in WatchFolders)
                        // If not recently modified and all files in the folder are readable
                            if (value < settletime)
                                if (!FileEx.AreFilesInDirectoryReadable(key))
                                    WriteLine($"Folder not readable : \"{key}\"");
                                else
                                    watchlist.Add(key);

                        // Remove watched folders from the watchlist
                        foreach (string folder in watchlist)
                            WatchFolders.Remove(folder);
                    }
                }

                // Any work to do
                if (!watchlist.Any())
                    continue;

                // Process changes in the watched folders
                foreach (string folder in watchlist)
                    ConsoleEx.WriteLine($"Monitored changes in : \"{folder}\"");
                Process process = new Process();
                process.ProcessFolders(watchlist);
                Process.DeleteEmptyFolders(watchlist);
            }

            // Disable event watching
            foreach (FileSystemWatcher watch in Watcher)
                watch.EnableRaisingEvents = false;
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
            WriteLineEvent($"OnChanged : {e.ChangeType} : \"{e.FullPath}\"");
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
                    break;
                case WatcherChangeTypes.All:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(e));
            }
        }

        private static void OnRenamed(RenamedEventArgs e, Monitor monitor)
        {
            // Call instance version
            monitor.OnRenamedEx(e);
        }

        private void OnRenamedEx(RenamedEventArgs e)
        {
            WriteLineEvent($"OnRenamed : {e.ChangeType} : \"{e.OldFullPath}\" to \"{e.FullPath}\"");
            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Renamed:
                    // Treat the old file as a deleted file
                    OnDeleted(e.OldFullPath);
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
                    throw new ArgumentOutOfRangeException(nameof(e));
            }
        }

        private static void OnError(ErrorEventArgs e, Monitor monitor)
        {
            // Call the instance version
            monitor.OnErrorEx(e);
        }

        private void OnErrorEx(ErrorEventArgs e)
        {
            // Cancel in case of error
            WriteLineError($"OnError : {e.GetException()}");
            Program.Cancel.State = true;
        }

        // Write to the console, but only write if the output is different to the previous output
        private void WriteLine(string value)
        {
            WriteLine(ConsoleEx.OutputColor, value);
        }

        private void WriteLineEvent(string value)
        {
            WriteLine(ConsoleEx.EventColor, value);
        }

        private void WriteLineError(string value)
        {
            WriteLine(ConsoleEx.ErrorColor, value);
        }

        private void WriteLine(ConsoleColor color, string value)
        {
            // Lock
            lock (LastWriteLineLock)
            {
                // Compare with previous output
                if (LastWriteLine.Equals(value, StringComparison.OrdinalIgnoreCase))
                    return;

                ConsoleEx.WriteLineColor(color, value);
                LastWriteLine = value;
            }
        }

        private void OnChanged(string pathname)
        {
            // File
            string foldername = null;
            if (File.Exists(pathname))
            {
                // Get the file details
                FileInfo fileinfo = new FileInfo(pathname);

                // Ignore our own sidecar and *.tmp files being created
                if (!fileinfo.Extension.Equals(".tmp", StringComparison.OrdinalIgnoreCase) &&
                    !SidecarFile.IsSidecarFile(fileinfo))
                    foldername = fileinfo.DirectoryName;
            }
            // Or directory
            else if(Directory.Exists(pathname))
            {
                foldername = pathname;
            }

            // Did we get a folder
            if (string.IsNullOrEmpty(foldername))
                return;

            // Lock
            lock (WatchLock)
            {
                // Add new folder or update existing timestamp
                if (WatchFolders.ContainsKey(foldername))
                {
                    // Update the modified time
                    WriteLine($"Updating folder for processing by {DateTime.Now.AddSeconds(Program.Config.MonitorOptions.MonitorWaitTime)} : \"{foldername}\"");
                    WatchFolders[foldername] = DateTime.UtcNow;
                }
                else
                {
                    // Add the folder
                    WriteLine($"Adding folder for processing by {DateTime.Now.AddSeconds(Program.Config.MonitorOptions.MonitorWaitTime)} : \"{foldername}\"");
                    WatchFolders.Add(foldername, DateTime.UtcNow);
                }
            }
        }

        private void OnDeleted(string pathname)
        {
            // The path we get no longer exists, it may be a file, or it may be a folder
            // TODO : Figure out how to accurately test if deleted path was a file or folder
        }

        private readonly List<FileSystemWatcher> Watcher;
        private readonly Dictionary<string, DateTime> WatchFolders;
        private readonly object WatchLock;
        private string LastWriteLine;
        private readonly object LastWriteLineLock;
    }
}
