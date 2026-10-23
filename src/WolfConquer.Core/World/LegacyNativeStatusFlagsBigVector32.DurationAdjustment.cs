namespace WolfConquer.Core.World;

internal partial class LegacyNativeStatusFlagsBigVector32
{
    // The real Player owns the ordered XP policy. This component owns selection,
    // duration arithmetic and its single physical-row commit under the same gates.
    internal bool TryAdjustFirstDuration(ReadOnlySpan<int> priority, int fallback, short delta,
        LegacyNativePlayerRuntime player, LegacyNativeGameClientRuntime client, uint uid)
    {
        if (!Monitor.IsEntered(client.SpellAdmissionSyncRoot) ||
            !Monitor.IsEntered(player.SpellAdmissionSyncRoot) || !Monitor.IsEntered(SyncRoot)) return false;
        try
        {
            if (!OwnsDuration(player, client, uid) || !TryCapture(out var before)) return false;
            RowState? selected = SelectDurationRow(before!, priority, fallback);
            int seconds = selected is { } saved ? checked(saved.Seconds + delta) : 0;
            if (selected is { } target && !TryDeadline(target.Timer, seconds, out _)) return false;

            // Existing comparer-aware validation can run caller-supplied comparer
            // code. Recapture and compare the physical graph AFTER that code, then
            // recheck Player/UID/vector identity before the nonthrowing assignment.
            if (!IsCurrent(before!) || !TryCapture(out var current) ||
                !SameDurationState(before!, current!) || !OwnsDuration(player, client, uid)) return false;
            if (selected is { } row) row.Row.Secounds = seconds;
            return true; // A valid absent XP/Normal row is a successful no-op.
        }
        catch (Exception) { return false; }
    }

    private bool OwnsDuration(LegacyNativePlayerRuntime player, LegacyNativeGameClientRuntime client, uint uid) =>
        ReferenceEquals(player.Owner, client) && ReferenceEquals(client.Player, player) &&
        ReferenceEquals(player.BitVector, this) && player.UID == uid;

    private static RowState? SelectDurationRow(State state, ReadOnlySpan<int> priority, int fallback)
    {
        foreach (int key in priority)
            foreach (var row in state.Rows)
                if (row.Key == key) return row;
        foreach (var row in state.Rows)
            if (row.Key == fallback) return row;
        return null;
    }

    private static bool SameDurationState(State before, State current)
    {
        if (!ReferenceEquals(before.Registrations, current.Registrations) ||
            !ReferenceEquals(before.Words, current.Words) || before.Rows.Length != current.Rows.Length ||
            !before.SnapshotWords.AsSpan().SequenceEqual(current.SnapshotWords)) return false;
        foreach (var saved in before.Rows)
        {
            bool found = false;
            foreach (var row in current.Rows)
            {
                if (row.Key != saved.Key) continue;
                if (!ReferenceEquals(row.Row, saved.Row) || row.Seconds != saved.Seconds || row.Invoker != saved.Invoker ||
                    row.Timer != saved.Timer || row.InvokeTimer != saved.InvokeTimer ||
                    row.RemoveOnDeath != saved.RemoveOnDeath) return false;
                found = true;
                break;
            }
            if (!found) return false;
        }
        return true;
    }
}
