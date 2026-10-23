using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNobilityPoleEventTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task NativeInfoBirthAndExactPhysicalFieldTypesRemainIntact(int kind) => NativePacketTestThread.Run(() =>
    {
        Assert.Equal(typeof(ushort), Field(kind, "MapID").FieldType);
        Assert.Equal(typeof(LegacyNativeProcesType), Field(kind, "Proces").FieldType);
        Assert.Equal(War(kind).GetNestedType("Info"), Field(kind, "RoundOwner").FieldType);
        Assert.Equal(typeof(LegacyNativeDbSafeDictionary<,>), Field(kind, "NobilityPoleList").FieldType.GetGenericTypeDefinition());
        var info = Activator.CreateInstance(War(kind).GetNestedType("Info")!)!;
        Assert.Equal(0u, RowField<uint>(info, "UID"));
        Assert.Equal(0u, RowField<uint>(info, "Score"));
        Assert.Null(RowField<string?>(info, "Name"));
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task LoadUsesRealTypedMapObjectIdentityAndNativeDuplicateAddFault(int kind) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var map = scope.Map();
        var pole = Scope.Pole(kind);
        map.View!.EnterMap(pole);
        Call(kind, "Load");
        Assert.Same(pole, Furniture(kind)[LegacyNativeSobNpcRuntime.StaticMesh.Pole]);
        Assert.Same(pole, map.View.GetMapObject<LegacyNativeSobNpcRuntime>(
            LegacyNativeMapObjectType.SobNpc, (uint)(22340 + kind)));
        Assert.Throws<ArgumentException>(() => Call(kind, "Load"));
        Assert.Single(Furniture(kind));
        Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task MissingLoadedObjectRetainsNullAndResetReplacesScoresBeforeFault(int kind) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Map();
        var old = Scores(kind);
        old.Add(7u, Row(kind, 7, 55));
        Call(kind, "Load");
        Assert.Null(Furniture(kind)[LegacyNativeSobNpcRuntime.StaticMesh.Pole]);
        Assert.Throws<NullReferenceException>(() => Call(kind, "Reset", scope.Packet));
        Assert.NotSame(old, Scores(kind));
        Assert.Single(old);
        Assert.Empty(Scores(kind));
        Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task ResetTargetsGlobalNearbyPlayersAcrossMapsAndDynamicsAtDistanceNineteen(int kind) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var pole = scope.Active(kind, 1, 777);
        pole.DynamicID = 13;
        var acrossMap = new Queue<byte[]>();
        var tooFar = new Queue<byte[]>();
        scope.Peer(91, 1002, 83, 777, acrossMap);
        scope.Peer(92, 11140, 84, 13, tooFar);
        var old = Scores(kind);
        old.Add(7u, Row(kind, 7, 90));
        Call(kind, "Reset", scope.Packet);
        Assert.Equal(777, pole.HitPoints);
        Assert.NotSame(old, Scores(kind));
        Assert.Single(old);
        Assert.Empty(Scores(kind));
        Assert.Equal(new ushort[] { 2176, 2166 }, scope.Queue.Select(Opcode));
        Assert.Equal(new ushort[] { 2176, 2166 }, acrossMap.Select(Opcode));
        Assert.Empty(tooFar);
        Assert.Equal(new LegacyMsgUpdatePacket((uint)(22340 + kind),
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mesh, 1137),
             new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, 777)]).ToFrameBytes(), acrossMap.First());
        var spawn = Proto<LegacyNativeSobNpcRuntime.SobNpcSpawnProto>(acrossMap.Last());
        Assert.Equal(777u, spawn.Hitpoints);
        Assert.Equal((ushort)1137, spawn.Mesh);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task ResetWithNonPoleMeshStillSendsCombinedUpdateButNoSpawn(int kind) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var pole = scope.Active(kind, 0, 600);
        pole.Mesh = LegacyNativeSobNpcRuntime.StaticMesh.LeftGate;
        Call(kind, "Reset", scope.Packet);
        Assert.Equal(600, pole.HitPoints);
        Assert.Equal((ushort)2176, Opcode(Assert.Single(scope.Queue)));
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task ResetCommitsScoreReplacementAndEveryFurnitureHpBeforePoolFault(int kind) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var pole = scope.Active(kind, 1, 700);
        var gate = Scope.Pole(kind, 2, 900);
        Furniture(kind).Add(LegacyNativeSobNpcRuntime.StaticMesh.LeftGate, gate);
        var old = Scores(kind);
        old.Add(7u, Row(kind, 7, 9));
        LegacyNativePoolClients.GamePoll = null;
        Assert.Throws<NullReferenceException>(() => Call(kind, "Reset", scope.Packet));
        Assert.NotSame(old, Scores(kind));
        Assert.Single(old);
        Assert.Empty(Scores(kind));
        Assert.Equal(700, pole.HitPoints);
        Assert.Equal(900, gate.HitPoints);
        Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task ScorePacketsUseRealMapMembershipNotGlobalPlayerMapOrDynamicFields(int kind) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var map = scope.Map();
        var memberQueue = new Queue<byte[]>();
        var nonmemberQueue = new Queue<byte[]>();
        var member = scope.Peer(91, 1002, 64, 444, memberQueue);
        map.Enquer(member);
        scope.Peer(92, 11140, 64, 0, nonmemberQueue);
        scope.Packet.TeamArenaMatchScoreCreate(1, 2, "A", 3, 4, 5, "B", 6);
        Call(kind, "SendMapPacket", scope.Packet);
        Assert.Equal((ushort)2190, Opcode(Assert.Single(scope.Queue)));
        Assert.Equal((ushort)2190, Opcode(Assert.Single(memberQueue)));
        Assert.Empty(nonmemberQueue);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task LoadAndBroadcastReadMutableNativeMapIdRatherThanHardcodeDefault(int kind) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Set(kind, "MapID", (ushort)12345);
        var map = scope.Map(12345);
        var pole = Scope.Pole(kind);
        pole.Map = 12345;
        map.View!.EnterMap(pole);
        Call(kind, "Load");
        Assert.Same(pole, Furniture(kind)[LegacyNativeSobNpcRuntime.StaticMesh.Pole]);
        scope.Packet.TeamArenaMatchScoreCreate(1, 2, "A", 3, 4, 5, "B", 6);
        Call(kind, "SendMapPacket", scope.Packet);
        Assert.Equal((ushort)2190, Opcode(Assert.Single(scope.Queue)));
        Assert.False(LegacyNativePoolMaps.ServerMaps!.TryGetValue(11140, out _));
    });
}
