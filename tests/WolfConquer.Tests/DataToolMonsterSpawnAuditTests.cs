using System.Text.Json;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class DataToolMonsterSpawnAuditTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "wolf-monster-spawn-audit-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void AuditDetectsDuplicateGuardTileAndMissingSourceFamily()
    {
        var fixture = WriteFixture();

        var report = global::MonsterSpawnAuditLoader.LoadAndAudit(new global::MonsterSpawnAuditOptions(
            fixture.ActiveRoot,
            fixture.DbServerRoot,
            MapId: 1002,
            Json: true));

        Assert.False(report.IsClean);
        Assert.Equal(2, report.ActiveSpawnCount);
        Assert.Equal(2, report.OriginalSpawnCount);

        var guardStack = Assert.Single(report.GuardStacks);
        Assert.Equal(1002, guardStack.MapId);
        Assert.Equal(50, guardStack.X);
        Assert.Equal(50, guardStack.Y);
        Assert.Equal(2, guardStack.ActiveCount);
        Assert.Equal(1, guardStack.OriginalCount);
        Assert.Equal(1, guardStack.ExcessCount);
        Assert.False(guardStack.SourceBacked);
        Assert.Equal("Guard1", Assert.Single(guardStack.ActiveFamilies).FamilyName);

        var coordinateDuplicate = Assert.Single(report.CoordinateDuplicates);
        Assert.Equal((guardStack.MapId, guardStack.X, guardStack.Y),
            (coordinateDuplicate.MapId, coordinateDuplicate.X, coordinateDuplicate.Y));

        var missingFamily = Assert.Single(report.MissingFamilies);
        Assert.Equal(1002, missingFamily.MapId);
        Assert.Equal(100, missingFamily.FamilyId);
        Assert.Equal("Pheasant", missingFamily.FamilyName);
        Assert.Equal(1, missingFamily.OriginalSpawnCount);
    }

    [Fact]
    public void AuditMapFilterReportsCleanMatchingMap()
    {
        var fixture = WriteFixture();

        var report = global::MonsterSpawnAuditLoader.LoadAndAudit(new global::MonsterSpawnAuditOptions(
            fixture.ActiveRoot,
            fixture.DbServerRoot,
            MapId: 1011,
            Json: false));

        Assert.True(report.IsClean);
        Assert.Equal(1011, report.MapId);
        Assert.Equal(2, report.ActiveSpawnCount);
        Assert.Equal(2, report.OriginalSpawnCount);
        Assert.Equal(2, report.ActiveFamilyCount);
        Assert.Equal(2, report.OriginalFamilyCount);
        Assert.Empty(report.GuardStacks);
        Assert.Empty(report.MissingFamilies);
        var sourceBackedDuplicate = Assert.Single(report.CoordinateDuplicates);
        Assert.True(sourceBackedDuplicate.SourceBacked);
        Assert.Equal(0, sourceBackedDuplicate.ExcessCount);
    }

    [Fact]
    public void AuditDetectsMissingSpawnWithinExistingFamily()
    {
        var family = Monster(100, "Pheasant");
        var retained = Spawn(20, 1011, 100, 70, 70, "Pheasant", "Monsters1011.txt", 1);
        var missing = Spawn(21, 1011, 100, 71, 70, "Pheasant", "Monsters1011.txt", 2);

        var report = global::MonsterSpawnAuditor.Audit(
            [family],
            [retained],
            [family],
            [retained, missing],
            "fixture://active",
            "fixture://original");

        Assert.False(report.IsClean);
        Assert.Empty(report.MissingFamilies);
        var finding = Assert.Single(report.MissingSpawns);
        Assert.Equal(missing.GeneratorId, finding.GeneratorId);
        Assert.Equal(1, finding.Count);
        Assert.Empty(report.ExcessSpawns);
    }

    [Fact]
    public void AuditDetectsMovedSpawnWithMatchingTotalsAndFamilies()
    {
        var family = Monster(100, "Pheasant");
        var original = Spawn(20, 1011, 100, 70, 70, "Pheasant", "Monsters1011.txt", 1);
        var moved = Spawn(20, 1011, 100, 71, 70, "Pheasant", "Monsters1011.txt", 1);

        var report = global::MonsterSpawnAuditor.Audit(
            [family],
            [moved],
            [family],
            [original],
            "fixture://active",
            "fixture://original");

        Assert.False(report.IsClean);
        Assert.Equal(report.ActiveSpawnCount, report.OriginalSpawnCount);
        Assert.Equal(report.ActiveFamilyCount, report.OriginalFamilyCount);
        Assert.Single(report.MissingSpawns);
        Assert.Single(report.ExcessSpawns);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private MonsterSpawnAuditFixture WriteFixture()
    {
        var activeRoot = Path.Combine(_rootPath, "active");
        var dbServerRoot = Path.Combine(_rootPath, "original-dbserver");
        var monstersRoot = Path.Combine(dbServerRoot, "Monsters");
        var spawnsRoot = Path.Combine(dbServerRoot, "MonsterSpawns");
        Directory.CreateDirectory(activeRoot);
        Directory.CreateDirectory(monstersRoot);
        Directory.CreateDirectory(spawnsRoot);

        var monsters = new[]
        {
            Monster(100, "Pheasant"),
            Monster(900, "Guard1"),
        };
        var activeSpawns = new[]
        {
            Spawn(10, 1002, 900, 50, 50, "Guard1", "Monsters1002.txt", 1),
            Spawn(12, 1002, 900, 50, 50, "Guard1", "Monsters1002.txt", 2),
            Spawn(20, 1011, 900, 70, 70, "Guard1", "Monsters1011.txt", 1),
            Spawn(21, 1011, 100, 70, 70, "Pheasant", "Monsters1011.txt", 2),
        };
        File.WriteAllText(
            Path.Combine(activeRoot, "monsters.json"),
            JsonSerializer.Serialize(monsters, JsonOptions));
        File.WriteAllText(
            Path.Combine(activeRoot, "monsterSpawns.json"),
            JsonSerializer.Serialize(activeSpawns, JsonOptions));

        File.WriteAllText(
            Path.Combine(monstersRoot, "Guard1.ini"),
            "[cq_monstertype]\nid=900\nname=Guard1\n");
        File.WriteAllText(
            Path.Combine(monstersRoot, "Pheasant.ini"),
            "[cq_monstertype]\nid=100\nname=Pheasant\n");
        File.WriteAllText(
            Path.Combine(spawnsRoot, "Monsters1002.txt"),
            "10,900,Guard1,50,50,1002\n11,100,Pheasant,60,60,1002\n");
        File.WriteAllText(
            Path.Combine(spawnsRoot, "Monsters1011.txt"),
            "20,900,Guard1,70,70,1011\n21,100,Pheasant,70,70,1011\n");

        return new MonsterSpawnAuditFixture(activeRoot, dbServerRoot);
    }

    private static Monster Monster(int id, string name, uint? settings = null)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (settings.HasValue)
        {
            attributes["settings"] = settings.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return new Monster(
            id,
            name,
            attributes,
            []);
    }

    private static MonsterSpawn Spawn(
        int generatorId,
        int mapId,
        int monsterTypeId,
        int x,
        int y,
        string monsterName,
        string sourceRelativePath,
        int sourceLine)
    {
        return new MonsterSpawn(
            generatorId,
            mapId,
            monsterTypeId,
            x,
            y,
            BoundWidth: 1,
            BoundHeight: 1,
            MaxNpc: 1,
            RestSeconds: 0,
            MaxPerGenerator: 1,
            TimerBegin: 0,
            TimerEnd: 0,
            BornX: 0,
            BornY: 0,
            SourceMapFolderId: null,
            SourceMonsterName: monsterName,
            sourceRelativePath,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["legacy_source"] = "LoadMapMonsters",
                ["source_line"] = sourceLine.ToString(System.Globalization.CultureInfo.InvariantCulture),
            });
    }

    private sealed record MonsterSpawnAuditFixture(string ActiveRoot, string DbServerRoot);
}
