using System;
using System.Collections.Generic;
using System.IO;
using System.Configuration;

namespace AzureSync
{
    public class FolderWatcher : IDisposable
    {
        public static FolderWatcher Current { get; }

        static FolderWatcher()
        {
            Current = new FolderWatcher(AzureSyncConfiguration.Current.SyncFolder);
        }

        public event EventHandler<FileSystemEventArgs> Changed;
        public event EventHandler<RenamedEventArgs> Renamed;

        private FileSystemWatcher _watcher;

        private FolderWatcher(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException("path");

            _watcher = new FileSystemWatcher();
            _watcher.Path = path;
            _watcher.IncludeSubdirectories = true;
            _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size;
            _watcher.Changed += new FileSystemEventHandler(OnChanged);
            _watcher.Created += new FileSystemEventHandler(OnChanged);
            _watcher.Deleted += new FileSystemEventHandler(OnChanged);
            _watcher.Renamed += new RenamedEventHandler(OnRenamed);
        }

        public void Start()
        {
            _watcher.EnableRaisingEvents = true;
        }

        public void Stop()
        {
            _watcher.EnableRaisingEvents = false;
        }

        private Dictionary<string, DateTime> _lastRead = new Dictionary<string, DateTime>();

        private DateTime GetLastRead(string path)
        {
            if (!_lastRead.ContainsKey(path))
                _lastRead.Add(path, default(DateTime));

            return _lastRead[path];
        }

        private void SetLastRead(string path, DateTime value)
        {
            if (!_lastRead.ContainsKey(path))
                _lastRead.Add(path, value);
            else
                _lastRead[path] = value;
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            // need single digit milliseconds precision, otherwise we get multiple events firing
            DateTime lastWriteTime = DateTime.Parse(File.GetLastWriteTime(e.FullPath).ToString("yyyy-MM-dd HH:mm:ss.f"));

            if (lastWriteTime != GetLastRead(e.FullPath))
            {
                SetLastRead(e.FullPath, lastWriteTime);

                // only raise event if not a directory, or change type is not changed (so directory change events are not raised)
                if (!IsDirectory(e.FullPath) || e.ChangeType != WatcherChangeTypes.Changed)
                    Changed?.Invoke(sender, e);
            }
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            Renamed?.Invoke(sender, e);
        }

        private bool IsDirectory(string path)
        {
            if (!Directory.Exists(path)) return false;
            FileAttributes attr = File.GetAttributes(path);
            return attr.HasFlag(FileAttributes.Directory);
        }

        public void Dispose()
        {
            _watcher.Dispose();
        }
    }
}
