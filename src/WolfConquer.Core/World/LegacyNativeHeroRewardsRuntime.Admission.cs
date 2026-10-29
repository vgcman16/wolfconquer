using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHeroRewardsRuntime
{
    private readonly record struct GoalRow(uint Key, Item Row, uint Uid, uint Unknown, byte Claim);
    private readonly record struct GoalRecord(uint Uid, byte Claim);
    private sealed record GoalState(LegacyNativeDbSafeDictionary<uint, Item> Book, object Gate,
        IEqualityComparer<uint> Comparer, GoalRow[] Rows, long Revision);

    private bool HasCurrentOwner(LegacyNativePlayerRuntime player, uint uid, bool requireAttached,
        LegacyNativeHeroRewardsRuntime? capturedSlot) =>
        ReferenceEquals(Owner.Player, player) && ReferenceEquals(player.Owner, Owner) && player.UID == uid &&
        ReferenceEquals(Owner.HeroRewards, capturedSlot) &&
        (ReferenceEquals(capturedSlot, this) || (!requireAttached && capturedSlot is null));

    private bool TryCaptureGoals(LegacyNativeDbSafeDictionary<uint, Item> book, object gate, out GoalState? state)
    {
        state = null;
        if (!Monitor.IsEntered(gate) || !ReferenceEquals(ArrayGoals, book) ||
            !ReferenceEquals(book.SyncRoot, gate) || book.Count > MaxGoalRecords) return false;
        long revision = book.Revision;
        var entries = book.ToArray();
        var rows = new GoalRow[entries.Length];
        var aliases = new HashSet<Item>(ReferenceEqualityComparer.Instance);
        for (int index = 0; index < entries.Length; index++)
        {
            var entry = entries[index]; var row = entry.Value;
            if (entry.Key == 0 || row is null || row.UID != entry.Key || !aliases.Add(row)) return false;
            rows[index] = new(entry.Key, row, row.UID, row.UnKnow, row.Claim);
        }
        if (!ReferenceEquals(ArrayGoals, book) || !ReferenceEquals(book.SyncRoot, gate) ||
            book.Count != rows.Length || book.Revision != revision) return false;
        state = new(book, gate, book.Comparer, rows, revision);
        return true;
    }

    private bool GoalsStillCurrent(GoalState before)
    {
        var book = before.Book;
        if (!Monitor.IsEntered(before.Gate) || !ReferenceEquals(ArrayGoals, book) ||
            !ReferenceEquals(book.SyncRoot, before.Gate) || !ReferenceEquals(book.Comparer, before.Comparer) ||
            book.Count != before.Rows.Length || book.Revision != before.Revision) return false;
        foreach (var saved in before.Rows)
            if (!book.TryGetValue(saved.Key, out var row) || !ReferenceEquals(row, saved.Row) ||
                row.UID != saved.Uid || row.UnKnow != saved.Unknown || row.Claim != saved.Claim) return false;
        return ReferenceEquals(ArrayGoals, book) && book.Revision == before.Revision;
    }

    private bool RevalidateGoals(GoalState before)
    {
        // Comparer code may change an earlier row or the live owner. Compare
        // real physical rows and the owner's revision after every lookup pass.
        if (!GoalsStillCurrent(before) || !TryCaptureGoals(before.Book, before.Gate, out var after)) return false;
        if (!ReferenceEquals(before.Comparer, after!.Comparer) || before.Revision != after.Revision ||
            before.Rows.Length != after.Rows.Length) return false;
        for (int index = 0; index < before.Rows.Length; index++)
        {
            var left = before.Rows[index]; var right = after.Rows[index];
            if (left.Key != right.Key || !ReferenceEquals(left.Row, right.Row) || left.Uid != right.Uid ||
                left.Unknown != right.Unknown || left.Claim != right.Claim) return false;
        }
        return true;
    }

    private bool TryReadGoalRows(out GoalRow[] rows)
    {
        rows = [];
        lock (Owner.SpellAdmissionSyncRoot)
        {
            var player = Owner.Player;
            if (player is null) return false;
            lock (player.SpellAdmissionSyncRoot)
            lock (goalSyncRoot)
            {
                uint uid = player.UID; var capturedSlot = Owner.HeroRewards;
                var book = ArrayGoals; var gate = book?.SyncRoot;
                if (book is null || gate is null || !HasCurrentOwner(player, uid, false, capturedSlot)) return false;
                lock (gate)
                {
                    try
                    {
                        if (!TryCaptureGoals(book, gate, out var state) || !RevalidateGoals(state!) ||
                            !HasCurrentOwner(player, uid, false, capturedSlot)) return false;
                        rows = state!.Rows;
                        return true;
                    }
                    catch (Exception) { return false; }
                }
            }
        }
    }
}
