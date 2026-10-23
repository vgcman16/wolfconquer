using WolfConquer.Core.World;
using Cell = WolfConquer.Core.World.LegacyNativeMapFlagType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapPopulationTests
{
    private static LegacyNativeMonsterFamily PopulationFamily(string name = "Pheasant") => new()
    {
        ID = 111, Name = name, MaxAttack = 30, MinAttack = 10, MaxHealth = 500,
        Defense = 7, Mesh = 100, Level = 15, AttackRange = 2, DropSpecials = [],
        SpawnX = 20, SpawnY = 20, MaxSpawnX = 21, MaxSpawnY = 21, SpawnCount = 9
    };

    [Theory]
    [InlineData(1002)] [InlineData(1020)] [InlineData(1011)] [InlineData(1768)]
    [InlineData(1000)] [InlineData(1001)] [InlineData(1779)] [InlineData(1780)]
    [InlineData(3935)] [InlineData(3998)] [InlineData(1043)] [InlineData(1044)]
    [InlineData(1045)] [InlineData(1046)] [InlineData(1047)] [InlineData(1048)]
    [InlineData(8822)] [InlineData(1015)] [InlineData(1016)] [InlineData(3845)]
    [InlineData(10250)] [InlineData(10166)] [InlineData(10089)] [InlineData(11127)]
    [InlineData(10137)] [InlineData(3071)] [InlineData(10341)] [InlineData(10342)]
    [InlineData(10323)] [InlineData(10324)] [InlineData(10322)] [InlineData(10343)]
    public Task WholeEnterMapRunsEveryNativeMonsterFileCaseThroughActualFamilyCopyAndAdd(int id) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); scope.Terrain(id);
        var family = PopulationFamily(); LegacyNativePoolMonsters.MonsterFamilies[111] = family;
        scope.WriteDb(Path.Combine("MonsterSpawns", $"Monsters{id}.txt"), $"unused,999,Pheasant,12,13,{id}\n");

        LegacyNativeGameMap.EnterMap(id);

        Assert.True(LegacyNativePoolMaps.ServerMaps!.TryGetValue((uint)id, out var map));
        var role = Assert.IsType<LegacyNativeMonsterRole>(Assert.Single(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster)));
        Assert.Equal(400001u, role.UID); Assert.Equal(111u, role.Family.ID); Assert.NotSame(family, role.Family);
        Assert.Equal((ushort)12, role.X); Assert.Equal((ushort)13, role.Y); Assert.Equal((uint)id, role.Map);
        Assert.Same(map, map.MonstersColletion.DMap); Assert.Same(map, role.GMap);
        Assert.Equal(Cell.Valid | Cell.Monster, map.cells[12, 13]);
        Assert.Equal(id == 10166 ? 400003u : 400001u, LegacyNativeMobCollection.GenerateUid.Count);
        Assert.Equal((ushort)20, family.SpawnX); Assert.Equal((byte)9, family.SpawnCount); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData("Pheasant")] [InlineData("WhiteTiger")]
    public Task MonsterFileMatchesNameRatherThanParsedIdAndCreatesRealMissingCollection(string name) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); var map = scope.Map(admitPlayer: false);
        var family = PopulationFamily(name); LegacyNativePoolMonsters.MonsterFamilies[111] = family;
        scope.WriteDb(Path.Combine("MonsterSpawns", "owned.txt"), $"unused,999,{name},12,13,700\n");
        Assert.Null(map.MonstersColletion);
        LegacyNativeServerMapPopulation.LoadMapMonsters("owned.txt");
        Assert.NotNull(map.MonstersColletion); Assert.Same(map, map.MonstersColletion.DMap);
        var role = Assert.IsType<LegacyNativeMonsterRole>(Assert.Single(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster)));
        Assert.Equal(111u, role.Family.ID); Assert.Equal(name, role.Name);
        Assert.Equal((byte)0, role.Family.SpawnCount); // The second genuine Copy does not copy SpawnCount.
    });

    [Fact]
    public Task MonsterFileSkipsTitanBeforeCoordinateParseButStopsAfterFirstCaughtBadId() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); var map = scope.Map(admitPlayer: false);
        LegacyNativePoolMonsters.MonsterFamilies[111] = PopulationFamily();
        scope.WriteDb(Path.Combine("MonsterSpawns", "owned.txt"),
            "unused,1,Titan,bad,bad,bad\nunused,999,Pheasant,12,13,700\nunused,bad,Pheasant,14,15,700\nunused,999,Pheasant,16,17,700\n");
        LegacyNativeServerMapPopulation.LoadMapMonsters("owned.txt");
        var role = Assert.Single(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster));
        Assert.Equal((ushort)12, role.X); Assert.Equal(400001u, LegacyNativeMobCollection.GenerateUid.Count);
        Assert.Equal(Cell.Valid, map.cells[16, 17]);
    });

    [Fact]
    public Task MonsterFileMissingMapAndBlankLineStopRatherThanInventingOrSkippingToLaterRows() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); var map = scope.Map(admitPlayer: false);
        LegacyNativePoolMonsters.MonsterFamilies[111] = PopulationFamily();
        scope.WriteDb(Path.Combine("MonsterSpawns", "owned.txt"), "unused,999,Pheasant,12,13,701\nunused,999,Pheasant,14,15,700\n");
        LegacyNativeServerMapPopulation.LoadMapMonsters("owned.txt");
        Assert.Empty(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster));
        Assert.False(LegacyNativePoolMaps.ServerMaps!.Base.ContainsKey(701));
        scope.WriteDb(Path.Combine("MonsterSpawns", "owned.txt"), "\nunused,999,Pheasant,14,15,700\n");
        LegacyNativeServerMapPopulation.LoadMapMonsters("owned.txt");
        Assert.Empty(map.View.GetAllMapRoles(LegacyNativeMapObjectType.Monster)); Assert.Null(map.MonstersColletion);
        LegacyNativeServerMapPopulation.LoadMapMonsters("owned-missing.txt"); Assert.Null(map.MonstersColletion);
    });
}
