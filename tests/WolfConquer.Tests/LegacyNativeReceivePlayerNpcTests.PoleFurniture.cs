using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeReceivePlayerNpcTests
{
    [Fact]
    public Task PoleFurnitureLookupUsesRealMapViewAndDuplicateAddFault() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var map = scope.Map();
        var pole = Scope.Pole();
        Assert.True(map.View.EnterMap(pole));
        Assert.Same(pole, map.View.GetMapObject<LegacyNativeSobNpcRuntime>(LegacyNativeMapObjectType.SobNpc, 830));
        var war = new LegacyNativePoleDomination();
        war.CreateFurnitures();
        Assert.Same(pole, war.Furnitures[LegacyNativeSobNpcRuntime.StaticMesh.Pole]);
        Assert.Throws<ArgumentException>(() => war.CreateFurnitures());
        Assert.Single(war.Furnitures);
    });

    [Fact]
    public Task PoleFurnitureMissingObjectRetainsNativeNullValueRatherThanPretendSpawn() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Map();
        var war = new LegacyNativePoleDomination();
        war.CreateFurnitures();
        Assert.Null(war.Furnitures[LegacyNativeSobNpcRuntime.StaticMesh.Pole]);
        Assert.Throws<NullReferenceException>(() => war.ResetFurnitures(scope.Packet));
    });

    [Fact]
    public Task PoleFurnitureNullExistingMapFaultDoesNotTriggerAnyLoader() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.True(LegacyNativePoolMaps.ServerMaps!.Add(11083, null!));
        var war = new LegacyNativePoleDomination();
        Assert.Throws<NullReferenceException>(() => war.CreateFurnitures());
        Assert.Empty(war.Furnitures);
    });

    [Theory]
    [InlineData(11083u, 999u, true)]
    [InlineData(6001u, 0u, true)]
    [InlineData(1002u, 0u, false)]
    public Task PoleMapBroadcastUsesTwoSourceMapsWithoutDynamicFilter(uint map, uint dynamicId, bool receives) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        LegacyNativePoolClients.GamePoll!.Clear();
        var queue = new Queue<byte[]>();
        scope.Peer(71, map, dynamicId, queue);
        scope.Packet.TeamArenaMatchScoreCreate(1, 2, "A", 3, 4, 5, "B", 6);
        var war = new LegacyNativePoleDomination();
        war.SendMapPacket(scope.Packet);
        Assert.Equal(receives ? 1 : 0, queue.Count);
        if (receives) Assert.Equal((ushort)2190, Opcode(queue.Peek()));
    });

    [Theory]
    [InlineData(18, true)]
    [InlineData(19, true)]
    [InlineData(20, false)]
    public Task PoleResetIncludesSourceDistanceNineteenAndIgnoresDynamicId(ushort distance, bool receives) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.Player.X = (ushort)(64 + distance);
        scope.Client.Player.DynamicID = 987;
        var pole = Scope.Pole(1, 700);
        pole.DynamicID = 123;
        var war = Scope.War(pole);
        war.ResetFurnitures(scope.Packet);
        Assert.Equal(700, pole.HitPoints);
        Assert.Equal(receives ? new ushort[] { 2176, 2166 } : Array.Empty<ushort>(), scope.Queue.Select(Opcode));
        if (receives)
        {
            // Required UID830, field2 submessage with omitted enum0 and value700.
            Assert.Equal(new byte[] { 12, 0, 128, 8, 8, 190, 6, 18, 3, 16, 188, 5 }, scope.Queue.Peek());
        }
    });

    [Theory]
    [InlineData(6001u)]
    [InlineData(1002u)]
    public Task PoleResetDoesNotSendOutsideMainEventMap(uint map) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.Player.Map = map;
        var pole = Scope.Pole(0, 750);
        Scope.War(pole).ResetFurnitures(scope.Packet);
        Assert.Equal(750, pole.HitPoints);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task PoleResetSendsHpForNonPoleFurnitureButNoSpawn() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var gate = Scope.Pole(0, 650);
        gate.Mesh = LegacyNativeSobNpcRuntime.StaticMesh.LeftGate;
        var war = new LegacyNativePoleDomination();
        war.Furnitures.Add(LegacyNativeSobNpcRuntime.StaticMesh.LeftGate, gate);
        war.ResetFurnitures(scope.Packet);
        Assert.Equal(650, gate.HitPoints);
        Assert.Equal((ushort)2176, Opcode(Assert.Single(scope.Queue)));
    });

    [Fact]
    public Task PoleResetCommitsAllHpBeforeAnyPoolEnumerationFault() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var first = Scope.Pole(0, 500);
        var second = Scope.Pole(1, 900);
        var war = Scope.War(first);
        war.Furnitures.Add(LegacyNativeSobNpcRuntime.StaticMesh.LeftGate, second);
        LegacyNativePoolClients.GamePoll = null;
        Assert.Throws<NullReferenceException>(() => war.ResetFurnitures(scope.Packet));
        Assert.Equal(500, first.HitPoints);
        Assert.Equal(900, second.HitPoints);
    });
}
