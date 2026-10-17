namespace WolfConquer.Core.World;

public sealed partial class MonsterRuntimeStateStore
{
    internal LegacyMonsterInstance? FindMapRoleMonsterByUid(
        int mapId,
        uint uid,
        IEnumerable<LegacyMonsterInstance> staticInstances)
    {
        lock (_gate)
        {
            var instance = _dynamicMonstersByUid.TryGetValue(uid, out var dynamicEntry) &&
                dynamicEntry.Instance.MapId == mapId
                ? dynamicEntry.Instance
                : staticInstances.FirstOrDefault(candidate => candidate.MapId == mapId && candidate.Uid == uid);
            if (instance is null || !_states.TryGetValue(MonsterRuntimeKey.From(instance), out var state))
            {
                return instance;
            }

            if (state.RemovedFromMapOnDeath)
            {
                return null;
            }

            // Normal MonsterRole.RemoveView moves the retained dead role to its respawn tile.
            var returnedToRespawn = state.DeadAt is not null && state.DeathRemovalBroadcastSent;
            return instance with
            {
                X = returnedToRespawn ? instance.X : state.X,
                Y = returnedToRespawn ? instance.Y : state.Y,
                Hitpoints = state.Hitpoints,
            };
        }
    }
}
