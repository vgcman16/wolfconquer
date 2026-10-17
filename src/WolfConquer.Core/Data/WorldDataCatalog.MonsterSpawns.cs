namespace WolfConquer.Core.Data;

public sealed partial class WorldDataCatalog
{
    private const uint LegacyFirstMonsterRuntimeUid = 400_000;

    public IReadOnlyDictionary<MonsterSpawn, uint> LegacyMonsterRuntimeUidBases { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<MonsterSpawn>> MonsterSpawnsByGeneratorId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<MonsterSpawn>> MonsterSpawnsByMapId { get; }

    public uint GetLegacyMonsterRuntimeUidBase(MonsterSpawn spawn)
    {
        ArgumentNullException.ThrowIfNull(spawn);

        return LegacyMonsterRuntimeUidBases.TryGetValue(spawn, out var uidBase)
            ? uidBase
            : LegacyFirstMonsterRuntimeUid;
    }

    private static IReadOnlyDictionary<MonsterSpawn, uint> BuildLegacyMonsterRuntimeUidBases(
        IReadOnlyList<MonsterSpawn> monsterSpawns,
        IReadOnlyList<Monster> monsters)
    {
        var result = new Dictionary<MonsterSpawn, uint>();
        var monstersById = monsters.ToDictionary(monster => monster.Id);
        var nextUid = LegacyFirstMonsterRuntimeUid;

        foreach (var spawn in monsterSpawns)
        {
            result.TryAdd(spawn, nextUid);
            nextUid = checked(nextUid + GetLegacyMonsterRuntimeUidStride(spawn, monstersById));
        }

        return result;
    }

    private static uint GetLegacyMonsterRuntimeUidStride(
        MonsterSpawn spawn,
        IReadOnlyDictionary<int, Monster> monstersById)
    {
        var stride = monstersById.TryGetValue(spawn.MonsterTypeId, out var monster)
            ? LegacyMonsterSpawnCountRules.GetSpawnAttemptCount(spawn, monster)
            : Math.Max(1, Math.Max(spawn.MaxNpc, spawn.MaxPerGenerator));
        return checked((uint)Math.Max(1, stride));
    }

    private static bool IsLegacyLoadMapMonstersSpawn(MonsterSpawn spawn)
    {
        return LegacyMonsterSpawnCountRules.IsLegacyLoadMapMonstersSpawn(spawn);
    }
}
