using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Core.Accounts;

internal static class SqlAccountWorldHandoffCleanupTransfer
{
    internal static Exception RetainIssueCleanup(Exception primary,
        IReadOnlyList<IAsyncDisposable>? failedResources, bool committed)
    {
        ArgumentNullException.ThrowIfNull(primary);
        var outcome = committed ? (Attempted: true, Committed: true) : ReadIssueOutcome(primary);
        return RetainCleanup(primary, failedResources, outcome.Attempted, outcome.Committed);
    }

    internal static Exception RetainCleanup(Exception primary,
        IReadOnlyList<IAsyncDisposable>? failedResources, bool commitAttempted, bool committed)
    {
        ArgumentNullException.ThrowIfNull(primary);
        if (committed && !commitAttempted)
            throw new ArgumentException("Observed commitment requires an attempted COMMIT.");
        var resources = new List<IAsyncDisposable>();
        var uniqueResources = new HashSet<IAsyncDisposable>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<Exception>();
        var visited = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        pending.Push(primary);
        while (pending.TryPop(out var failure))
        {
            if (!visited.Add(failure)) continue;
            if (failure is ISqlPendingCleanup { HasPendingCleanup: true } retained)
            {
                if (uniqueResources.Add(retained)) resources.Add(retained);
                continue; // This exact owner already retains its nested pending cleanup.
            }
            PushChildren(failure, pending);
        }
        if (failedResources is not null)
            foreach (var resource in failedResources)
                if (uniqueResources.Add(resource)) resources.Add(resource);
        if (resources.Count == 0) return primary;
        return SqlAccountWorldHandoffException.WithPendingCleanup(primary, resources.ToArray(),
            commitAttempted, committed);
    }

    private static (bool Attempted, bool Committed) ReadIssueOutcome(Exception primary)
    {
        bool known = false;
        var pending = new Stack<Exception>();
        var visited = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        pending.Push(primary);
        while (pending.TryPop(out var failure))
        {
            if (!visited.Add(failure)) continue;
            if (failure is SqlAccountWorldHandoffCommitOutcomeUnknownException) return (true, false);
            if (failure is SqlAccountWorldHandoffCommittedCleanupException) { known = true; continue; }
            PushChildren(failure, pending);
        }
        return (known, known);
    }

    private static void PushChildren(Exception failure, Stack<Exception> pending)
    {
        if (failure is AggregateException aggregate)
        {
            for (int index = aggregate.InnerExceptions.Count - 1; index >= 0; index--)
                pending.Push(aggregate.InnerExceptions[index]);
        }
        else if (failure.InnerException is { } inner) pending.Push(inner);
    }
}
