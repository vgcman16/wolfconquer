using System.Diagnostics;

namespace WolfConquer.Core.World;

// Synchronous bridge for genuine legacy-native callers. It uses the real
// connection's ordered write gate with a bounded token; no queue or native Socket.
internal static class LegacyOrdinarySpellNotificationPublisher
{
    private static readonly TimeSpan BatchTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DisconnectTimeout = TimeSpan.FromSeconds(5);

    internal static void Publish(LegacyOrdinarySpellBinding binding, IEnumerable<byte[]> frames)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(frames);
        var deadline = Stopwatch.GetTimestamp() + (long)(BatchTimeout.TotalSeconds * Stopwatch.Frequency);
        foreach (var frame in frames)
        {
            ArgumentNullException.ThrowIfNull(frame);
            if (WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
                Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) ||
                Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
                Monitor.IsEntered(binding.Storage.MutationGate) ||
                Monitor.IsEntered(binding.Proficiencies.MutationGate))
                throw new LegacyCommittedSpellNotificationException(
                    new InvalidOperationException("Committed notifications cannot run under mutation gates."));
            if (binding.IsRetired || !binding.Store.IsBoundSpellOwnerCurrent(binding))
                throw new LegacyCommittedSpellNotificationException(
                    new InvalidOperationException("Committed spell notification owner retired."));
            var remainingTicks = deadline - Stopwatch.GetTimestamp();
            if (remainingTicks <= 0) FailTimedOut(binding, null);
            var remaining = TimeSpan.FromSeconds((double)remainingTicks / Stopwatch.Frequency);
            var timeout = new CancellationTokenSource(remaining);
            Task? write = null;
            try
            {
                write = binding.Connection.SendAsync(frame, timeout.Token).AsTask();
                write.WaitAsync(remaining).GetAwaiter().GetResult();
                if (timeout.IsCancellationRequested) FailTimedOut(binding, write);
            }
            catch (Exception error)
            {
                if (error is LegacyCommittedSpellNotificationException) throw;
                if (error is TimeoutException or OperationCanceledException) FailTimedOut(binding, write);
                DisconnectBounded(binding, write);
                throw new LegacyCommittedSpellNotificationException(error);
            }
            finally
            {
                // A route which ignores both cancellation and close keeps its
                // owned byte array/token alive until the actual write ends.
                // This never transfers pooled native packet storage.
                if (write is not null && !write.IsCompleted)
                    ObserveUntilTerminal(write, new PendingWrite(frame, timeout));
                else timeout.Dispose();
            }
            // The next frame performs the owner check again. If ownership
            // retired in flight, remaining transport is an explicit committed
            // notification failure, never silently counted as delivery.
        }
    }

    private static void FailTimedOut(LegacyOrdinarySpellBinding binding, Task? write)
    {
        DisconnectBounded(binding, write);
        throw new LegacyCommittedSpellNotificationException(
            new TimeoutException("Committed spell notification exceeded its total batch deadline."));
    }

    private static void DisconnectBounded(LegacyOrdinarySpellBinding binding, Task? write)
    {
        var timeout = new CancellationTokenSource(DisconnectTimeout);
        Task? close = null;
        try
        {
            close = binding.Connection.DisconnectAsync(timeout.Token).AsTask();
            close.WaitAsync(DisconnectTimeout).GetAwaiter().GetResult();
        }
        catch (Exception) { /* Committed state remains; cancellation alone is not a bounded observer. */ }
        finally
        {
            if (close is not null && !close.IsCompleted)
                ObserveUntilTerminal(close, new PendingWrite(binding, timeout));
            else timeout.Dispose();
        }
        if (write is null) return;
        try { write.WaitAsync(DisconnectTimeout).GetAwaiter().GetResult(); }
        catch (TimeoutException) { /* Caller retains and observes this task in its finally. */ }
        catch (Exception) { _ = write.Exception; }
    }

    private static void ObserveUntilTerminal(Task operation, PendingWrite ownership) =>
        _ = operation.ContinueWith(static (completed, state) =>
        {
            _ = completed.Exception;
            ((PendingWrite)state!).Dispose();
        }, ownership, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    private sealed class PendingWrite(object retained, CancellationTokenSource lifetime) : IDisposable
    {
        public void Dispose()
        {
            lifetime.Dispose();
            GC.KeepAlive(retained);
        }
    }
}

internal sealed class LegacyCommittedSpellNotificationException : IOException
{
    internal LegacyCommittedSpellNotificationException(Exception inner)
        : base("ordinarySpellCommittedNotificationUnavailable", inner) { }
}
