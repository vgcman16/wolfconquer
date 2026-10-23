namespace WolfConquer.Core.World;

internal partial class LegacyNativeStatusFlagsBigVector32
{
    // ConcurrentDictionary.ToArray and this typed scalar comparison do not call
    // its user comparer. This must run AFTER every comparer-dependent lookup,
    // including the final membership candidate, not merely before its first key.
    private bool HasUnchangedCapturedGraph(State before)
    {
        if (!TryCapture(out var after) ||
            !ReferenceEquals(before.Registrations, after!.Registrations) ||
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

    private static Flag? FindCapturedFlag(State state, int key)
    {
        foreach (var saved in state.Rows)
            if (saved.Key == key) return saved.Row;
        return null;
    }

    // Shared by expiry and invocation queries. Refusal must precede CheckInvoke's
    // genuine timestamp renewal; valid success still calls the real row method.
    private bool TryFindCurrentFlag(int flag, out Flag? value)
    {
        value = null;
        if (!TryCapture(out var state) || !ValidIndex(flag, state!.Words) ||
            !state.Registrations.TryGetValue(flag, out var candidate) ||
            !HasUnchangedCapturedGraph(state) || candidate is null ||
            !ReferenceEquals(FindCapturedFlag(state, flag), candidate)) return false;
        value = candidate;
        return true;
    }
}
