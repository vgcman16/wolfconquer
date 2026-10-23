using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHeroRewardsRuntime
{
    public void AddGoal(uint id)
    {
        if (!TryAddGoal(id)) throw new InvalidDataException("Hero goal admission requires a current owner and a valid goal book.");
    }

    internal bool TryAddGoal(uint id) => id != 0 && TryWriteGoals([new(id, 0)]);

    private bool TryWriteGoals(GoalRecord[] incoming)
    {
        lock (Owner.SpellAdmissionSyncRoot)
        {
            var player = Owner.Player;
            if (player is null) return false;
            lock (player.SpellAdmissionSyncRoot)
                return TryPublishGoals(incoming, player, player.UID, commitXpTwenty: false);
        }
    }

    internal bool TryCommitXpTwenty(LegacyNativePlayerRuntime player, uint uid)
    {
        if (!Monitor.IsEntered(Owner.SpellAdmissionSyncRoot) ||
            !Monitor.IsEntered(player.SpellAdmissionSyncRoot)) return false;
        return TryPublishGoals([new(103, 0)], player, uid, commitXpTwenty: true);
    }

    private bool TryPublishGoals(GoalRecord[] incoming, LegacyNativePlayerRuntime player, uint uid, bool commitXpTwenty)
    {
        lock (goalSyncRoot)
        {
            var capturedSlot = Owner.HeroRewards;
            var book = ArrayGoals; var gate = book?.SyncRoot;
            if (book is null || gate is null || !HasCurrentOwner(player, uid, commitXpTwenty, capturedSlot)) return false;
            lock (gate)
            {
                try
                {
                    if (!TryCaptureGoals(book, gate, out var before) ||
                        !TryPrepareGoals(before!, incoming, out var prepared) || !RevalidateGoals(before!) ||
                        !HasCurrentOwner(player, uid, commitXpTwenty, capturedSlot)) return false;
                    // All lookups, comparer calls, allocations and validation are
                    // complete. These real physical assignments cannot fail.
                    if (prepared is not null) ArrayGoals = prepared;
                    if (commitXpTwenty) player.CommitXpCountTwenty();
                    return true;
                }
                catch (Exception) { return false; }
            }
        }
    }

    private static bool TryPrepareGoals(GoalState before, GoalRecord[] incoming,
        out LegacyNativeDbSafeDictionary<uint, Item>? prepared)
    {
        prepared = null;
        var rows = before.Rows.Select(row => new KeyValuePair<uint, Item>(row.Key, row.Row)).ToList();
        var seen = before.Rows.Select(row => row.Key).ToHashSet();
        foreach (var record in incoming)
        {
            if (record.Uid == 0) return false;
            if (!seen.Add(record.Uid)) continue; // Existing/first row and its Claim alias win.
            if (rows.Count == MaxGoalRecords) return false;
            rows.Add(new(record.Uid, new Item { UID = record.Uid, Claim = record.Claim }));
        }
        if (rows.Count == before.Rows.Length) return true;
        var candidate = new LegacyNativeDbSafeDictionary<uint, Item>(rows.Count, before.Comparer);
        foreach (var row in rows)
            ((Dictionary<uint, Item>)candidate).Add(row.Key, row.Value);
        candidate.MyArray = candidate.Values.ToArray();
        candidate.Update = false;
        prepared = candidate;
        return true;
    }
}
