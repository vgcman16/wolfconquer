using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacySuperGuildWarStateStore
{
    // This captures the real score store. It does not publish a score during
    // preflight; the registered proc's concrete event stage must consume it.
    internal PreparedDragonhowlScore? PrepareDragonhowlScore(
        WorldSessionStore.OwnedDragonhowlNonPlayer purpose, WorldSession actor,
        SobNpc original, SobNpc after, SobNpcDamageResult result, ulong guildId, string guildName)
    {
        if (!purpose.CoversSobEventPreparation(actor, original, after, result) ||
            !LegacySobNpcEventRules.IsSuperGuildWarPole(original) || result.DamageApplied == 0 ||
            guildId == 0 || string.IsNullOrWhiteSpace(guildName)) return null;
        lock (_sync) return PreparedDragonhowlScore.Capture(this, purpose, guildId, guildName, result);
    }

    internal sealed class PreparedDragonhowlScore
    {
        private readonly LegacySuperGuildWarStateStore store;
        private readonly WorldSessionStore.OwnedDragonhowlNonPlayer purpose;
        private readonly Dictionary<ulong, LegacySuperGuildWarScoreEntry> before, after;
        private readonly KeyValuePair<ulong, LegacySuperGuildWarScoreEntry>[] beforeRows, afterRows;
        private readonly ulong beforeVersion, afterVersion;
        private bool published;
        internal IReadOnlyList<LegacySuperGuildWarScoreEntry> Scored { get; }
        internal IReadOnlyList<LegacySuperGuildWarScoreEntry> Remaining { get; }
        internal LegacySuperGuildWarScoreEntry Winner => Scored[0];

        private PreparedDragonhowlScore(LegacySuperGuildWarStateStore store,
            WorldSessionStore.OwnedDragonhowlNonPlayer purpose, ulong guildId, string guildName, SobNpcDamageResult result)
        {
            this.store = store; this.purpose = purpose;
            before = store._scores; beforeRows = before.ToArray(); beforeVersion = store._scoreVersion;
            // The legacy finish path updates and then clears. Preserve both
            // mutations in the incarnation even though no intermediate graph escapes.
            afterVersion = checked(beforeVersion + (result.Killed ? 2UL : 1UL));
            var scored = new Dictionary<ulong, LegacySuperGuildWarScoreEntry>(before);
            scored[guildId] = new(guildId, guildName, scored.TryGetValue(guildId, out var prior)
                ? SaturatingAdd(prior.Score, result.DamageApplied) : result.DamageApplied);
            Scored = Array.AsReadOnly(scored.Values.OrderByDescending(row => row.Score).ThenBy(row => row.GuildId).ToArray());
            after = result.Killed ? [] : scored;
            afterRows = after.ToArray();
            Remaining = Array.AsReadOnly(after.Values.OrderByDescending(row => row.Score).ThenBy(row => row.GuildId).ToArray());
        }

        internal static PreparedDragonhowlScore Capture(LegacySuperGuildWarStateStore store,
            WorldSessionStore.OwnedDragonhowlNonPlayer purpose, ulong guildId, string guildName, SobNpcDamageResult result)
        {
            if (!Monitor.IsEntered(store._sync)) throw new InvalidOperationException("ownedDragonhowlScoreGateRequired");
            return new(store, purpose, guildId, guildName, result);
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
            if (!Monitor.IsEntered(store._sync) || published || !plan.UsesScore(this) ||
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
