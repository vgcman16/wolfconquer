using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHeroRewardsRuntime
{
    private readonly record struct GoalRow(uint Key, Item Row, uint Uid, uint Unknown, byte Claim);
    private readonly record struct GoalRecord(uint Uid, byte Claim);
    private sealed record GoalState(LegacyNativeDbSafeDictionary<uint, Item> Book, object Gate,
        IEqualityComparer<uint> Comparer, GoalRow[] Rows, Item[] Cache, Item[] CacheValues, bool Dirty);

    private bool HasCurrentOwner(LegacyNativePlayerRuntime player, uint uid, bool requireAttached,
        LegacyNativeHeroRewardsRuntime? capturedSlot) =>
        ReferenceEquals(Owner.Player, player) && ReferenceEquals(player.Owner, Owner) && player.UID == uid &&
        ReferenceEquals(Owner.HeroRewards, capturedSlot) &&
        (ReferenceEquals(capturedSlot, this) || (!requireAttached && capturedSlot is null));

    private bool TryCaptureGoals(LegacyNativeDbSafeDictionary<uint, Item> book, object gate, out GoalState? state)
    {
        state = null;
        if (!ReferenceEquals(ArrayGoals, book) || !ReferenceEquals(book.SyncRoot, gate) ||
            book.Count > MaxGoalRecords || book.MyArray is null || book.MyArray.Length > MaxGoalRecords) return false;
        var entries = book.ToArray();
        var rows = new GoalRow[entries.Length];
        var aliases = new HashSet<Item>(ReferenceEqualityComparer.Instance);
        for (int index = 0; index < entries.Length; index++)
        {
            var entry = entries[index]; var row = entry.Value;
            if (entry.Key == 0 || row is null || row.UID != entry.Key || !aliases.Add(row)) return false;
            rows[index] = new(entry.Key, row, row.UID, row.UnKnow, row.Claim);
        }
        var cache = book.MyArray;
        if (!ReferenceEquals(ArrayGoals, book) || !ReferenceEquals(book.SyncRoot, gate) ||
            book.Count != rows.Length || cache is null || cache.Length > MaxGoalRecords) return false;
        state = new(book, gate, book.Comparer, rows, cache, cache.ToArray(), book.Update);
        return true;
    }

    private bool GoalsStillCurrent(GoalState before)
    {
        var book = before.Book;
        if (!ReferenceEquals(ArrayGoals, book) || !ReferenceEquals(book.SyncRoot, before.Gate) ||
            !ReferenceEquals(book.Comparer, before.Comparer) || book.Count != before.Rows.Length ||
            !ReferenceEquals(book.MyArray, before.Cache) || book.Update != before.Dirty ||
            !SameGoalReferences(before.Cache, before.CacheValues)) return false;
        foreach (var saved in before.Rows)
            if (!book.TryGetValue(saved.Key, out var row) || !ReferenceEquals(row, saved.Row) ||
                row.UID != saved.Uid || row.UnKnow != saved.Unknown || row.Claim != saved.Claim) return false;
        return true;
    }

    private bool RevalidateGoals(GoalState before)
    {
        // Comparer code above may change an earlier row or the live owner. A
        // fresh physical capture and pure comparisons follow every lookup.
        if (!GoalsStillCurrent(before) || !TryCaptureGoals(before.Book, before.Gate, out var after)) return false;
        if (!ReferenceEquals(before.Comparer, after!.Comparer) || !ReferenceEquals(before.Cache, after.Cache) ||
            before.Dirty != after.Dirty || !SameGoalReferences(before.CacheValues, after.CacheValues) ||
            before.Rows.Length != after.Rows.Length) return false;
        for (int index = 0; index < before.Rows.Length; index++)
        {
            var left = before.Rows[index]; var right = after.Rows[index];
            if (left.Key != right.Key || !ReferenceEquals(left.Row, right.Row) || left.Uid != right.Uid ||
                left.Unknown != right.Unknown || left.Claim != right.Claim) return false;
        }
        return true;
    }

    private static bool SameGoalReferences(Item[] left, Item[] right)
    {
        if (left.Length != right.Length) return false;
        for (int index = 0; index < left.Length; index++)
            if (!ReferenceEquals(left[index], right[index])) return false;
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
