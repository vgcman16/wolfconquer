using WolfConquer.Core.World;
using Cell = WolfConquer.Core.World.LegacyNativeMapFlagType;
using Drop = WolfConquer.Core.World.LegacyNativeMsgDropID;
using Mode = WolfConquer.Core.World.LegacyNativeFloorItemRuntime.ItemType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapPopulationTests
{
    [Theory]
    [InlineData(0, 0u, 0u)] [InlineData(1, 123u, 0u)]
    [InlineData(2, 0u, 123u)] [InlineData(3, 0u, 0u)]
    public Task FullFloorBirthPreservesAmountsUidPacketAliasesAndPhysicalDefaults(int mode, uint gold, uint cps) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map(admitPlayer: false);
        var before = DateTime.Now;
        var role = new LegacyNativeFloorItemRuntime(null, 11, 12, (Mode)mode, 123, 9, 700, 17, true, map, 3600);
        Assert.Same(map, role.GMap); Assert.Null(role.ItemBase); Assert.Null(role.OwnerEffert); Assert.Null(role.DBSkill);
        Assert.Equal(900028u, role.UID); Assert.Equal(900028u, LegacyNativeFloorItemRuntime.UIDS.Count);
        Assert.Equal(17u, role.ItemOwner); Assert.Equal(17u, role.MsgFloor.ItemOwnerUID); Assert.True(role.ToMySelf);
        Assert.Equal(9u, role.DynamicID); Assert.Equal(700u, role.Map); Assert.False(role.AllowDynamic);
        Assert.Equal(gold, role.Gold); Assert.Equal(cps, role.ConquerPoints); Assert.Equal((Mode)mode, role.Typ);
        Assert.InRange(role.Expire, before, DateTime.Now); Assert.True(role.Alive); Assert.False(role.ExpireMySelf);
        role.UID = 91; role.X = 21; role.Y = 22;
        Assert.Equal(91u, role.MsgFloor.m_UID); Assert.Equal((ushort)21, role.MsgFloor.m_X); Assert.Equal((ushort)22, role.MsgFloor.m_Y);
        Assert.Equal(LegacyNativeMapObjectType.Item, role.ObjType);
    });

    [Theory]
    [InlineData(0u, 0u, true, 410301u)]
    [InlineData(1u, 123456u, true, 123456u)]
    [InlineData(1u, 123456u, false, 410301u)]
    public Task FloorBirthReusesGenuineItemPurificationLifetimeAndCosmetics(uint itemUid, uint purification, bool alive, uint expected) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map(admitPlayer: false);
        var item = new LegacyNativeGameItemRuntime { ITEM_ID = 410301, Plus = 7, Color = (LegacyNativeGameItemFlags.Color)9 };
        item.Purification = new()
        {
            ItemUID = itemUid, PurificationItemID = purification,
            PurificationDuration = alive ? 0u : 1u, AddedOn = DateTime.Now.AddHours(-1)
        };
        var role = new LegacyNativeFloorItemRuntime(item, 11, 12, Mode.Item, 0, 0, 700, 0, false, map);
        Assert.Same(item, role.ItemBase); Assert.Equal(expected, role.MsgFloor.m_ID);
        Assert.Equal((byte)7, role.MsgFloor.Plus); Assert.Equal((byte)9, role.MsgFloor.m_Color);
    });

    [Theory]
    [InlineData(41u, 2, 50u)] [InlineData(42u, 12, 50u)] [InlineData(99u, 12, 99u)]
    public Task ExpiredFloorRemovalRewritesSoulIdsClearsOnlyItemBitAndRetainsSpatialRole(uint itemId, int drop, uint expected) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map();
        var role = new LegacyNativeFloorItemRuntime(null, 11, 10, Mode.Effect, 0, 0, 700, 0, false, map)
        { Expire = DateTime.Now.AddMinutes(-5) };
        role.MsgFloor.m_ID = itemId; map.cells[11, 10] = Cell.Valid | Cell.Monster | Cell.Item;
        Assert.True(map.View!.EnterMap(role));
        role.SendAll(scope.Packet, (Drop)drop);
        Assert.Equal(expected, role.MsgFloor.m_ID); Assert.Equal((Drop)drop, role.MsgFloor.DropType);
        Assert.Equal(Cell.Valid | Cell.Monster, map.cells[11, 10]);
        Assert.Same(role, Assert.Single(map.View.GetAllMapRoles(LegacyNativeMapObjectType.Item)));
        var frame = Assert.Single(scope.Queue); Assert.Equal((ushort)2164, Opcode(frame));
        Assert.Equal(expected, BitConverter.ToUInt32(frame, 8)); Assert.Equal((byte)drop, frame[18]);
    });

    [Fact]
    public Task LiveRemovalDoesNotClearCollisionAndExpiredNonremovalDoesNotPublish() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map();
        var role = new LegacyNativeFloorItemRuntime(null, 11, 10, Mode.Effect, 0, 0, 700, 0, false, map, 3600);
        role.MsgFloor.m_ID = 41; map.cells[11, 10] |= Cell.Item;
        role.SendAll(scope.Packet, Drop.RemoveEffect);
        Assert.Equal(41u, role.MsgFloor.m_ID); Assert.Equal(Cell.Valid | Cell.Item, map.cells[11, 10]); Assert.Single(scope.Queue);
        role.Expire = DateTime.Now.AddHours(-2); role.MsgFloor.DropType = Drop.Visible;
        role.SendAll(scope.Packet, Drop.Effect);
        Assert.Single(scope.Queue); Assert.Equal(Drop.Visible, role.MsgFloor.DropType);
    });

    [Theory]
    [InlineData(0, 39, true)] [InlineData(0, 41, false)]
    [InlineData(3600, 3500, true)] [InlineData(3600, 3700, false)]
    public Task FloorLifetimeUsesSpecialSecondsButSelfExpiryAlwaysUsesTwenty(int special, int seconds, bool alive) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map(admitPlayer: false);
        var role = new LegacyNativeFloorItemRuntime(null, 11, 10, Mode.Effect, 0, 0, 700, 0, false, map, special)
        { Expire = DateTime.Now.AddSeconds(-seconds) };
        Assert.Equal(alive, role.Alive); Assert.True(role.ExpireMySelf);
        role.MsgFloor.DropType = Drop.Effect; Assert.True(role.IsTrap());
        role.MsgFloor.DropType = Drop.Visible; Assert.False(role.IsTrap());
    });

    [Fact]
    public Task WholeTrapFileRetainsExcludedEffectsDynamicFlagBlankStopAndNoCollisionReservation() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); var map = scope.Map(1002, admitPlayer: false);
        scope.WriteDb("Traps.txt", "1002,11,12,1063,1\n1002,13,14,9000,1\n\n1002,15,16,9001,0\n");
        LegacyNativeNpcServer.LoadServerTraps(1002);
        var role = Assert.IsType<LegacyNativeFloorItemRuntime>(Assert.Single(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Item)));
        Assert.Equal(9000u, role.MsgFloor.m_ID); Assert.Equal(900028u, role.UID); Assert.True(role.AllowDynamic);
        Assert.Equal((byte)2, role.MsgFloor.m_Color); Assert.Equal(Drop.Effect, role.MsgFloor.DropType);
        Assert.Equal(Cell.Valid, map.cells[13, 14]);
        scope.WriteDb("Traps.txt", "1002,21,22,9002\nbad,1,1,9003\n");
        Assert.Throws<FormatException>(() => LegacyNativeNpcServer.LoadServerTraps(1002));
        Assert.Equal(2, map.View.GetAllMapRoles(LegacyNativeMapObjectType.Item).Count());
    });

    [Fact]
    public Task SectorTrapProducerAndRemovalUseRealRandomSpatialFlagsAndFloorSendGraph() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map();
        LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(31); var random = new Random(31);
        ushort x = (ushort)(10 + random.Next(1, 18)), y = (ushort)(10 + random.Next(1, 18));
        map.GenerateSectorTraps(10, 10, 9000);
        var trap = Assert.IsType<LegacyNativeFloorItemRuntime>(Assert.Single(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Item)));
        Assert.Equal(x, trap.X); Assert.Equal(y, trap.Y); Assert.Equal(Cell.Valid | Cell.Item, map.cells[x, y]);
        Assert.Equal(9000u, trap.MsgFloor.m_ID); Assert.Single(scope.Queue);
        map.RemoveTrap(0, 0, trap); // Native arguments are ignored; actual object coordinates own removal.
        Assert.Empty(map.View.GetAllMapRoles(LegacyNativeMapObjectType.Item)); Assert.Equal(Cell.Valid, map.cells[x, y]);
        Assert.Equal(2, scope.Queue.Count); Assert.Equal(Drop.RemoveEffect, trap.MsgFloor.DropType);
        Assert.Equal(random.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });
}
