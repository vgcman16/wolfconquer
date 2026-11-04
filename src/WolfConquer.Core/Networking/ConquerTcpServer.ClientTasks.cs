using System.Net.Sockets;
using System.Runtime.ExceptionServices;

namespace WolfConquer.Core.Networking;

public sealed partial class ConquerTcpServer
{
    private readonly List<ExceptionDispatchInfo> _clientTaskFailures = [];
    private readonly Dictionary<Task, TrackedClientTask> _clientTaskObservations = [];

    private sealed class TrackedClientTask
    {
        internal Task Task = null!; // Assigned the actual producer Task before registration.
        internal Exception? RetirementFailure;
        internal bool Observed;
    }

    private void TrackClientTask(TcpClient client, CancellationToken cancellationToken)
    {
        var tracked = new TrackedClientTask();
        tracked.Task = HandleClientAsync(client, cancellationToken, tracked);
        lock (_gate)
        {
            _clientTasks.Add(tracked.Task);
            _clientTaskObservations.Add(tracked.Task, tracked);
        }
        // Synchronous bookkeeping only, never detached resource cleanup. Read
        // each actual task's faults before the producer is removed from tracking.
        _ = tracked.Task.ContinueWith(_ => ObserveClientTask(tracked), CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private void ObserveClientTask(TrackedClientTask tracked)
    {
        lock (_gate)
        {
            if (tracked.Observed) return;
            if (!tracked.Task.IsCompleted) throw new InvalidOperationException("TCP client producer has not settled.");
            tracked.Observed = true;
            if (tracked.Task.Exception is { } aggregate)
            {
                foreach (var failure in aggregate.InnerExceptions)
                {
                    // This exact failure already belongs to the strong retirement
                    // entry. Do not report the same task failure a second time.
                    if (!ReferenceEquals(failure, tracked.RetirementFailure))
                        _clientTaskFailures.Add(ExceptionDispatchInfo.Capture(failure));
                }
                _publicFaulted = true;
            }
            _clientTasks.Remove(tracked.Task);
            _clientTaskObservations.Remove(tracked.Task);
        }
    }

    private List<Exception> DrainClientTaskFailures()
    {
        lock (_gate)
        {
            var result = _clientTaskFailures.Select(failure => failure.SourceException).ToList();
            _clientTaskFailures.Clear();
            return result;
        }
    }
}
