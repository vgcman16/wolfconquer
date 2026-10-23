using WolfConquer.Core.World;
using Cell = WolfConquer.Core.World.LegacyNativeMapFlagType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapPopulationTests
{
    [Fact]
    public Task ColdLazyLookupRunsWholeNpcSobPortalTrapPopulationAgainstRealMapOwners() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); scope.Terrain();
        scope.WriteDb("GameMapEx.ini", "[700]\r\nName=OwnedCity\r\n");
        scope.WriteDb("npcs.txt", "50,2,1000,700,10,10,1\r\n");
        scope.WriteDb("SobNpcs.txt", "70,Pole,10,1137,700,20,20,17,91,3,First,Second\r\n");
        scope.WriteDb("portals.ini", "[700]\nCount=1\nportal0=700 3 4\ndest0=1002 410 354\n");
        scope.WriteDb("Traps.txt", "700,25,25,9000,1\r\n");

        Assert.True(LegacyNativePoolMaps.ServerMaps!.ContainsKey(700));
        var map = LegacyNativePoolMaps.ServerMaps[700];

        Assert.Equal("OwnedCity", map.Name);
        var npc = Assert.IsType<LegacyNativeNpcRuntime>(Assert.Single(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Npc)));
        Assert.Equal(50u, npc.UID); Assert.True(npc.AllowDynamic); Assert.Same(map, npc.GMap);
        Assert.Equal(Cell.Npc, map.cells[10, 10]);
        var sob = Assert.IsType<LegacyNativeSobNpcRuntime>(Assert.Single(map.View.GetAllMapRoles(LegacyNativeMapObjectType.SobNpc)));
        Assert.Equal(70u, sob.UID); Assert.Equal(17, sob.HitPoints); Assert.Equal(91, sob.MaxHitPoints);
        Assert.Equal("Second", sob.Name2); Assert.Equal(Cell.Npc, map.cells[20, 20]);
        var portal = Assert.Single(map.Portals); Assert.Equal((ushort)3, portal.X); Assert.Equal((ushort)1002, portal.Destiantion_MapID);
        var trap = Assert.IsType<LegacyNativeFloorItemRuntime>(Assert.Single(map.View.GetAllMapRoles(LegacyNativeMapObjectType.Item)));
        Assert.Equal(900028u, trap.UID); Assert.Equal(9000u, trap.MsgFloor.m_ID);
        Assert.True(trap.IsTrap()); Assert.True(trap.AllowDynamic); Assert.Same(map, trap.GMap);
        Assert.Equal(Cell.Valid, map.cells[25, 25]); // Native trap-file loading does not reserve Item collision.
        Assert.Null(map.MonstersColletion.DMap); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(3830, 1780)] [InlineData(3833, 1780)] [InlineData(3834, 1780)]
    [InlineData(3826, 3825)] [InlineData(3829, 3825)] [InlineData(7357, 6000)]
    [InlineData(5040, 5000)] [InlineData(5342, 700)] [InlineData(5339, 3851)]
    [InlineData(2351, 1351)] [InlineData(2352, 1351)] [InlineData(2353, 1351)]
    [InlineData(6011, 1021)] [InlineData(9988, 8709)] [InlineData(8437, 1801)]
    [InlineData(1818, 1765)] [InlineData(1052, 1082)] [InlineData(6072, 1004)]
    [InlineData(1782, 1004)] [InlineData(1783, 1004)] [InlineData(1784, 601)]
    [InlineData(1794, 1028)] [InlineData(5599, 1016)] [InlineData(1792, 1014)]
    [InlineData(1791, 1765)] [InlineData(6784, 1505)] [InlineData(44455, 10088)]
    [InlineData(44457, 10088)] [InlineData(44460, 10090)] [InlineData(44463, 10090)]
    [InlineData(6546, 3872)] [InlineData(6521, 2071)] [InlineData(8009, 2071)]
    [InlineData(6891, 2071)] [InlineData(6525, 1507)] [InlineData(8521, 1507)]
    [InlineData(6729, 3825)]
    public Task WholeEnterMapKeepsEveryNativeRemappingAndLogicalIdentity(int logical, int terrain) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); scope.Terrain(terrain);
        LegacyNativeGameMap.EnterMap(logical);
        Assert.True(LegacyNativePoolMaps.ServerMaps!.TryGetValue((uint)logical, out var map));
        Assert.Equal((uint)logical, map.ID); Assert.Equal((uint)terrain, map.BaseID);
        Assert.False(LegacyNativePoolMaps.ServerMaps.Base.ContainsKey((uint)terrain));
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task MissingRequiredSobFileRetainsPublishedTerrainPrefixButSkipsLaterPopulation() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); scope.Terrain();
        File.Delete(Path.Combine(scope.Db, "SobNpcs.txt")); // Exact file created by this Scope only.
        scope.WriteDb("Traps.txt", "700,25,25,9000\n");
        scope.WriteDb("portals.ini", "[700]\nCount=1\nportal0=700 3 4\ndest0=1002 410 354\n");
        LegacyNativeGameMap.EnterMap(700);
        Assert.True(LegacyNativePoolMaps.ServerMaps!.TryGetValue(700, out var map));
        Assert.Empty(map.Portals); Assert.Empty(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Item));
        Assert.True(LegacyNativeGameMap.CheckMap(700));
    });

    [Fact]
    public Task ExistingMapAndZeroIdDoNotRunPopulationOrInventCatalogRows() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); var map = scope.Map(admitPlayer: false);
        scope.WriteDb("SobNpcs.txt", "malformed");
        LegacyNativeGameMap.EnterMap(700); LegacyNativeGameMap.EnterMap(0);
        Assert.Same(map, LegacyNativePoolMaps.ServerMaps![700]); Assert.Single(LegacyNativePoolMaps.ServerMaps.Base);
        Assert.Empty(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.SobNpc));
        Assert.False(LegacyNativePoolMaps.ServerMaps.ContainsKey(0));
    });

    [Fact]
    public Task RealLazyDictionaryKeepsWarmRawKeysAndNativeMissingKeyConversionFaults() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles();
        var text = new LegacyNativeMapDictionary<string, object>(); var row = new object();
        text.Add("not-numeric", row);
        Assert.True(text.ContainsKey("not-numeric")); Assert.Same(row, text["not-numeric"]);
        Assert.Throws<FormatException>(() => text.ContainsKey("missing-text"));
        Assert.Throws<OverflowException>(() => text.ContainsKey("-1"));
        Assert.Throws<ArgumentNullException>(() => text.ContainsKey(null!));
        Assert.False(text.ContainsKey("0")); Assert.Null(text["700"]);
        Assert.Throws<OverflowException>(() => LegacyNativePoolMaps.ServerMaps!.ContainsKey(uint.MaxValue));
        Assert.Empty(LegacyNativePoolMaps.ServerMaps!.Base);
    });

    [Fact]
    public Task FailedTerrainAndMissingCatalogContentReturnNoPlaceholderThroughWholeLookup() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles();
        LegacyNativeGameMap.MapContents[700] = "owned-missing.dmap";
        Assert.False(LegacyNativePoolMaps.ServerMaps!.ContainsKey(700));
        Assert.Null(LegacyNativePoolMaps.ServerMaps[701]);
        Assert.False(LegacyNativeGameMap.CheckMap(700)); Assert.False(LegacyNativeGameMap.CheckMap(701));
        Assert.Empty(LegacyNativePoolMaps.ServerMaps.Base);
    });
}
