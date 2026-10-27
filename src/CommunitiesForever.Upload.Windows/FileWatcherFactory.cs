using CommunitiesForever.Upload.Core.Uploading;

namespace CommunitiesForever.Upload.Windows;

/// <summary>
/// FileSystemWatcher on an account's SavedVariables folder. It can miss events (buffer overflow, network drives), so the
/// loop also checks every 30 s; an error just wakes it.
/// </summary>
internal sealed class FileWatcherFactory : IFileWatcherFactory
{
    public IDisposable Watch(string folder, string fileName, Action changed) => new Watcher(folder, fileName, changed);

    private sealed class Watcher : IDisposable
    {
        private readonly FileSystemWatcher _watcher;
        private readonly Action _changed;

        public Watcher(string folder, string fileName, Action changed)
        {
            _changed = changed;
            _watcher = new FileSystemWatcher(folder, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime,
            };
            _watcher.Changed += OnChanged;
            _watcher.Created += OnChanged;
            _watcher.Renamed += OnChanged;
            _watcher.Error += OnError;
            _watcher.EnableRaisingEvents = true;
        }

        public void Dispose()
        {
            // Long-running app: unsubscribe before disposing (CLAUDE.md rule 11).
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnChanged;
            _watcher.Created -= OnChanged;
            _watcher.Renamed -= OnChanged;
            _watcher.Error -= OnError;
            _watcher.Dispose();
        }

        private void OnChanged(object sender, FileSystemEventArgs e) => _changed();

        private void OnError(object sender, ErrorEventArgs e) => _changed();
    }
}
