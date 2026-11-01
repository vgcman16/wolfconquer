using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyClassPolePlayerScoreStateStore
{
    internal PreparedDragonhowlScore? PrepareDragonhowlScore(WorldSessionStore.OwnedDragonhowlNonPlayer purpose,
        WorldSession actor, SobNpc original, SobNpc after, SobNpcDamageResult result)
    {
        if (!purpose.CoversSobEventPreparation(actor, original, after, result) || result.DamageApplied == 0 ||
            actor.Character.Uid is <= 0 or > uint.MaxValue || string.IsNullOrWhiteSpace(actor.Character.Name)) return null;
        lock (_sync) return PreparedDragonhowlScore.Capture(this, purpose, checked((uint)actor.Character.Uid),
            actor.Character.Name.Trim(), result);
    }

    internal sealed class PreparedDragonhowlScore
    {
        private readonly LegacyClassPolePlayerScoreStateStore store;
        private readonly WorldSessionStore.OwnedDragonhowlNonPlayer purpose;
        private readonly Dictionary<uint, LegacyClassPolePlayerScoreEntry> before, after;
        private readonly KeyValuePair<uint, LegacyClassPolePlayerScoreEntry>[] beforeRows, afterRows;
        private readonly ulong beforeVersion, afterVersion;
        private bool published;
        internal IReadOnlyList<LegacyClassPolePlayerScoreEntry> Scored { get; }
        internal IReadOnlyList<LegacyClassPolePlayerScoreEntry> Remaining { get; }
        internal LegacyClassPolePlayerScoreEntry Winner => Scored[0];

        private PreparedDragonhowlScore(LegacyClassPolePlayerScoreStateStore store,
            WorldSessionStore.OwnedDragonhowlNonPlayer purpose, uint uid, string name, SobNpcDamageResult result)
        {
            this.store = store; this.purpose = purpose;
            before = store._scores; beforeRows = before.ToArray(); beforeVersion = store._scoreVersion;
            afterVersion = checked(beforeVersion + (result.Killed ? 2UL : 1UL));
            var scored = new Dictionary<uint, LegacyClassPolePlayerScoreEntry>(before);
            scored[uid] = new(uid, name, scored.TryGetValue(uid, out var prior)
                ? LegacyCombatDamageCalculator.AddLegacySaturating(prior.Score, result.DamageApplied) : result.DamageApplied);
            Scored = Array.AsReadOnly(scored.Values.OrderByDescending(row => row.Score).ThenBy(row => row.PlayerUid).ToArray());
            after = result.Killed ? [] : scored;
            afterRows = after.ToArray();
            Remaining = Array.AsReadOnly(after.Values.OrderByDescending(row => row.Score).ThenBy(row => row.PlayerUid).ToArray());
        }

        internal static PreparedDragonhowlScore Capture(LegacyClassPolePlayerScoreStateStore store,
            WorldSessionStore.OwnedDragonhowlNonPlayer purpose, uint uid, string name, SobNpcDamageResult result)
        {
            if (!Monitor.IsEntered(store._sync)) throw new InvalidOperationException("ownedDragonhowlClassScoreGateRequired");
            return new(store, purpose, uid, name, result);
        }

        internal IDisposable EnterCommitScope() => new ScoreScope(store._sync);
        internal bool IsCurrentPlain()
        {
            lock (store._sync)
            {
                var expected = published ? after : before;
                var rows = published ? afterRows : beforeRows;
                return store._scoreVersion == (published ? afterVersion : beforeVersion) &&
                    ReferenceEquals(store._scores, expected) && store._scores.Count == rows.Length &&
                    rows.All(row => store._scores.TryGetValue(row.Key, out var value) && value == row.Value);
            }
        }

        internal bool PublishPrepared(WorldPacketHandlers.PreparedDragonhowlSobEvent plan)
        {
            if (!Monitor.IsEntered(store._sync) || published || !plan.UsesClassScore(this) ||
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
