using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeStatusFlagsBigVector32
{
    private const int WindWalkerFlag = 194;
    private readonly ConditionalWeakTable<WindWalkerSnapshot, State> windWalkerSnapshots = new();
    private readonly ConditionalWeakTable<WindWalkerChange, WindWalkerPreparedState> windWalkerChanges = new();
    private readonly ConditionalWeakTable<WindWalkerChange, WindWalkerOrigin> windWalkerOrigins = new();
    private sealed record WindWalkerPreparedState(State Before, State After, PreparedChange? Change);
    private enum WindWalkerPreparationKind { Query, Expiry }
    private sealed record WindWalkerOrigin(WindWalkerPreparationKind Kind)
    {
        internal bool Committed;
    }

    // Values describe one private capture. Possessing or constructing a value
    // cannot put it into this actual vector's private capture table.
    internal sealed class WindWalkerSnapshot
    {
        internal bool Present { get; }
        internal DateTime Timer { get; }
        internal int Seconds { get; }
        internal bool RemoveOnDeath { get; }
        internal WindWalkerSnapshot(bool present, DateTime timer, int seconds, bool removeOnDeath)
        { Present = present; Timer = timer; Seconds = seconds; RemoveOnDeath = removeOnDeath; }
    }

    internal sealed class WindWalkerChange
    {
        internal WindWalkerSnapshot Proposed { get; }
        internal bool Changed { get; }
        internal WindWalkerChange(WindWalkerSnapshot proposed, bool changed)
        { Proposed = proposed; Changed = changed; }
    }

    internal bool TryCaptureWindWalker(out WindWalkerSnapshot? snapshot)
    {
        snapshot = null;
        if (!Monitor.IsEntered(SyncRoot)) return false;
        try
        {
            if (!TryCapture(out var before) || before!.Words.Length != LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength ||
                !ValidIndex(WindWalkerFlag, before.Words) ||
                !IsCurrent(before) || !HasUnchangedCapturedGraph(before)) return false;
            snapshot = CaptureWindWalkerInfo(before); return true;
        }
        catch (Exception) { return false; }
    }

    // No dictionary comparer, callbacks, time read, repair or mutation. A fresh
    // complete physical recapture also checks every unrelated registration.
    internal bool IsWindWalkerCurrentPlain(WindWalkerSnapshot snapshot) =>
        Monitor.IsEntered(SyncRoot) && snapshot is not null &&
        windWalkerSnapshots.TryGetValue(snapshot, out var captured) && HasUnchangedCapturedGraph(captured);

    // Issuance classification survives consumption for the final closed proof.
    // It is not mutation authority: the separate live preparation is single-use.
    internal bool IsWindWalkerExpiryChange(WindWalkerChange change) =>
        Monitor.IsEntered(SyncRoot) && change is not null &&
        windWalkerOrigins.TryGetValue(change, out var origin) && origin.Kind == WindWalkerPreparationKind.Expiry;

    // Historical evidence only; never a live preparation or replay capability.
    internal bool IsWindWalkerCommitted(WindWalkerChange change) =>
        Monitor.IsEntered(SyncRoot) && change is not null &&
        windWalkerOrigins.TryGetValue(change, out var origin) && origin.Committed;

    internal bool TryCommitWindWalker(WindWalkerChange change, WorldSessionStore.WindWalkerPublicationProof proof)
    {
        if (!Monitor.IsEntered(SyncRoot) || change is null || proof is null ||
            !windWalkerChanges.TryGetValue(change, out var prepared) ||
            !windWalkerOrigins.TryGetValue(change, out var origin) || origin.Committed ||
            !proof.IsCurrentPlain(this, change)) return false;
        // A failed final attempt is consumed too; a restored raw graph cannot
        // replay this preparation under a later query/registration lifetime.
        windWalkerChanges.Remove(change);
        try
        {
            PreparedState? staged = null;
            if (prepared.Change is { } native)
            {
                if (!TryTakeCurrent(native, out staged)) return false;
            }
            else if (!IsCurrent(prepared.Before)) return false;
            // IsCurrent/TryTakeCurrent can invoke a legacy custom comparer.
            // Purely recapture the FULL before and proposed graph afterward,
            // then check the closed Store/actor proof with no callback left.
            if (!HasUnchangedCapturedGraph(prepared.Before) || !WindWalkerGraphMatchesPlain(prepared.After) ||
                !Monitor.IsEntered(SyncRoot) || !proof.IsCurrentPlain(this, change)) return false;
            if (staged is not null) CommitPreparedState(staged);
            origin.Committed = true;
            return true;
        }
        catch (Exception) { return false; }
    }

    private WindWalkerSnapshot CaptureWindWalkerInfo(State state)
    {
        RowState? current = null;
        foreach (var row in state.Rows) if (row.Key == WindWalkerFlag) { current = row; break; }
        var snapshot = current is { } value
            ? new WindWalkerSnapshot(true, value.Timer, value.Seconds, value.RemoveOnDeath)
            : new WindWalkerSnapshot(false, default, 0, false);
        windWalkerSnapshots.Add(snapshot, state); return snapshot;
    }

    private static bool WindWalkerGraphMatchesPlain(State expected)
    {
        if (!expected.Words.AsSpan().SequenceEqual(expected.SnapshotWords)) return false;
        var entries = expected.Registrations.ToArray();
        if (entries.Length != expected.Rows.Length) return false;
        foreach (var captured in expected.Rows)
        {
            bool found = false;
            foreach (var entry in entries)
            {
                if (entry.Key != captured.Key) continue;
                var row = entry.Value;
                if (!ReferenceEquals(row, captured.Row) || row is null || row.Key != captured.Key ||
                    row.Secounds != captured.Seconds || row.InvokerSecouds != captured.Invoker ||
                    row.Timer != captured.Timer || row.TimerInvoker != captured.InvokeTimer ||
                    row.RemoveOnDead != captured.RemoveOnDeath) return false;
                found = true; break;
            }
            if (!found) return false;
        }
        return true;
    }
}
