using WolfConquer.Core.World;
using Cell = WolfConquer.Core.World.LegacyNativeMapFlagType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapSpawnTests
{
    [Fact]
    public Task NativeItemAdmissionAndQueriesKeepDuplicateAliasesExpiredRolesAndNoCollisionReservation() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map(admitPlayer: false);
        var item = new LegacyNativeFloorItemRuntime(null, 12, 13, LegacyNativeFloorItemRuntime.ItemType.Effect, 0, 0, 700, 0, false, map)
        { Expire = DateTime.Now.AddHours(-1) };
        item.MsgFloor.m_ID = 940;
        Assert.True(map.EnqueueItem(item)); Assert.True(map.EnqueueItem(item));
        Assert.Equal(2, map.ContainItem(940)); Assert.Equal(0, map.ContainItem(930));
        Assert.Equal(Cell.Valid, map.cells[12, 13]); Assert.Empty(scope.Queue);
        item.X = 80; Assert.False(map.EnqueueItem(item)); Assert.Equal(2, map.ContainItem(940));
    });

    [Theory]
    [InlineData(1, true)] [InlineData(2, false)] [InlineData(0, true)] [InlineData(-1, true)]
    public Task NativeMonsterPresenceUsesAliveFamilyAndRequestedMultiplicity(int count, bool expected) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map(admitPlayer: false);
        var role = new LegacyNativeMonsterRole(Family(), 400001, "owned", map) { Map = 700, X = 12, Y = 13, DynamicID = 9 };
        map.View!.EnterMap(role);
        Assert.Equal(expected, map.ContainMobID(111, count: count));
        Assert.Equal(expected, map.ContainMobID(111, 9, count));
        Assert.Equal(count <= 0, map.ContainMobID(111, 10, count));
        role.HitPoints = 0; Assert.Equal(count <= 0, map.ContainMobID(111, count: count));
    });

    [Fact]
    public Task GuildWarItemUsesActualTwoDrawPlacementNameFloorPacketAndItemFlag() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map();
        int selectedSeed = -1; ushort x = 0, y = 0; Random control = null!;
        for (int seed = 31; seed < 256; seed++)
        {
            control = new Random(seed); x = (ushort)control.Next(80); y = (ushort)control.Next(80);
            if (x > 0 && y > 0) { selectedSeed = seed; break; }
        }
        Assert.InRange(selectedSeed, 31, 255);
        LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(selectedSeed);
        map.GuildWarItem(940);
        var item = SingleRole<LegacyNativeFloorItemRuntime>(map, LegacyNativeMapObjectType.Item);
        Assert.Equal(x, item.X); Assert.Equal(y, item.Y); Assert.Equal(940u, item.MsgFloor.m_ID);
        Assert.Equal("STR_TRAP_ID_940@@", item.MsgFloor.Name); Assert.Equal((byte)1, item.MsgFloor.m_Color);
        Assert.Equal(LegacyNativeMsgDropID.Effect, item.MsgFloor.DropType);
        Assert.Equal(Cell.Valid | Cell.Item, map.cells[x, y]);
        Assert.Equal(control.Next(), LegacyNativePoolRandom.GetRandom.Next());
        var retainedUid = LegacyNativeFloorItemRuntime.UIDS.Count;
        LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(selectedSeed); map.GuildWarItem(941);
        Assert.Equal(retainedUid, LegacyNativeFloorItemRuntime.UIDS.Count); Assert.Equal(1, map.ContainItem(940));
    });

    [Theory]
    [InlineData(18, true)] [InlineData(19, false)] [InlineData(20, false)]
    public Task NpcScreenSearchPreservesFoundReferenceWhenNativeDistanceRejects(int distance, bool found) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map(admitPlayer: false);
        var npc = LegacyNativeNpcRuntime.Create(); npc.UID = 55; npc.Map = 700; npc.X = 10; npc.Y = 10;
        map.View!.EnterMap(npc);
        Assert.Equal(found, map.SearchNpcInScreen(55, (ushort)(10 + distance), 10, out var result));
        Assert.Same(npc, result); Assert.False(map.SearchNpcInScreen(99, 10, 10, out result)); Assert.Null(result);
    });

    [Theory]
    [InlineData(1u, 19340)] [InlineData(7u, 19350)] [InlineData(8u, 19360)]
    [InlineData(9u, 19370)] [InlineData(10u, 19380)]
    public Task MagnoliaProducerKeepsNativeAlternationQualityMeshesAndActualNpcIdentity(uint quality, int mesh) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map(admitPlayer: false, width: 140, height: 140);
        map.AddMagnolia(scope.Packet, quality);
        var first = map.Magnolia; Assert.Equal(999901u, first.UID); Assert.Equal((ushort)99, first.X); Assert.Equal((ushort)112, first.Y);
        Assert.Equal((ushort)mesh, first.Mesh); Assert.Same(first, SingleRole<LegacyNativeNpcRuntime>(map, LegacyNativeMapObjectType.Npc));
        map.AddMagnolia(scope.Packet, quality);
        var second = map.Magnolia; Assert.NotSame(first, second); Assert.Equal(999900u, second.UID);
        Assert.Equal((ushort)106, second.X); Assert.Equal((ushort)99, second.Y);
        Assert.Same(second, SingleRole<LegacyNativeNpcRuntime>(map, LegacyNativeMapObjectType.Npc));
        map.AddMagnolia(scope.Packet, quality); Assert.Equal(999901u, map.Magnolia.UID); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(1000, 55u, 592, 656, 445, 509, true)]
    [InlineData(1015, 8551u, 519, 583, 310, 374, false)]
    [InlineData(1015, 55u, 364, 544, 484, 664, false)]
    public Task WholeSoldierRespawnKeepsPerMapRangesAndOriginalDeadlinePolicy(int id, uint uid,
        int minX, int maxX, int minY, int maxY, bool past) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map(id, admitPlayer: false, width: 700, height: 700);
        var npc = LegacyNativeNpcRuntime.Create(); npc.UID = uid; npc.Map = (uint)id;
        npc.Respawn = DateTime.Now.AddHours(past ? -1 : 1); map.soldierRemains[uid] = npc;
        LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(37); var random = new Random(37);
        ushort x = (ushort)random.Next(minX, maxX), y = (ushort)random.Next(minY, maxY);
        map.CheckUpSoldierReamins();
        Assert.Empty(map.soldierRemains); Assert.Same(npc, SingleRole<LegacyNativeNpcRuntime>(map, LegacyNativeMapObjectType.Npc));
        Assert.Equal(x, npc.X); Assert.Equal(y, npc.Y); Assert.Equal(random.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });

    [Fact]
    public Task SoldierRespawnOnMap1000RetainsFutureEntriesAndOtherMapDoesNothing() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map(1000, admitPlayer: false, width: 700, height: 700);
        var npc = LegacyNativeNpcRuntime.Create(); npc.UID = 55; npc.Map = 1000; npc.Respawn = DateTime.Now.AddHours(1);
        map.soldierRemains[55] = npc; map.CheckUpSoldierReamins(); Assert.Single(map.soldierRemains);
        Assert.Empty(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Npc));
        var other = scope.Map(700, admitPlayer: false); other.soldierRemains[55] = npc; other.CheckUpSoldierReamins();
        Assert.Single(other.soldierRemains); Assert.Empty(other.View!.GetAllMapRoles(LegacyNativeMapObjectType.Npc));
    });
}
