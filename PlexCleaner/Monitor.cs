using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Utilities;
using Settings = PlexCleaner.Properties.Settings;

namespace PlexCleaner
{
    internal class Monitor
    {
        public Monitor()
        {
            _watcher = new List<FileSystemWatcher>();
            _watchfolders = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            _watchlock = new object();
            _lastwriteline = String.Empty;
            _lastwritelinelock = new object();
        }

        public bool MonitorFolders(List<string> folders)
        {
            ConsoleEx.WriteLine("Monitoring folders ...");

            void Changehandler(object s, FileSystemEventArgs e) => OnChanged(e, this);
            void Renamehandler(object s, RenamedEventArgs e) => OnRenamed(e, this);
            void Errorhandler(object s, ErrorEventArgs e) => OnError(e, this);

            // Create file system watcher for each folder
            foreach (string folder in folders)
            {
                // Create a file system watcher for the folder
                ConsoleEx.WriteLine($"Monitoring : \"{folder}\"");
                FileSystemWatcher watch = new FileSystemWatcher();
                _watcher.Add(watch);
                watch.Path = folder;
                watch.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
                watch.Filter = "*.*";
                watch.IncludeSubdirectories = true;
                watch.Changed += Changehandler;
                watch.Created += Changehandler;
                watch.Deleted += Changehandler;
                watch.Renamed += Renamehandler;
                watch.Error += Errorhandler;
            }

            // Enable event watching
            foreach (FileSystemWatcher watch in _watcher)
                watch.EnableRaisingEvents = true;

            // Wait for exit to be signalled
            while (!Program.Default.Cancel.WaitForCancel(1000))
            {
                // Lock and process the list of folders
                List<string> watchlist = new List<string>();
                lock (_watchlock)
                {
                    if (_watchfolders.Any())
                    {
                        // Remove root folders from the watchlist
                        foreach (string folder in folders)
                            _watchfolders.Remove(folder);

                        // Find folders that have settled down, i.e. not modified in last wait time
                        DateTime settletime = DateTime.UtcNow.AddSeconds(-Settings.Default.MonitorWaitTime);
                        foreach (KeyValuePair<string, DateTime> pair in _watchfolders)
                        // If not recently modified and all files in the folder are readable
                            if (pair.Value < settletime)
                                if (!FileEx.AreFilesInfolderReadable(pair.Key))
                                    WriteLine($"Folder not readable : \"{pair.Key}\"");
                                else
                                    watchlist.Add(pair.Key);

                        // Remove watched folders from the watchlist
                        foreach (string folder in watchlist)
                            _watchfolders.Remove(folder);
                    }
                }

                // Any work to do
                if (!watchlist.Any())
                    continue;

                // Process changes in the watched folders
                foreach (string folder in watchlist)
                    ConsoleEx.WriteLine($"Processing folder : \"{folder}\"");
                Process process = new Process();
                process.ProcessFolders(watchlist);
            }

            // Disable event watching
            foreach (FileSystemWatcher watch in _watcher)
                watch.EnableRaisingEvents = false;
            _watcher.Clear();

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
                    throw new ArgumentOutOfRangeException();
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
                    throw new ArgumentOutOfRangeException();
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
            Program.Default.Cancel.Cancel = true;
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
            lock (_lastwritelinelock)
            {
                if (_lastwriteline.Equals(value, StringComparison.OrdinalIgnoreCase))
                    return;

                ConsoleEx.WriteLineColor(color, value);
                _lastwriteline = value;
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

                // Ignore our own *.xml and *.tmp files
                if (!fileinfo.Extension.Equals(".tmp", StringComparison.OrdinalIgnoreCase) &&
                    !fileinfo.Extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
                    foldername = fileinfo.DirectoryName;
            }
            // Or directory
            else if(Directory.Exists(pathname))
            {
                foldername = pathname;
            }

            // Did we get a folder
            if (String.IsNullOrEmpty(foldername))
                return;
            lock (_watchlock)
            {
                // Add new folder or update existing timestamp
                if (_watchfolders.ContainsKey(foldername))
                {
                    // Update the modified time
                    _watchfolders[foldername] = DateTime.UtcNow;
                }
                else
                {
                    // Add the folder
                    WriteLine($"Adding folder for processing : \"{foldername}\"");
                    _watchfolders.Add(foldername, DateTime.UtcNow);
                }
            }
        }

        private void OnDeleted(string pathname)
        {
            // The path we get no longer exists, it may be a file, or it may be a folder
            // TODO : Figure out how to accurately test if deleted path was a file or folder
            // If it is a file, we want to cleanup MKV and XML mappings, for deleted MKV files also delete the XML file
            if (!Tools.IsMkvFile(pathname))
                return;
            
            // Looks like MKV got deleted, create XML path, and delete XML file
            string xmlpath = Path.ChangeExtension(pathname, ".xml");
            WriteLine($"Deleting XML for deleted MKV : \"{xmlpath}\"");
            FileEx.DeleteFile(xmlpath);
        }


        private List<FileSystemWatcher> _watcher;
        private Dictionary<string, DateTime> _watchfolders;
        private object _watchlock;
        private String _lastwriteline;
        private object _lastwritelinelock;
    }
}
