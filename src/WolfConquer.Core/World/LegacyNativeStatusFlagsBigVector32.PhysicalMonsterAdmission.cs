namespace WolfConquer.Core.World;

internal partial class LegacyNativeStatusFlagsBigVector32
{
    // Scoped read-only admission for the full physical monster calculation. No
    // live/staged words, registrations, rows or reusable authority escape here.
    internal bool TryValidatePhysicalMonsterStatus(ReadOnlySpan<int> queried)
    {
        if (!Monitor.IsEntered(SyncRoot)) return false;
        try
        {
            if (!TryCapture(out var before)) return false;
            foreach (int index in queried)
                if (!ValidIndex(index, before!.Words)) return false;

            // IsCurrent invokes the genuine dictionary comparer. A pure full
            // recapture afterward catches changes made by its final lookup.
            return IsCurrent(before!) && TryCapture(out var after) &&
                SamePhysicalMonsterState(before!, after!);
        }
        catch (Exception) { return false; }
    }

    private static bool SamePhysicalMonsterState(State before, State after)
    {
        if (!ReferenceEquals(before.Registrations, after.Registrations) ||
            !ReferenceEquals(before.Words, after.Words) || before.Rows.Length != after.Rows.Length ||
            !before.SnapshotWords.AsSpan().SequenceEqual(after.SnapshotWords)) return false;
        foreach (var saved in before.Rows)
        {
            bool found = false;
            foreach (var row in after.Rows)
            {
                if (row.Key != saved.Key) continue;
                if (!ReferenceEquals(saved.Row, row.Row) || saved.Seconds != row.Seconds ||
                    saved.Invoker != row.Invoker || saved.Timer != row.Timer ||
                    saved.InvokeTimer != row.InvokeTimer || saved.RemoveOnDeath != row.RemoveOnDeath) return false;
                found = true;
                break;
            }
            if (!found) return false;
        }
        return true;
    }
}
