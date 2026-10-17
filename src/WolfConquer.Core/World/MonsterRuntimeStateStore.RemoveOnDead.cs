namespace WolfConquer.Core.World;

public sealed partial class MonsterRuntimeStateStore
{
    private bool IsRemovedFromMapOnDeathNoLock(LegacyMonsterInstance instance)
    {
        return _states.TryGetValue(MonsterRuntimeKey.From(instance), out var state) &&
            state.RemovedFromMapOnDeath;
    }

    private bool HasPendingMonsterLifecycleObservationNoLock(LegacyMonsterInstance instance)
    {
        // Keep the dead entry/UID as a reservation against stale-reference reuse
        // and instance replay. Only map eligibility ends immediately on death.
        // Existing delayed removal and BlackSpot-off publication must still drain.
        return !_states.TryGetValue(MonsterRuntimeKey.From(instance), out var state) ||
            !state.RemovedFromMapOnDeath ||
            !state.DeathRemovalBroadcastSent ||
            state.PendingBlackSpotInactiveBroadcast;
    }
}
