using System.Globalization;

namespace WolfConquer.Core.Data;

internal static class LegacyMonsterSpawnCountRules
{
    private static readonly HashSet<int> LegacyMaxNpcSpawnMaps =
    [
        1011,
        1770,
        1771,
        1772,
        1773,
        1774,
        1775,
        1777,
        1782,
        1785,
        1786,
        1787,
        1794,
        3071,
    ];

    public static int GetSpawnAttemptCount(MonsterSpawn spawn, Monster monster)
    {
        ArgumentNullException.ThrowIfNull(spawn);
        ArgumentNullException.ThrowIfNull(monster);

        if (ReadOptionalByte(monster.Attributes, "Boss") > 0)
        {
            return 1;
        }

        if (IsLegacyLoadMapMonstersSpawn(spawn))
        {
            return 1;
        }

        if (LegacyMaxNpcSpawnMaps.Contains(spawn.MapId))
        {
            var spawnCount = spawn.MaxNpc;
            if (spawnCount > 1)
            {
                return (Math.Max(0, spawn.BoundWidth) / spawnCount) *
                    (Math.Max(0, spawn.BoundHeight) / spawnCount);
            }

            return checked(Math.Max(1, spawnCount) * 3);
        }

        return checked(Math.Max(1, spawn.MaxPerGenerator) * 3);
    }

    public static bool IsLegacyLoadMapMonstersSpawn(MonsterSpawn spawn)
    {
        ArgumentNullException.ThrowIfNull(spawn);

        return spawn.Attributes.TryGetValue("legacy_source", out var legacySource) &&
            string.Equals(legacySource, "LoadMapMonsters", StringComparison.Ordinal);
    }

    private static byte ReadOptionalByte(IReadOnlyDictionary<string, string> attributes, string key)
    {
        if (!attributes.TryGetValue(key, out var value))
        {
            return 0;
        }

        return byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid monster attribute '{key}'.");
    }
}
