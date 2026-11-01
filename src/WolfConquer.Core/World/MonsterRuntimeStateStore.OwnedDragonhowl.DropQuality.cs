namespace WolfConquer.Core.World;

public sealed partial class MonsterRuntimeStateStore
{
    // Closed DATA transition shared with the existing live generator. Earlier
    // watcher hits intentionally leave later watcher counters untouched.
    private readonly record struct LegacyMonsterQualityCounters(int Refined, int Unique, int Elite, int Super)
    {
        internal LegacyMonsterQualityCounters Advance(out byte quality)
        {
            if (Refined is < 0 or >= 10 || Unique is < 0 or >= 20 || Elite is < 0 or >= 25 || Super is < 0 or >= 30)
                throw new InvalidDataException("monsterDropQualityCounterInvalid");
            int refined = Refined + 1, unique = Unique, elite = Elite, super = Super;
            if (refined == 10) { quality = 6; return new(0, unique, elite, super); }
            if (++unique == 20) { quality = 7; return new(refined, 0, elite, super); }
            if (++elite == 25) { quality = 8; return new(refined, unique, 0, super); }
            if (++super == 30) { quality = 9; return new(refined, unique, elite, 0); }
            quality = 3; return new(refined, unique, elite, super);
        }
    }

    internal sealed class PreparedDragonhowlDropQuality
    {
        private readonly MonsterRuntimeStateStore store;
        private readonly WorldSessionStore.OwnedDragonhowlDropQuality purpose;
        private readonly int family;
        private readonly PreparedDragonhowlDropQuality? previous;
        private readonly LegacyMonsterItemGeneratorState state;
        private readonly bool initiallyAbsent;
        private readonly LegacyMonsterQualityCounters before, after;
        private readonly long beforeVersion, afterVersion;
        private bool committed;
        internal byte Quality { get; }

        private PreparedDragonhowlDropQuality(MonsterRuntimeStateStore store,
            WorldSessionStore.OwnedDragonhowlDropQuality purpose, int family,
            LegacyMonsterItemGeneratorState state, bool initiallyAbsent,
            LegacyMonsterQualityCounters before, long version, PreparedDragonhowlDropQuality? previous)
        {
            this.store = store; this.purpose = purpose; this.family = family; this.state = state;
            this.initiallyAbsent = initiallyAbsent; this.before = before; beforeVersion = version; this.previous = previous;
            after = before.Advance(out var quality); Quality = quality; afterVersion = checked(version + 1);
        }

        internal static PreparedDragonhowlDropQuality? TryPrepare(MonsterRuntimeStateStore store,
            WorldSessionStore.OwnedDragonhowlDropQuality purpose)
        {
            // Real lifecycle/actor owners precede the monster gate.
            if (!purpose.TryReadPreparation(store, out int family, out var previous)) return null;
            lock (store._gate)
            {
                if (previous is not null)
                {
                    if (!ReferenceEquals(previous.store, store) || previous.family != family || previous.committed ||
                        !previous.IsSequenceCurrent()) return null;
                    return new(store, purpose, family, previous.state, previous.initiallyAbsent,
                        previous.after, previous.afterVersion, previous);
                }
                bool missing = !store._itemGeneratorsByMonsterId.TryGetValue(family, out var actual);
                var state = actual ?? new LegacyMonsterItemGeneratorState();
                return new(store, purpose, family, state, missing, state.Counters, state.Version, null);
            }
        }

        // This is a sequence witness, not a supplied counter or a grant. During
        // preparation nothing is inserted into the live generator dictionary.
        internal bool IsSequenceCurrent()
        {
            lock (store._gate)
            {
                var latest = this;
                while (!latest.committed && latest.previous is not null) latest = latest.previous;
                if (!latest.committed && latest.initiallyAbsent)
                    return !store._itemGeneratorsByMonsterId.ContainsKey(family);
                return store._itemGeneratorsByMonsterId.TryGetValue(family, out var actual) && ReferenceEquals(actual, state) &&
                    actual.Counters == (latest.committed ? latest.after : latest.before) &&
                    actual.Version == (latest.committed ? latest.afterVersion : latest.beforeVersion);
            }
        }

        internal bool TryCommit(WorldSessionStore.OwnedDragonhowlDropQuality actualPurpose)
        {
            if (!ReferenceEquals(actualPurpose, purpose) || !purpose.TryReadCommit(this)) return false;
            lock (store._gate)
            {
                if (committed || previous is { committed: false } || !IsSequenceCurrent()) return false;
                bool exists = store._itemGeneratorsByMonsterId.TryGetValue(family, out var actual);
                if (exists ? !ReferenceEquals(actual, state) || actual.Counters != before || actual.Version != beforeVersion :
                    !initiallyAbsent || previous is not null) return false;
                if (!exists) store._itemGeneratorsByMonsterId.Add(family, state);
                state.Counters = after; state.Version = afterVersion; committed = true;
                return true;
            }
        }
    }
}
