using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private readonly ConcurrentDictionary<Task, byte> _legacyRuntimeWorkers = new();
    private Task? _legacyRuntimeLastFault;
    private long _legacyRuntimeFaultedWorkers;

    // Diagnostics for real returned workers, not a stop/start fence or a handler drain.
    internal long LegacyRuntimeFaultedWorkerCount => Volatile.Read(ref _legacyRuntimeFaultedWorkers);

    internal Task[] CaptureLegacyRuntimeWorkerTasks()
    {
        var tasks = _legacyRuntimeWorkers.Keys.ToList();
        var fault = Volatile.Read(ref _legacyRuntimeLastFault);
        if (fault is not null && !tasks.Contains(fault)) tasks.Add(fault);
        return tasks.ToArray();
    }

    private void TrackLegacyRuntimeWorker(Task actual)
    {
        _legacyRuntimeWorkers.TryAdd(actual, 0);
        _ = actual.ContinueWith(static (completed, state) =>
        {
            var owner = (WorldServerFrameHandler)state!;
            if (completed.IsFaulted)
            {
                // Keep bounded, observable diagnostics before retiring the actual task.
                _ = completed.Exception;
                Interlocked.Increment(ref owner._legacyRuntimeFaultedWorkers);
                Interlocked.Exchange(ref owner._legacyRuntimeLastFault, completed);
            }
            owner._legacyRuntimeWorkers.TryRemove(completed, out _);
        }, this, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
