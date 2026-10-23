using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapSpawnTests
{
    private static string SpawnIni(uint map, uint family = 111, int count = 1, int maxnpc = 1) =>
        $"[cq_generator]\r\nnpctype={family}\r\nbound_x=12\r\nbound_y=13\r\nbound_cx=1\r\nbound_cy=1\r\nmapid={map}\r\nmax_per_gen={count}\r\nmaxnpc={maxnpc}\r\nrest_secs=71\r\n";

    [Theory]
    [InlineData(700, 3u)] [InlineData(1011, 0u)] [InlineData(3071, 0u)] [InlineData(1794, 0u)]
    public Task WholeDirectoryLoaderPreservesNestedPathNativeSpawnCountOverrideAndRealCollection(uint mapId, uint attempts) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); var map = scope.Map((int)mapId, admitPlayer: false);
        LegacyNativePoolMonsters.MonsterFamilies[111] = Family();
        string file = scope.WriteDb(Path.Combine("MonsterSpawns", mapId.ToString(), "owned", "group", "one.ini"), SpawnIni(mapId, count: 1, maxnpc: 2));
        LegacyNativeServerMapPopulation.LoadMonsters(mapId);
        Assert.NotNull(map.MonstersColletion); Assert.Same(map, map.MonstersColletion.DMap);
        // Native concatenates an extra separator onto the already terminated DB prefix;
        // Directory.GetFiles preserves that physical path spelling rather than normalizing it.
        string nativePath = scope.Db + Path.DirectorySeparatorChar + "\\MonsterSpawns\\" + mapId + "\\owned\\group\\one.ini";
        Assert.Equal(Path.GetFullPath(file), Path.GetFullPath(nativePath));
        Assert.Equal(nativePath, map.MonstersColletion.LocationSpawn);
        Assert.Equal(400000u + attempts, LegacyNativeMobCollection.GenerateUid.Count);
        var roles = map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster).ToArray();
        if (attempts == 0) Assert.Empty(roles);
        else
        {
            var role = Assert.IsType<LegacyNativeMonsterRole>(Assert.Single(roles));
            Assert.Equal(400001u, role.UID); Assert.Equal(nativePath, role.LocationSpawn);
            Assert.Equal((ushort)12, role.X); Assert.Equal((ushort)13, role.Y); Assert.Equal(mapId, role.Map);
        }
    });

    [Fact]
    public Task DirectoryLoaderRequiresActualWarmMapAndSkipsNativeSpecialFamilies() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles();
        uint special = LegacyNativeMonsterRole.SpecialMonsters[0];
        LegacyNativePoolMonsters.MonsterFamilies[special] = Family(special);
        scope.WriteDb(Path.Combine("MonsterSpawns", "700", "owned", "group", "one.ini"), SpawnIni(700, special));
        LegacyNativeServerMapPopulation.LoadMonsters(700);
        Assert.Empty(LegacyNativePoolMaps.ServerMaps!.Base); Assert.Equal(400000u, LegacyNativeMobCollection.GenerateUid.Count);
        var map = scope.Map(admitPlayer: false); LegacyNativeServerMapPopulation.LoadMonsters(700);
        Assert.NotNull(map.MonstersColletion); Assert.Empty(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster));
        Assert.Equal(400000u, LegacyNativeMobCollection.GenerateUid.Count);
    });

    [Fact]
    public Task WholeTextSpawnerUsesMatchingMapCastsAndStopsAtBadLineAfterPublishedPrefix() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); var map = scope.Map(admitPlayer: false);
        LegacyNativePoolMonsters.MonsterFamilies[111] = Family();
        scope.WriteDb(Path.Combine("MonsterSpawns", "myMonsters.txt"),
            "unused 701 bad bad bad bad bad bad bad bad\nunused 700 12 13 1 1 999 71 257 111\nbroken\nunused 700 14 15 1 1 0 71 1 111\n");
        LegacyNativeServerMapPopulation.LoadMyMonsters(700);
        var role = SingleRole<LegacyNativeMonsterRole>(map, LegacyNativeMapObjectType.Monster);
        Assert.Equal((ushort)12, role.X); Assert.Equal((ushort)13, role.Y);
        Assert.Equal(400003u, LegacyNativeMobCollection.GenerateUid.Count); // 257 -> byte1 -> native three attempts.
        Assert.Equal("", role.LocationSpawn); Assert.Same(map, map.MonstersColletion.DMap);
    });

    [Fact]
    public Task TextSpawnerConstructsRealCollectionBeforeMissingFileAndDoesNotAllocateMap() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles();
        LegacyNativeServerMapPopulation.LoadMyMonsters(700);
        Assert.Empty(LegacyNativePoolMaps.ServerMaps!.Base);
        var map = scope.Map(admitPlayer: false); LegacyNativeServerMapPopulation.LoadMyMonsters(700);
        Assert.NotNull(map.MonstersColletion); Assert.Same(map, map.MonstersColletion.DMap);
        Assert.Empty(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster));
    });
}
