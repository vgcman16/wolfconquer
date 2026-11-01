namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed partial class PreparedDragonhowlMonsterRewards
    {
        // Called after this target's actual damage/death packet. Each Store
        // method consumes its own registered phase and retains committed state
        // before output; a false return here is never an uncommitted rejection.
        internal async ValueTask<bool> PublishAsync(OwnedDragonhowlPreparation plan,
            CancellationToken cancellationToken)
        {
            if (!ReferenceEquals(plan.Store, store) || Volatile.Read(ref disposed) != 0 ||
                Interlocked.CompareExchange(ref started, 1, 0) != 0 || !applicability.IsCurrentPlain() ||
                !store.TryReadDragonhowlMonsterPublication(plan, target, out _, out _, out _, out _, out var killed) ||
                !killed.Killed || !store.TryBeginDragonhowlMonsterDeath(plan, Death)) return false;

            cancellationToken.ThrowIfCancellationRequested();
            if (!await store.PublishDragonhowlMonsterDropsAsync(plan, Death, drops, cancellationToken).ConfigureAwait(false) ||
                !applicability.IsCurrentPlain()) return false;
            if (!await store.PublishDragonhowlDeathCpsAsync(plan, Death, randomCps, cancellationToken).ConfigureAwait(false) ||
                !applicability.IsCurrentPlain()) return false;
            if (!await store.PublishDragonhowlDeathCpsAsync(plan, Death, bossCps, cancellationToken).ConfigureAwait(false) ||
                !applicability.IsCurrentPlain()) return false;

            // This proves the completed non-XP tail only. It does not advance
            // the enclosing proc phase or substitute for the subsequent team
            // XP continuation and its actual final-clear/query successors.
            return store.IsDragonhowlMonsterRewardsComplete(plan, Death);
        }
    }
}
