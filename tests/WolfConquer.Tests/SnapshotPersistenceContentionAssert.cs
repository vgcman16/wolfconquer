namespace WolfConquer.Tests;

// Exercises real persistence entrypoints once; retries belong only to production.
internal static class SnapshotPersistenceContentionAssert
{
    internal static Task RetriesAsync(
        string path, byte[] original, string transactionMarker, Action persist) =>
        RetriesAsync(path, original, transactionMarker, () => { persist(); return true; });

    internal static Task<T> RetriesAsync<T>(
        string path, byte[] original, string transactionMarker, Func<T> persist)
    {
        // No outer timeout can return while the owned writer is still using the fixture.
        return Dedicated(() =>
        {
            using var persistenceStarted = new ManualResetEventSlim();
            var retryObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var watcher = new FileSystemWatcher(
                Path.GetDirectoryName(path)!, Path.GetFileName(path) + transactionMarker + "*.tmp")
            {
                NotifyFilter = NotifyFilters.FileName,
            };
            using var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            // Deletion under this real lock proves failed replace cleanup, not merely Save entry.
            watcher.Deleted += (_, _) => retryObserved.TrySetResult(true);
            watcher.EnableRaisingEvents = true;
            var persistence = Dedicated(() =>
            {
                persistenceStarted.Set();
                return persist();
            });
            bool started;
            bool sawRetry;
            bool completedWhileLocked;
            byte[] whileLocked;
            T saved;
            try
            {
                started = persistenceStarted.Wait(TimeSpan.FromSeconds(5));
                sawRetry = retryObserved.Task.Wait(TimeSpan.FromSeconds(5));
                completedWhileLocked = persistence.IsCompleted;
                whileLocked = File.ReadAllBytes(path);
            }
            finally
            {
                locked.Dispose();
                // Drain even on observation failure before caller cleanup; retain writer faults.
                saved = persistence.GetAwaiter().GetResult();
            }

            Assert.True(started);
            Assert.True(sawRetry, "A failed real replace attempt must delete its transaction temp.");
            Assert.False(completedWhileLocked);
            Assert.Equal(original, whileLocked);
            return saved;
        });
    }

    private static Task<T> Dedicated<T>(Func<T> action) => Task.Factory.StartNew(
        action, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
}
