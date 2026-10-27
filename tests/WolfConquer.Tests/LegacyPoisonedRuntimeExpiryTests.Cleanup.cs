using System.Runtime.ExceptionServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyPoisonedRuntimeExpiryTests
{
    private static async Task FinishPoisonRuntimeAsync(WorldServerFrameHandler handler,
        CancellationTokenSource cancellation, StringWriter log, PoisonExpiryTimeProvider clock,
        IReadOnlyCollection<Task> startedWorkers, Exception? primary)
    {
        var failures = new List<Exception>();
        if (primary is not null) failures.Add(primary);
        try { await cancellation.CancelAsync(); }
        catch (Exception exception) { failures.Add(exception); }

        // Keep the actual initial tasks, even if tracking retires them later.
        var workers = new HashSet<Task>(startedWorkers, ReferenceEqualityComparer.Instance);
        workers.UnionWith(handler.CaptureLegacyRuntimeWorkerTasks());
        try
        {
            await Task.WhenAll(workers).WaitAsync(WorkerBound);
            clock.AssertAllDisposed();
            Assert.Equal(0L, handler.LegacyRuntimeFaultedWorkerCount);
        }
        catch (Exception exception) { failures.Add(exception); }

        // CancelAsync joins cancellation callbacks, not runtime task bodies.
        // Keep their log/token usable if actual worker completion is unproven.
        if (workers.All(worker => worker.IsCompleted))
        {
            try { log.Dispose(); }
            catch (Exception exception) { failures.Add(exception); }
            try { cancellation.Dispose(); }
            catch (Exception exception) { failures.Add(exception); }
        }

        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1)
            throw new AggregateException("Poison expiry assertion and/or owned runtime cleanup failed.", failures);
    }
}
