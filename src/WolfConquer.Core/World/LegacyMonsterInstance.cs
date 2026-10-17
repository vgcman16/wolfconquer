using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed record LegacyMonsterInstance(
    uint Uid,
    int MapId,
    ushort X,
    ushort Y,
    uint Mesh,
    byte Level,
    uint Hitpoints,
    uint MaxLife,
    uint MonsterId,
    string Name,
    bool Boss,
    int GeneratorId,
    int SpawnIndex,
    int RestSeconds = 0,
    uint DynamicId = 0,
    bool IsFloor = false,
    uint RuntimeOwnerUid = 0)
{
    // Source MonsterRole owns this per spawn, not per monster family.
    public bool RemoveOnDead { get; init; }

    private const uint LegacyFirstMonsterUid = 400_000;
    public static IReadOnlyList<LegacyMonsterInstance> FromGenerator(MonsterSpawn spawn, Monster monster)
    {
        ArgumentNullException.ThrowIfNull(spawn);
        ArgumentNullException.ThrowIfNull(monster);

        if (spawn.MonsterTypeId != monster.Id)
        {
            throw new ArgumentException(
                "Monster spawn type must match the monster family id.",
                nameof(monster));
        }

        return CreateInstances(spawn, monster, index => CheckedUid(spawn, index));
    }

    public static IReadOnlyList<LegacyMonsterInstance> FromGenerator(MonsterSpawn spawn, Monster monster, uint uidBase)
    {
        ArgumentNullException.ThrowIfNull(spawn);
        ArgumentNullException.ThrowIfNull(monster);

        if (spawn.MonsterTypeId != monster.Id)
        {
            throw new ArgumentException(
                "Monster spawn type must match the monster family id.",
                nameof(monster));
        }

        return CreateInstances(spawn, monster, index => CheckedRuntimeUid(uidBase, index));
    }

    internal static IReadOnlyList<LegacyMonsterInstance> FromGenerator(
        MonsterSpawn spawn,
        Monster monster,
        uint uidBase,
        ISet<(ushort X, ushort Y)> occupiedTiles)
    {
        ArgumentNullException.ThrowIfNull(spawn);
        ArgumentNullException.ThrowIfNull(monster);
        ArgumentNullException.ThrowIfNull(occupiedTiles);

        if (spawn.MonsterTypeId != monster.Id)
        {
            throw new ArgumentException(
                $"Spawn monster type {spawn.MonsterTypeId} does not match monster {monster.Id}.",
                nameof(monster));
        }

        return CreateInstances(spawn, monster, index => CheckedRuntimeUid(uidBase, index), occupiedTiles);
    }

    public static LegacyMonsterInstance FromGenerator(MonsterSpawn spawn, Monster monster, int spawnIndex)
    {
        ArgumentNullException.ThrowIfNull(spawn);
        ArgumentNullException.ThrowIfNull(monster);

        if (spawnIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(spawnIndex), spawnIndex, "Spawn index cannot be negative.");
        }

        if (spawn.MonsterTypeId != monster.Id)
        {
            throw new ArgumentException(
                "Monster spawn type must match the monster family id.",
                nameof(monster));
        }

        var (x, y) = ProjectSpawnPoint(spawn, spawnIndex);
        return CreateInstance(spawn, monster, spawnIndex, CheckedUid(spawn, spawnIndex), x, y);
    }

    public static LegacyMonsterInstance FromGenerator(MonsterSpawn spawn, Monster monster, int spawnIndex, uint uidBase)
    {
        ArgumentNullException.ThrowIfNull(spawn);
        ArgumentNullException.ThrowIfNull(monster);

        if (spawnIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(spawnIndex), spawnIndex, "Spawn index cannot be negative.");
        }

        if (spawn.MonsterTypeId != monster.Id)
        {
            throw new ArgumentException(
                "Monster spawn type must match the monster family id.",
                nameof(monster));
        }

        var (x, y) = ProjectSpawnPoint(spawn, spawnIndex);
        return CreateInstance(spawn, monster, spawnIndex, CheckedRuntimeUid(uidBase, spawnIndex), x, y);
    }

    private static IReadOnlyList<LegacyMonsterInstance> CreateInstances(
        MonsterSpawn spawn,
        Monster monster,
        Func<int, uint> resolveUid,
        ISet<(ushort X, ushort Y)>? occupiedTiles = null)
    {
        var count = GetLegacySpawnAttemptCount(spawn, monster);
        var instances = new List<LegacyMonsterInstance>(count);
        var occupied = occupiedTiles ?? new HashSet<(ushort X, ushort Y)>();
        for (var index = 0; index < count; index++)
        {
            if (!TryProjectUnoccupiedSpawnPoint(spawn, index, occupied, out var x, out var y))
            {
                continue;
            }

            occupied.Add((x, y));
            instances.Add(CreateInstance(spawn, monster, index, resolveUid(index), x, y));
        }

        return instances;
    }

    private static LegacyMonsterInstance CreateInstance(
        MonsterSpawn spawn,
        Monster monster,
        int spawnIndex,
        uint uid,
        ushort x,
        ushort y)
    {
        var boss = ReadOptionalByte(monster.Attributes, "Boss") > 0;
        var maxLife = ReadRequiredUInt32(monster.Attributes, "life");

        return new LegacyMonsterInstance(
            Uid: uid,
            spawn.MapId,
            x,
            y,
            ReadRequiredUInt32(monster.Attributes, "lookface"),
            ReadRequiredByte(monster.Attributes, "level"),
            maxLife,
            maxLife,
            ResolveLegacyMonsterId(monster),
            monster.Name,
            boss,
            spawn.GeneratorId,
            spawnIndex,
            spawn.RestSeconds,
            ReadOptionalUInt32(spawn.Attributes, "DynamicID", "DynamicId", "dynamicid", "dinamicid"));
    }

    public LegacyMonsterSpawnPacket ToSpawnPacket(
        ushort action = 0,
        LegacyClientCompatibilityOptions? clientCompatibility = null,
        IReadOnlyList<ulong>? statusFlags = null)
    {
        var mesh = LegacyClientAppearanceCompatibility.ResolveMonsterMesh(Mesh, Name, clientCompatibility);
        if (IsFloor && Mesh == 980)
        {
            return LegacyMonsterSpawnPacket.FromOwnerPet(
                uid: Uid,
                mesh: mesh,
                level: Level,
                hitpoints: Hitpoints,
                maxLife: MaxLife,
                x: X,
                y: Y,
                ownerUid: RuntimeOwnerUid,
                ownerFacing: 0,
                ownerGuildId: 0,
                ownerPet: 3,
                ownerPet1: 15,
                monsterId: MonsterId,
                name: Name,
                action: action,
                statusFlags: statusFlags);
        }

        return LegacyMonsterSpawnPacket.FromMonsterRole(
            uid: Uid,
            mesh: mesh,
            level: Level,
            hitpoints: Hitpoints,
            maxLife: MaxLife,
            x: X,
            y: Y,
            action: action,
            monsterId: MonsterId,
            name: Name,
            boss: Boss,
            statusFlags: statusFlags);
    }

    private static int GetLegacySpawnAttemptCount(MonsterSpawn spawn, Monster monster)
    {
        return LegacyMonsterSpawnCountRules.GetSpawnAttemptCount(spawn, monster);
    }

    private static (ushort X, ushort Y) ProjectSpawnPoint(MonsterSpawn spawn, int spawnIndex)
    {
        var width = Math.Max(1, spawn.BoundWidth);
        var height = Math.Max(1, spawn.BoundHeight);
        var xOffset = PositiveModulo(spawn.GeneratorId + (spawnIndex * 37), width);
        var yOffset = PositiveModulo(spawn.MonsterTypeId + (spawnIndex * 53), height);

        return (
            CheckedUInt16(spawn.BoundX + xOffset, nameof(spawn.BoundX)),
            CheckedUInt16(spawn.BoundY + yOffset, nameof(spawn.BoundY)));
    }

    private static bool TryProjectUnoccupiedSpawnPoint(
        MonsterSpawn spawn,
        int spawnIndex,
        ISet<(ushort X, ushort Y)> occupied,
        out ushort x,
        out ushort y)
    {
        var projected = ProjectSpawnPoint(spawn, spawnIndex);
        if (!occupied.Contains(projected))
        {
            x = projected.X;
            y = projected.Y;
            return true;
        }

        var width = Math.Max(1, spawn.BoundWidth);
        var height = Math.Max(1, spawn.BoundHeight);
        var area = (long)width * height;
        for (var offset = 0L; offset < area; offset++)
        {
            var candidate = (spawnIndex + offset) % area;
            var candidateX = CheckedUInt16(spawn.BoundX + (int)(candidate % width), nameof(spawn.BoundX));
            var candidateY = CheckedUInt16(spawn.BoundY + (int)(candidate / width), nameof(spawn.BoundY));
            if (occupied.Contains((candidateX, candidateY)))
            {
                continue;
            }

            x = candidateX;
            y = candidateY;
            return true;
        }

        x = 0;
        y = 0;
        return false;
    }

    private static int PositiveModulo(int value, int divisor)
    {
        var result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    private static uint CheckedUid(MonsterSpawn spawn, int spawnIndex)
    {
        if (ReadSpawnPathOrdinal(spawn.SourceRelativePath) > 1)
        {
            return 100_000_000U + (StableSpawnHash(spawn, spawnIndex) % 3_000_000_000U);
        }

        var uid = LegacyFirstMonsterUid +
            checked((uint)spawn.GeneratorId * 1_000U) +
            checked((uint)spawnIndex);
        return uid;
    }

    private static uint CheckedRuntimeUid(uint uidBase, int spawnIndex)
    {
        return checked(uidBase + (uint)spawnIndex);
    }

    private static uint ReadSpawnPathOrdinal(string sourceRelativePath)
    {
        if (string.IsNullOrWhiteSpace(sourceRelativePath))
        {
            return 0;
        }

        var fileName = Path.GetFileNameWithoutExtension(sourceRelativePath);
        return uint.TryParse(fileName, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ordinal)
            ? ordinal
            : 0;
    }

    private static uint StableSpawnHash(MonsterSpawn spawn, int spawnIndex)
    {
        const uint fnvOffset = 2_166_136_261;
        const uint fnvPrime = 16_777_619;

        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"{spawn.MapId}|{spawn.SourceRelativePath}|{spawn.GeneratorId}|{spawnIndex}");
        var hash = fnvOffset;
        foreach (var character in text)
        {
            hash ^= character;
            hash *= fnvPrime;
        }

        return hash;
    }

    private static ushort CheckedUInt16(int value, string name)
    {
        if (value is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy monster instance value cannot be outside ushort range.");
        }

        return (ushort)value;
    }

    private static uint ResolveLegacyMonsterId(Monster monster)
    {
        return monster.Name switch
        {
            "NemesisTyrant" => 4220,
            "BloodyBanshee" => 3976,
            "DragonWraith" => 3971,
            "QueenofEvil" => 3970,
            "RockMonsterKing" => 8311,
            "NetherTyrant" => 3978,
            "ThrillingSpook" => 4212,
            "SnowBanshee" => 4171,
            "FrostSerpent" => 2414,
            "ChillingSpook" => 3977,
            "BladeDevilLord" => 2417,
            "BladeDevilKing" => 2418,
            "BladeDevilEnvoy" => 2415,
            "SerpentLord" => 2412,
            "NightmareCaptain" => 7563,
            "RedDevilL118" => 4858,
            "MonkeyKing" => 8305,
            "MonkeyMessenger" => 8105,
            "MonkeyAide" => 8205,
            _ when monster.Id == 20010 => 2416,
            _ when monster.Id == 20000 => 4860,
            _ => CheckedUInt32(monster.Id, nameof(monster.Id)),
        };
    }

    private static uint ReadRequiredUInt32(IReadOnlyDictionary<string, string> attributes, string key)
    {
        if (!attributes.TryGetValue(key, out var value) ||
            !uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Invalid or missing monster attribute '{key}'.");
        }

        return parsed;
    }

    private static byte ReadRequiredByte(IReadOnlyDictionary<string, string> attributes, string key)
    {
        if (!attributes.TryGetValue(key, out var value) ||
            !byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Invalid or missing monster attribute '{key}'.");
        }

        return parsed;
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

    private static uint ReadOptionalUInt32(IReadOnlyDictionary<string, string> attributes, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (attributes.TryGetValue(key, out var value) &&
                uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }

    private static uint CheckedUInt32(int value, string name)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy monster instance value cannot be outside uint range.");
        }

        return (uint)value;
    }
}
