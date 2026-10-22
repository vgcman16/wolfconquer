namespace WolfConquer.Tests;

internal interface ISnapshotTransactionDeletionObserver : IDisposable
{
    bool WaitForDeletion(TimeSpan timeout);
}

internal static class SnapshotTransactionDeletionObserver
{
    internal static ISnapshotTransactionDeletionObserver Create(string path, string transactionMarker)
    {
        // The Windows observer owns pending native IO and needs no ThreadPool
        // callback to observe cleanup inside production's bounded retry window.
        return OperatingSystem.IsWindows()
            ? new WindowsSnapshotTransactionDeletionObserver(path, transactionMarker)
            : new ManagedSnapshotTransactionDeletionObserver(path, transactionMarker);
    }

    // Retain the existing portable FileSystemWatcher behavior outside Windows;
    // no Windows DLL is loaded or native sharing assumption added on that path.
    private sealed class ManagedSnapshotTransactionDeletionObserver : ISnapshotTransactionDeletionObserver
    {
        private readonly object _sync = new();
        private readonly ManualResetEventSlim _deleted = new();
        private readonly FileSystemWatcher _watcher;
        private bool _disposed;

        internal ManagedSnapshotTransactionDeletionObserver(string path, string transactionMarker)
        {
            _watcher = new FileSystemWatcher(
                Path.GetDirectoryName(path)!, Path.GetFileName(path) + transactionMarker + "*.tmp")
            {
                NotifyFilter = NotifyFilters.FileName,
            };
            _watcher.Deleted += OnDeleted;
            try
            {
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception creationFailure)
            {
                try
                {
                    Dispose();
                }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException(creationFailure, cleanupFailure);
                }
                throw;
            }
        }

        public bool WaitForDeletion(TimeSpan timeout) => _deleted.Wait(timeout);

        private void OnDeleted(object sender, FileSystemEventArgs args)
        {
            lock (_sync)
            {
                if (!_disposed)
                {
                    _deleted.Set();
                }
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
            }
            _watcher.Deleted -= OnDeleted;
            _watcher.Dispose();
            _deleted.Dispose();
        }
    }
}
