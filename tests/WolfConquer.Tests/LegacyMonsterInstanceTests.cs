using System.Buffers;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMonsterInstanceTests
{
    [Fact]
    public void LegacyMonsterInstanceProjectsNormalGeneratorIntoRuntimeLikeInstances()
    {
        var spawn = Spawn(generatorId: 70, maxPerGenerator: 2);
        var monster = Monster(
            id: 76,
            name: "BladeGhostL83",
            lookface: "0200",
            level: "0083",
            life: "23346");

        var instances = LegacyMonsterInstance.FromGenerator(spawn, monster);

        Assert.Equal(6, instances.Count);
        var first = instances[0];
        Assert.Equal(470000U, first.Uid);
        Assert.Equal(1000, first.MapId);
        Assert.Equal(192, first.X);
        Assert.Equal(345, first.Y);
        Assert.Equal(200U, first.Mesh);
        Assert.Equal((byte)83, first.Level);
        Assert.Equal(23346U, first.Hitpoints);
        Assert.Equal(23346U, first.MaxLife);
        Assert.Equal(76U, first.MonsterId);
        Assert.Equal("BladeGhostL83", first.Name);
        Assert.False(first.Boss);
        Assert.Equal(70, first.GeneratorId);
        Assert.Equal(0, first.SpawnIndex);
    }

    [Fact]
    public void LegacyMonsterInstanceUsesMaxNpcForLegacySpecialSpawnMaps()
    {
        var spawn = Spawn(
            generatorId: 57,
            maxNpc: 39,
            maxPerGenerator: 30,
            mapId: 1011,
            monsterTypeId: 66,
            sourceMonsterName: "BanditL33",
            sourceRelativePath: Path.Combine("1011", "BanditL33", "1.ini"));
        var monster = Monster(
            id: 66,
            name: "BanditL33",
            lookface: "0601",
            level: "0033",
            life: "2451");

        var instances = LegacyMonsterInstance.FromGenerator(spawn, monster);

        Assert.Equal(6, instances.Count);
        Assert.Equal(457000U, instances[0].Uid);
        Assert.Equal(457005U, instances[^1].Uid);
    }

    [Fact]
    public void LegacyMonsterInstanceUsesRuntimeUidBaseForHigherOrdinalSpawnFiles()
    {
        var spawn = Spawn(
            generatorId: 77,
            maxPerGenerator: 3,
            mapId: 1015,
            monsterTypeId: 77,
            sourceMonsterName: "BirdmanL88",
            sourceRelativePath: Path.Combine("1015", "BirdmanL88", "5.ini"));
        var monster = Monster(
            id: 77,
            name: "BirdmanL88",
            lookface: "0605",
            level: "0088",
            life: "26844");

        var instances = LegacyMonsterInstance.FromGenerator(spawn, monster, uidBase: 400_123);

        Assert.Equal(
            Enumerable.Range(400_123, 9).Select(uid => (uint)uid).ToArray(),
            instances.Select(instance => instance.Uid).ToArray());
        Assert.All(instances, instance => Assert.InRange(instance.Uid, 400_000U, 999_999U));
    }

    [Fact]
    public void WorldDataCatalogRuntimeUidBasesReserveLegacyMultipliedGeneratorStride()
    {
        var firstSpawn = Spawn(generatorId: 10, maxPerGenerator: 3);
        var secondSpawn = Spawn(generatorId: 11, maxPerGenerator: 1);
        var monster = Monster(
            id: 76,
            name: "BladeGhostL83",
            lookface: "0200",
            level: "0083",
            life: "23346");
        var catalog = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(
            monsters: [monster],
            monsterSpawns: [firstSpawn, secondSpawn]);

        Assert.Equal(400_000U, catalog.GetLegacyMonsterRuntimeUidBase(firstSpawn));
        Assert.Equal(400_009U, catalog.GetLegacyMonsterRuntimeUidBase(secondSpawn));
    }

    [Fact]
    public void LegacyMonsterInstanceDoesNotDuplicateOneTileGuardGenerators()
    {
        var spawn = Spawn(
            generatorId: 107,
            maxNpc: 1,
            maxPerGenerator: 1,
            mapId: 1011,
            monsterTypeId: 900,
            boundX: 197,
            boundY: 230,
            boundWidth: 1,
            boundHeight: 1,
            sourceMonsterName: "Guard1",
            sourceRelativePath: Path.Combine("1011", "Guard1", "1.ini"));
        var monster = Monster(
            id: 900,
            name: "Guard1",
            lookface: "0900",
            level: "0200",
            life: "70000");

        var instance = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, monster));

        Assert.Equal(507000U, instance.Uid);
        Assert.Equal(197, instance.X);
        Assert.Equal(230, instance.Y);
    }

    [Fact]
    public void LegacyMonsterInstanceDoesNotStackWhenLegacyMultiplierExceedsBoundTiles()
    {
        var spawn = Spawn(
            generatorId: 70,
            maxPerGenerator: 2,
            boundX: 10,
            boundY: 20,
            boundWidth: 2,
            boundHeight: 1);
        var monster = Monster(
            id: 76,
            name: "BladeGhostL83",
            lookface: "0200",
            level: "0083",
            life: "23346");

        var instances = LegacyMonsterInstance.FromGenerator(spawn, monster);

        Assert.Equal(2, instances.Count);
        Assert.Equal(instances.Count, instances.Select(instance => (instance.X, instance.Y)).Distinct().Count());
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task LegacyMonsterInstanceSeedGuardProjectionHasNoStackedGuardTiles()
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        Assert.True(Directory.Exists(seedPath), $"Expected seed-json world data at {seedPath}.");

        var catalog = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
        var guardInstances = new List<LegacyMonsterInstance>();
        foreach (var spawn in catalog.MonsterSpawns)
        {
            if (!catalog.MonstersById.TryGetValue(spawn.MonsterTypeId, out var monster) ||
                !IsLegacySeedGuardMonster(monster))
            {
                continue;
            }

            guardInstances.AddRange(LegacyMonsterInstance.FromGenerator(
                spawn,
                monster,
                catalog.GetLegacyMonsterRuntimeUidBase(spawn)));
        }

        Assert.NotEmpty(guardInstances);
        Assert.Contains(guardInstances, instance => instance.MapId == 1002 && instance.Name == "Guard1");
        var stackedTiles = guardInstances
            .GroupBy(instance => (instance.MapId, instance.DynamicId, instance.X, instance.Y))
            .Where(group => group.Count() > 1)
            .Select(group =>
            {
                var sample = string.Join(
                    ",",
                    group.Take(5).Select(instance => $"{instance.Name}#{instance.GeneratorId}:{instance.SpawnIndex}"));
                return $"map={group.Key.MapId} dynamic={group.Key.DynamicId} x={group.Key.X} y={group.Key.Y} count={group.Count()} sample={sample}";
            })
            .ToArray();
        Assert.Empty(stackedTiles);
    }

    [Fact]
    public void LegacyMonsterInstanceKeepsProjectedPointsInsideLegacyExclusiveBounds()
    {
        var spawn = Spawn(generatorId: 70, maxPerGenerator: 4);
        var monster = Monster(
            id: 76,
            name: "BladeGhostL83",
            lookface: "0200",
            level: "0083",
            life: "23346");

        var instances = LegacyMonsterInstance.FromGenerator(spawn, monster);

        Assert.All(
            instances,
            instance =>
            {
                Assert.InRange(instance.X, spawn.BoundX, spawn.BoundX + spawn.BoundWidth - 1);
                Assert.InRange(instance.Y, spawn.BoundY, spawn.BoundY + spawn.BoundHeight - 1);
            });
    }

    [Fact]
    public void LegacyMonsterInstanceCreatesOneInstanceForLoadMapMonstersRows()
    {
        var spawn = Spawn(
            generatorId: 12_345,
            maxPerGenerator: 1,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["legacy_source"] = "LoadMapMonsters",
            });
        var monster = Monster(
            id: 76,
            name: "BladeGhostL83",
            lookface: "0200",
            level: "0083",
            life: "23346");

        var instance = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, monster));

        Assert.Equal(12_745_000U, instance.Uid);
        Assert.Equal(12_345, instance.GeneratorId);
        Assert.Equal(0, instance.SpawnIndex);
    }

    [Fact]
    public void LegacyMonsterInstanceKeepsLoadMapMonsters10166OneTileGuardSingleVisible()
    {
        var spawn = Spawn(
            generatorId: 434_163,
            maxNpc: 1,
            maxPerGenerator: 5,
            mapId: 10_166,
            monsterTypeId: 900,
            boundX: 72,
            boundY: 127,
            boundWidth: 1,
            boundHeight: 1,
            sourceMonsterName: "Guard1",
            sourceRelativePath: "Monsters10166.txt",
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["legacy_source"] = "LoadMapMonsters",
            });
        var monster = Monster(
            id: 900,
            name: "Guard1",
            lookface: "0900",
            level: "0200",
            life: "70000");

        var instance = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, monster));

        Assert.Equal(72, instance.X);
        Assert.Equal(127, instance.Y);
        Assert.Equal("Guard1", instance.Name);
    }

    [Fact]
    public void LegacyMonsterInstanceCreatesOneInstanceForBossGenerator()
    {
        var spawn = Spawn(generatorId: 9001, monsterTypeId: 4212, maxPerGenerator: 25);
        var monster = Monster(
            id: 4212,
            name: "ThrillingSpook",
            lookface: "4212",
            level: "0120",
            life: "100000",
            boss: "1");

        var instance = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, monster));

        Assert.True(instance.Boss);
        Assert.Equal(4212U, instance.MonsterId);
        var frame = ReadFrame(instance.ToSpawnPacket().ToFrameBytes());
        Assert.True(LegacyMonsterSpawnPacket.TryParse(frame, out var packet));
        Assert.Equal(10000U, packet.Hitpoints);
        Assert.True(packet.Boss);
    }

    [Fact]
    public void LegacyMonsterInstanceProjectsMesh980FloorMonsterAsOwnerPetSpawn()
    {
        var instance = new LegacyMonsterInstance(
            Uid: 700_201,
            MapId: 1002,
            X: 831,
            Y: 585,
            Mesh: 980,
            Level: 140,
            Hitpoints: 14,
            MaxLife: 14,
            MonsterId: 999_996_641,
            Name: "Thundercloud",
            Boss: true,
            GeneratorId: 0,
            SpawnIndex: 0,
            IsFloor: true,
            RuntimeOwnerUid: 1_293_600);

        var frame = ReadFrame(instance.ToSpawnPacket().ToFrameBytes());

        Assert.True(LegacyMonsterSpawnPacket.TryParse(frame, out var packet));
        Assert.Equal(980U, packet.Mesh);
        Assert.Equal(3U, packet.OwnerPet);
        Assert.Equal(15U, packet.OwnerPet1);
        Assert.Equal(1_293_600U, packet.OwnerUid);
        Assert.Equal(4_264U, packet.MonsterId);
        Assert.Equal(14U, packet.Hitpoints);
        Assert.Equal(14U, packet.MaxLife);
        Assert.False(packet.Boss);
    }

    [Theory]
    [InlineData(156U, 156U, 157U)]
    [InlineData(556U, 556U, 557U)]
    public void LegacyMonsterInstanceMapsKnownMissingMeshesForNewerClient(
        uint sourceMesh,
        uint expectedDecompiledMesh,
        uint expectedNewerClientMesh)
    {
        var instance = new LegacyMonsterInstance(
            Uid: 410_162,
            MapId: 1002,
            X: 420,
            Y: 360,
            Mesh: sourceMesh,
            Level: 48,
            Hitpoints: 1_578,
            MaxLife: 1_578,
            MonsterId: 69,
            Name: "MacaqueL48",
            Boss: false,
            GeneratorId: 10,
            SpawnIndex: 0);

        var decompiledFrame = ReadFrame(instance.ToSpawnPacket(
                clientCompatibility: LegacyClientCompatibilityOptions.DecompiledSourceCompatible)
            .ToFrameBytes());
        var newerClientFrame = ReadFrame(instance.ToSpawnPacket(
                clientCompatibility: LegacyClientCompatibilityOptions.NewerClient)
            .ToFrameBytes());

        Assert.True(LegacyMonsterSpawnPacket.TryParse(decompiledFrame, out var decompiledPacket));
        Assert.True(LegacyMonsterSpawnPacket.TryParse(newerClientFrame, out var newerClientPacket));
        Assert.Equal(expectedDecompiledMesh, decompiledPacket.Mesh);
        Assert.Equal(expectedNewerClientMesh, newerClientPacket.Mesh);
    }

    [Fact]
    public void LegacyMonsterInstanceAppliesLegacyMonsterIdOverrides()
    {
        var spawn = Spawn(generatorId: 200, monsterTypeId: 20010, maxPerGenerator: 1);
        var monster = Monster(
            id: 20010,
            name: "PlainFamilyName",
            lookface: "0200",
            level: "0083",
            life: "23346");

        var instance = LegacyMonsterInstance.FromGenerator(spawn, monster, spawnIndex: 0);

        Assert.Equal(2416U, instance.MonsterId);
    }

    [Fact]
    public void LegacyMonsterInstanceRejectsMismatchedSpawnFamily()
    {
        var spawn = Spawn(generatorId: 70, monsterTypeId: 76, maxPerGenerator: 1);
        var monster = Monster(
            id: 77,
            name: "WrongFamily",
            lookface: "0200",
            level: "0083",
            life: "23346");

        Assert.Throws<ArgumentException>(() => LegacyMonsterInstance.FromGenerator(spawn, monster));
    }

    private static MonsterSpawn Spawn(
        int generatorId,
        int maxPerGenerator,
        int maxNpc = 39,
        int mapId = 1000,
        int monsterTypeId = 76,
        int boundX = 122,
        int boundY = 269,
        int boundWidth = 108,
        int boundHeight = 150,
        string sourceMonsterName = "BladeGhostL83",
        string? sourceRelativePath = null,
        Dictionary<string, string>? attributes = null)
    {
        return new MonsterSpawn(
            generatorId,
            mapId,
            monsterTypeId,
            boundX,
            boundY,
            boundWidth,
            boundHeight,
            maxNpc,
            RestSeconds: 1,
            maxPerGenerator,
            TimerBegin: 0,
            TimerEnd: 0,
            BornX: 0,
            BornY: 0,
            SourceMapFolderId: mapId,
            sourceMonsterName,
            sourceRelativePath ?? Path.Combine(mapId.ToString(CultureInfo.InvariantCulture), sourceMonsterName, "1.ini"),
            Attributes: attributes ?? new Dictionary<string, string>());
    }

    private static Monster Monster(
        int id,
        string name,
        string lookface,
        string level,
        string life,
        string? boss = null)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = id.ToString(),
            ["name"] = name,
            ["lookface"] = lookface,
            ["level"] = level,
            ["life"] = life,
        };

        if (boss is not null)
        {
            attributes["Boss"] = boss;
        }

        return new Monster(id, name, attributes, []);
    }

    private static bool IsLegacySeedGuardMonster(Monster monster)
    {
        return (monster.Name.Contains("Guard", StringComparison.OrdinalIgnoreCase) &&
                !monster.Name.Contains("Chaos", StringComparison.OrdinalIgnoreCase)) ||
            string.Equals(monster.Name, "CitySoldier", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "WolfConquer.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate WolfConquer.sln from the test base directory.");
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
