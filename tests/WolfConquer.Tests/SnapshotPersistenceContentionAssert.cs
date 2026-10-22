using System.Runtime.ExceptionServices;

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
            using var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var observer = SnapshotTransactionDeletionObserver.Create(path, transactionMarker);
            Task<T>? persistence = null;
            var failures = new List<Exception>();
            var started = false;
            var sawRetry = false;
            var completedWhileLocked = false;
            byte[] whileLocked = [];
            T saved = default!;
            try
            {
                // Native notification registration is accepted before this writer
                // exists. The observer waits on this owned thread, not the pool.
                persistence = Dedicated(() =>
                {
                    persistenceStarted.Set();
                    return persist();
                });
                started = persistenceStarted.Wait(TimeSpan.FromSeconds(5));
                // An actual matching temp REMOVAL under the real sharing lock
                // proves a failed replace was cleaned up, not merely Save entry.
                sawRetry = observer.WaitForDeletion(TimeSpan.FromSeconds(5));
                completedWhileLocked = persistence.IsCompleted;
                whileLocked = File.ReadAllBytes(path);
            }
            catch (Exception observationFailure)
            {
                failures.Add(observationFailure);
            }
            finally
            {
                CaptureFailure(locked.Dispose, failures);
                if (persistence != null)
                {
                    // Drain even on observation/assertion/setup failure before
                    // caller cleanup; retain the actual persistence exception.
                    CaptureFailure(() => saved = persistence.GetAwaiter().GetResult(), failures);
                }
                // Cancels/drains ONLY this observer's exact pending native IO
                // before freeing its unmanaged buffers or retained handles.
                CaptureFailure(observer.Dispose, failures);
            }

            if (failures.Count == 1)
            {
                ExceptionDispatchInfo.Capture(failures[0]).Throw();
            }
            if (failures.Count > 1)
            {
                throw new AggregateException(failures);
            }
            Assert.True(started);
            Assert.True(sawRetry, "A failed real replace attempt must delete its transaction temp.");
            Assert.False(completedWhileLocked);
            Assert.Equal(original, whileLocked);
            return saved;
        });
    }

    private static void CaptureFailure(Action action, List<Exception> failures)
    {
        try
        {
            action();
        }
        catch (Exception failure)
        {
            failures.Add(failure);
        }
    }

    private static Task<T> Dedicated<T>(Func<T> action) => Task.Factory.StartNew(
        action, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
}
