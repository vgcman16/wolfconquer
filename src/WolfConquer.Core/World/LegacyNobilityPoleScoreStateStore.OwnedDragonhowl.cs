using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNobilityPoleScoreStateStore
{
    internal PreparedDragonhowlScore? PrepareDragonhowlScore(WorldSessionStore.OwnedDragonhowlNonPlayer purpose,
        WorldSession actor, SobNpc original, SobNpc after, SobNpcDamageResult result,
        string schedule, string displayRank)
    {
        if (!purpose.CoversSobEventPreparation(actor, original, after, result) || result.DamageApplied == 0 ||
            !LegacySobNpcEventRules.TryGetNobilityPoleSchedule(original, out var actualSchedule, out _) ||
            !StringComparer.Ordinal.Equals(actualSchedule, schedule) || string.IsNullOrWhiteSpace(displayRank) ||
            actor.Character.Uid is <= 0 or > uint.MaxValue || string.IsNullOrWhiteSpace(actor.Character.Name)) return null;
        var paid = LegacyNativeAttributeMapProof.CaptureRaw(actor.Character.PaidNobility);
        lock (_sync) return PreparedDragonhowlScore.Capture(this, purpose, checked((uint)actor.Character.Uid),
            actor.Character.Name.Trim(), result, schedule, displayRank, paid);
    }

    internal sealed class PreparedDragonhowlScore
    {
        private readonly LegacyNobilityPoleScoreStateStore store;
        private readonly WorldSessionStore.OwnedDragonhowlNonPlayer purpose;
        private readonly Dictionary<LegacyNobilityPoleScoreKey, LegacyNobilityPoleScoreEntry> before, after;
        private readonly KeyValuePair<LegacyNobilityPoleScoreKey, LegacyNobilityPoleScoreEntry>[] beforeRows, afterRows;
        private readonly LegacyNativeAttributeMapProof paid;
        private readonly ulong beforeVersion, afterVersion;
        private bool published;
        internal IReadOnlyList<LegacyNobilityPoleScoreEntry> ScoredPartition { get; }
        internal IReadOnlyList<LegacyNobilityPoleScoreEntry> RemainingAll { get; }
        internal LegacyNobilityPoleScoreEntry Winner => ScoredPartition[0];

        private PreparedDragonhowlScore(LegacyNobilityPoleScoreStateStore store,
            WorldSessionStore.OwnedDragonhowlNonPlayer purpose, uint uid, string name, SobNpcDamageResult result,
            string schedule, string displayRank, LegacyNativeAttributeMapProof paid)
        {
            this.store = store; this.purpose = purpose; this.paid = paid;
            before = store._scores; beforeRows = before.ToArray(); beforeVersion = store._scoreVersion;
            afterVersion = checked(beforeVersion + (result.Killed ? 2UL : 1UL));
            var scored = new Dictionary<LegacyNobilityPoleScoreKey, LegacyNobilityPoleScoreEntry>(before);
            var key = new LegacyNobilityPoleScoreKey(schedule, uid);
            scored[key] = new(schedule, displayRank, uid, name, scored.TryGetValue(key, out var prior)
                ? SaturatingAdd(prior.Score, result.DamageApplied) : result.DamageApplied);
            ScoredPartition = Array.AsReadOnly(scored.Values.Where(row => StringComparer.Ordinal.Equals(row.ScheduleName, schedule))
                .OrderByDescending(row => row.Score).ThenBy(row => row.PlayerUid).ToArray());
            after = scored;
            if (result.Killed)
                foreach (var remove in after.Keys.Where(row => StringComparer.Ordinal.Equals(row.ScheduleName, schedule)).ToArray())
                    after.Remove(remove);
            afterRows = after.ToArray();
            RemainingAll = Array.AsReadOnly(after.Values.OrderBy(row => row.ScheduleName, StringComparer.Ordinal)
                .ThenByDescending(row => row.Score).ThenBy(row => row.PlayerUid).ToArray());
        }

        internal static PreparedDragonhowlScore Capture(LegacyNobilityPoleScoreStateStore store,
            WorldSessionStore.OwnedDragonhowlNonPlayer purpose, uint uid, string name, SobNpcDamageResult result,
            string schedule, string displayRank, LegacyNativeAttributeMapProof paid)
        {
            if (!Monitor.IsEntered(store._sync)) throw new InvalidOperationException("ownedDragonhowlNobilityScoreGateRequired");
            return new(store, purpose, uid, name, result, schedule, displayRank, paid);
        }

        internal IDisposable EnterCommitScope() => new ScoreScope(store._sync);
        internal bool IsCurrentPlain()
        {
            lock (store._sync)
            {
                var expected = published ? after : before;
                var rows = published ? afterRows : beforeRows;
                return paid.IsCurrentPlain() && store._scoreVersion == (published ? afterVersion : beforeVersion) &&
                    ReferenceEquals(store._scores, expected) && store._scores.Count == rows.Length &&
                    rows.All(row => store._scores.TryGetValue(row.Key, out var value) && value == row.Value);
            }
        }

        internal bool PublishPrepared(WorldPacketHandlers.PreparedDragonhowlSobEvent plan)
        {
            if (!Monitor.IsEntered(store._sync) || published || !plan.UsesNobilityScore(this) ||
                !purpose.CoversSobEventCommit(plan) || !IsCurrentPlain()) return false;
            store._scores = after;
            store._scoreVersion = afterVersion;
            published = true;
            return true;
        }

        private sealed class ScoreScope : IDisposable
        {
            private object? gate;
            internal ScoreScope(object gate) { this.gate = gate; Monitor.Enter(gate); }
            public void Dispose()
            {
                if (Interlocked.Exchange(ref gate, null) is { } held) Monitor.Exit(held);
            }
        }
    }
}
