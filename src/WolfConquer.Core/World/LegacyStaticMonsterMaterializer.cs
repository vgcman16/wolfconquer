using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal readonly record struct LegacyStaticMonsterEntry(
    MonsterSpawn Spawn,
    Monster MonsterFamily,
    LegacyMonsterInstance Instance);

internal static class LegacyStaticMonsterMaterializer
{
    public static IEnumerable<LegacyStaticMonsterEntry> Enumerate(
        WorldDataCatalog worldData,
        IReadOnlyList<MonsterSpawn> spawns)
    {
        ArgumentNullException.ThrowIfNull(worldData);
        ArgumentNullException.ThrowIfNull(spawns);

        var occupiedTiles = new HashSet<(ushort X, ushort Y)>();
        foreach (var spawn in spawns)
        {
            if (!worldData.MonstersById.TryGetValue(spawn.MonsterTypeId, out var monsterFamily))
            {
                continue;
            }

            var uidBase = worldData.GetLegacyMonsterRuntimeUidBase(spawn);
            foreach (var instance in LegacyMonsterInstance.FromGenerator(spawn, monsterFamily, uidBase, occupiedTiles))
            {
                yield return new LegacyStaticMonsterEntry(spawn, monsterFamily, instance);
            }
        }
    }
}
