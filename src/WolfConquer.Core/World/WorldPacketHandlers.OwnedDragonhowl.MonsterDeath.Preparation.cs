namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Orchestrates concrete registered participants. This object is never a
    // substitute for the killed-incarnation, floor or Character writer owners.
    internal sealed partial class PreparedDragonhowlMonsterRewards : IDisposable
    {
        private readonly WorldSessionStore store;
        private readonly WorldSessionStore.OwnedDragonhowlNonPlayer target;
        private readonly WorldSessionStore.OwnedDragonhowlMonsterDrops drops;
        private readonly WorldSessionStore.OwnedDragonhowlDeathCps randomCps, bossCps;
        private readonly DragonhowlMonsterRewardApplicability applicability;
        private int started, disposed;

        internal WorldSessionStore.OwnedDragonhowlMonsterDeath Death { get; }
        internal WorldSession CalculatedAfter { get; }

        private PreparedDragonhowlMonsterRewards(WorldSessionStore store,
            WorldSessionStore.OwnedDragonhowlNonPlayer target,
            WorldSessionStore.OwnedDragonhowlMonsterDeath death,
            WorldSessionStore.OwnedDragonhowlMonsterDrops drops,
            WorldSessionStore.OwnedDragonhowlDeathCps randomCps,
            WorldSessionStore.OwnedDragonhowlDeathCps bossCps,
            DragonhowlMonsterRewardApplicability applicability, WorldSession calculatedAfter)
        {
            this.store = store; this.target = target; Death = death; this.drops = drops;
            this.randomCps = randomCps; this.bossCps = bossCps;
            this.applicability = applicability; CalculatedAfter = calculatedAfter;
        }

        internal static bool TryPrepare(WorldSessionStore store, WorldSessionStore.OwnedDragonhowlOwner owner,
            WorldSessionStore.OwnedDragonhowlNonPlayer target,
            out PreparedDragonhowlMonsterRewards? rewards, out string reason)
        {
            rewards = null; reason = "ownedDragonhowlMonsterDeathOwnerUnavailable";
            var death = store.PrepareDragonhowlMonsterDeath(owner, target);
            if (death is null || !store.TryReadDragonhowlMonsterDeathPreparation(death,
                    out var context, out var actor, out var monster, out var family)) return false;
            if (!DragonhowlMonsterRewardApplicability.TryCapture(context, actor, monster, family,
                    out var applicability, out reason) || applicability is null) return false;

            WorldSessionStore.OwnedDragonhowlMonsterDrops? drops = null;
            try
            {
                if (!store.TryPrepareDragonhowlMonsterDrops(death, out drops, out reason) || drops is null) return false;
                if (!store.TryPrepareDragonhowlDeathCps(death, actor, DragonhowlDeathCpsKind.Random,
                        out var random, out var afterRandom) || random is null)
                { reason = "ownedDragonhowlRandomCpsPreparationUnavailable"; return false; }
                if (!store.TryPrepareDragonhowlDeathCps(death, afterRandom, DragonhowlDeathCpsKind.Boss,
                        out var boss, out var afterBoss) || boss is null)
                { reason = "ownedDragonhowlBossCpsPreparationUnavailable"; return false; }
                var prepared = new PreparedDragonhowlMonsterRewards(store, target, death, drops,
                    random, boss, applicability, afterBoss);
                if (!prepared.IsPreparedCurrent())
                { reason = "ownedDragonhowlMonsterRewardPreparationChanged"; return false; }
                rewards = prepared; reason = string.Empty; return true;
            }
            finally { if (rewards is null) drops?.Dispose(); }
        }

        internal bool IsPreparedCurrent() => Volatile.Read(ref started) == 0 && Volatile.Read(ref disposed) == 0 &&
            applicability.IsCurrentPlain() && store.IsPreparedDragonhowlMonsterDeathCurrent(Death) &&
            store.IsPreparedDragonhowlMonsterDropsCurrent(drops);

        internal bool RecordedInputsAreCurrent() => Volatile.Read(ref disposed) == 0 && applicability.IsCurrentPlain() &&
            store.DragonhowlMonsterRewardInputsCurrent(Death);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0) drops.Dispose();
        }
    }
}
