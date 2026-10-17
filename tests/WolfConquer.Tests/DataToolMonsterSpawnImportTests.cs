using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class DataToolMonsterSpawnImportTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "wolf-monster-spawn-import-" + Guid.NewGuid());

    [Fact]
    public void ReadMonsterSpawnsDefaultMatchesLegacyStartupMapMonsterFilesOnly()
    {
        var (spawnRoot, monsterTypeIdsByName) = WriteFixture();

        var spawns = global::MonsterSpawnImport.ReadMonsterSpawns(spawnRoot, monsterTypeIdsByName);

        Assert.Equal(2, spawns.Count);
        Assert.All(spawns, spawn => Assert.Equal("LoadMapMonsters", spawn.Attributes["legacy_source"]));
        Assert.Contains(spawns, spawn =>
            spawn.GeneratorId == 446664 &&
            spawn.MapId == 1002 &&
            spawn.MonsterTypeId == 1 &&
            spawn.SourceMonsterName == "Pheasant" &&
            spawn.SourceRelativePath == "Monsters1002.txt" &&
            spawn.Attributes["legacy_source"] == "LoadMapMonsters");
        Assert.DoesNotContain(spawns, spawn => spawn.SourceRelativePath == Path.Combine("1000", "BladeGhost", "1.ini"));
        Assert.DoesNotContain(spawns, spawn => spawn.SourceRelativePath == "Monsters10137.txt.txt");
        Assert.DoesNotContain(spawns, spawn => spawn.SourceRelativePath == "myMonsters.txt");
        Assert.DoesNotContain(spawns, spawn => spawn.SourceMonsterName == "TitanL77");
    }

    [Fact]
    public void ReadMonsterSpawnsFullInventoryIncludesDeferredGeneratorAndMyMonstersSources()
    {
        var (spawnRoot, monsterTypeIdsByName) = WriteFixture();

        var spawns = global::MonsterSpawnImport.ReadMonsterSpawns(
            spawnRoot,
            monsterTypeIdsByName,
            global::MonsterSpawnImportMode.FullInventory);

        Assert.Equal(5, spawns.Count);
        Assert.Contains(spawns, spawn =>
            spawn.GeneratorId == 21 &&
            spawn.MapId == 1000 &&
            spawn.SourceMapFolderId == 1000 &&
            spawn.SourceMonsterName == "BladeGhost" &&
            spawn.SourceRelativePath == Path.Combine("1000", "BladeGhost", "1.ini"));
        Assert.Contains(spawns, spawn =>
            spawn.GeneratorId == 7591 &&
            spawn.MapId == 1927 &&
            spawn.MonsterTypeId == 20010 &&
            spawn.SourceRelativePath == "myMonsters.txt" &&
            spawn.Attributes["legacy_source"] == "LoadMyMonsters");
        Assert.DoesNotContain(spawns, spawn => spawn.SourceMonsterName == "TitanL77");
    }

    [Fact]
    public void ReadMonsterSpawnsUsesLegacyExactMapMonsterNameMatching()
    {
        var spawnRoot = Path.Combine(_rootPath, "MonsterSpawns");
        Directory.CreateDirectory(spawnRoot);
        File.WriteAllText(
            Path.Combine(spawnRoot, "Monsters1002.txt"),
            """
            446664,231,Pheasant,465,477,1002
            """);
        var monsterTypeIdsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["pheasant"] = 1
        };

        var spawns = global::MonsterSpawnImport.ReadMonsterSpawns(spawnRoot, monsterTypeIdsByName);

        Assert.Empty(spawns);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private (string SpawnRoot, IReadOnlyDictionary<string, int> MonsterTypeIdsByName) WriteFixture()
    {
        var spawnRoot = Path.Combine(_rootPath, "MonsterSpawns");
        var iniRoot = Path.Combine(spawnRoot, "1000", "BladeGhost");
        Directory.CreateDirectory(iniRoot);
        File.WriteAllText(
            Path.Combine(iniRoot, "1.ini"),
            """
            [cq_generator]
            id=21
            mapid=11127
            bound_x=300
            bound_y=300
            bound_cx=8
            bound_cy=50
            maxnpc=15
            rest_secs=1
            max_per_gen=30
            npctype=17
            timer_begin=0
            timer_end=0
            born_x=0
            born_y=0
            """);
        File.WriteAllText(
            Path.Combine(iniRoot, "unknown.ini"),
            """
            [cq_generator]
            id=22
            mapid=1000
            bound_x=300
            bound_y=300
            bound_cx=8
            bound_cy=50
            maxnpc=15
            rest_secs=1
            max_per_gen=30
            npctype=9999
            timer_begin=0
            timer_end=0
            born_x=0
            born_y=0
            """);
        File.WriteAllText(
            Path.Combine(spawnRoot, "Monsters1002.txt"),
            """
            446664,231,Pheasant,465,477,1002
            455543,231,Pheasant,456,482,1002
            500016,153,TitanL77,400,587,1020
            """);
        File.WriteAllText(
            Path.Combine(spawnRoot, "Monsters10137.txt.txt"),
            """
            555555,231,Pheasant,500,500,10137
            """);
        File.WriteAllText(
            Path.Combine(spawnRoot, "myMonsters.txt"),
            """
            7591 1927 324 530 50 50 30 1 25 20010
            7592 1927 324 530 50 50 30 1 25 9999
            """);

        var monsterTypeIdsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Pheasant"] = 1,
            ["BladeGhost"] = 17,
            ["LegacyMyMonster"] = 20010,
            ["TitanL77"] = 3134
        };
        return (spawnRoot, monsterTypeIdsByName);
    }
}
