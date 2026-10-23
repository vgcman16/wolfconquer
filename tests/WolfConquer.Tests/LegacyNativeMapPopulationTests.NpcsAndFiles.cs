using WolfConquer.Core.World;
using Cell = WolfConquer.Core.World.LegacyNativeMapFlagType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapPopulationTests
{
    [Theory]
    [InlineData(16851u, true)] [InlineData(168052u, true)] [InlineData(51u, false)]
    public Task NativeNpcFilePreservesForcedDynamicIdsAndMapFilter(uint uid, bool dynamic) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); var map = scope.Map(admitPlayer: false);
        scope.Terrain(701, "other.dmap");
        scope.WriteDb("npcs.txt", $"{uid},2,1000,700,10,10,0\n52,2,1000,701,10,10,1\n53,2,42580,700,12,12\n");
        LegacyNativeNpcServer.LoadNpcs(700);
        var npc = Assert.IsType<LegacyNativeNpcRuntime>(Assert.Single(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Npc)));
        Assert.Equal(uid, npc.UID); Assert.Equal(dynamic, npc.AllowDynamic);
        Assert.False(LegacyNativePoolMaps.ServerMaps!.Base.ContainsKey(701));
        Assert.Equal(Cell.Npc, map.cells[10, 10]); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task NpcLoaderStopsAtBlankLineAndKeepsFirstPublishedPrefixOnLaterParseFault() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); var map = scope.Map(admitPlayer: false);
        scope.WriteDb("npcs.txt", "51,2,1000,700,10,10\n\n52,2,1000,700,20,20\n");
        LegacyNativeNpcServer.LoadNpcs(700);
        Assert.Equal(51u, Assert.Single(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Npc)).UID);
        scope.WriteDb("npcs.txt", "53,2,1000,700,30,30\nbad,2,1000,700,20,20\n");
        Assert.Throws<FormatException>(() => LegacyNativeNpcServer.LoadNpcs(700));
        Assert.Equal(new uint[] { 51, 53 }, map.View.GetAllMapRoles(LegacyNativeMapObjectType.Npc).Select(n => n.UID).Order().ToArray());
    });

    [Fact]
    public Task FurnitureCatalogKeepsFirstUidAndRealDuplicateNpcReferenceRemovalQuirk() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); var map = scope.Map(admitPlayer: false);
        scope.WriteDb("furnitures.txt", "55,2,1000,700,10,10,Chair,800000,12\n55,2,2000,700,20,20,Desk,900000,23\n");
        LegacyNativeNpcServer.LoadNpcs(700);
        var furniture = Assert.Single(LegacyNativeNpcServer.FurnitureInformations).Value;
        Assert.Equal("Chair", furniture.Name); Assert.Equal(800000u, furniture.ItemID); Assert.Equal(12, furniture.MoneyCost);
        Assert.Same(furniture, LegacyNativeNpcServer.GetNpc(800000));
        Assert.Same(furniture, LegacyNativeNpcServer.GetNpcFromMesh(1000));
        Assert.Null(LegacyNativeNpcServer.GetNpc(900000)); Assert.Null(LegacyNativeNpcServer.GetNpcFromMesh(2000));
        var npc = Assert.IsType<LegacyNativeNpcRuntime>(Assert.Single(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Npc)));
        Assert.Equal((ushort)1000, npc.Mesh); Assert.Equal((ushort)10, npc.X);
        Assert.Equal(Cell.Valid, map.cells[20, 20]); // RemoveNpc used the new object coordinates before failing identity removal.
    });

    [Theory]
    [InlineData(false, 10, true)] [InlineData(false, 19, true)] [InlineData(false, 20, false)]
    [InlineData(true, 19, true)]
    public Task WholeNpcSendUsesRealMapPlayerRecipientsAndNativeDistanceGate(bool allowDynamic, int distance, bool visible) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map();
        map.View!.MoveTo((ILegacyNativeMapObj)scope.Player, 10 + distance, 10);
        scope.Player.X = (ushort)(10 + distance); scope.Player.Y = 10;
        var npc = LegacyNativeNpcRuntime.Create(); npc.UID = 55; npc.Map = 700; npc.X = 10; npc.Y = 10;
        npc.AllowDynamic = allowDynamic;
        npc.Send(scope.Packet);
        Assert.Equal(visible ? 1 : 0, scope.Queue.Count);
        Assert.Equal(visible ? 55u : 0u, npc.UnKnow);
        if (visible) Assert.Equal((ushort)2210, Opcode(Assert.Single(scope.Queue)));
    });

    [Fact]
    public Task NpcDynamicVisibilityAndCachedGMapDoNotRebindToReplacedCatalogRow() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map(); scope.Player.DynamicID = 9;
        var npc = LegacyNativeNpcRuntime.Create(); npc.UID = 55; npc.Map = 700; npc.X = 10; npc.Y = 10;
        Assert.Same(map, npc.GMap); npc.Send(scope.Packet); Assert.Empty(scope.Queue);
        npc.AllowDynamic = true; npc.Send(scope.Packet); Assert.Single(scope.Queue);
        var replacement = NewMap(); LegacyNativePoolMaps.ServerMaps!.Base[700] = replacement;
        npc.Map = 701; Assert.Same(map, npc.GMap); Assert.NotSame(replacement, npc.GMap);
        npc.Send(scope.Packet); Assert.Single(scope.Queue);
    });

    [Fact]
    public Task NpcGMapNullResultIsNotCachedAsAPlaceholderAndNullCatalogStillFaults() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); var npc = LegacyNativeNpcRuntime.Create(); npc.Map = 700;
        Assert.Null(npc.GMap); var map = scope.Map(admitPlayer: false); Assert.Same(map, npc.GMap);
        var other = LegacyNativeNpcRuntime.Create(); other.Map = 701;
        LegacyNativePoolMaps.ServerMaps = null;
        Assert.Throws<NullReferenceException>(() => other.GMap);
    });

    [Fact]
    public Task NativeMapNameAndPortalLoadersPreserveFallbackFilteringAndPartialAppendFault() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); var map = scope.Map(admitPlayer: false);
        var other = scope.Map(701, admitPlayer: false);
        scope.WriteDb("GameMapEx.ini", "[701]\r\nName=OtherCity\r\n");
        LegacyNativeServerMapPopulation.LoadMapName(700); LegacyNativeServerMapPopulation.LoadMapName(701);
        Assert.Equal("OwnedWorld", map.Name); Assert.Equal("OtherCity", other.Name);
        scope.WriteDb("portals.ini", "[700]\nCount=2\na=700 3 4\nb=701 5 6\na=700 7 8\nb=701 9 10\n[701]\nCount=1\na=701 11 12\nb=700 13 14\n");
        LegacyNativeServerMapPopulation.LoadPortals(700);
        Assert.Equal(2, map.Portals.Count); Assert.Empty(other.Portals);
        LegacyNativeServerMapPopulation.LoadPortals(700); Assert.Equal(4, map.Portals.Count); // No de-duplication.
        scope.WriteDb("portals.ini", "[700]\nCount=2\na=700 15 16\nb=701 17 18\na=broken\nb=701 9 10\n");
        Assert.Throws<FormatException>(() => LegacyNativeServerMapPopulation.LoadPortals(700));
        Assert.Equal(5, map.Portals.Count); Assert.Equal((ushort)15, map.Portals[^1].X);
    });
}
