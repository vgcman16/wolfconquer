using WolfConquer.Core.World;
using Cell = WolfConquer.Core.World.LegacyNativeMapFlagType;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapPopulationTests
{
    [Theory]
    [InlineData(false, "English")] [InlineData(true, "Arabic")] [InlineData(false, null)]
    public Task WholeSobSpawnKeepsRequiredZeroFieldsSignedCastsAndBothLanguages(bool alternate, string? name) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var sob = new LegacyNativeSobNpcRuntime
        {
            UID = 99, X = 21, Y = 22, HitPoints = -1, MaxHitPoints = -2,
            Mesh = (LegacyNativeSobNpcRuntime.StaticMesh)31220, Type = (LegacyNativeNpcType)10,
            Sort = 7, Name = alternate ? "ignored" : name, Name2 = alternate ? name : "ignored"
        };
        var result = ((ILegacyNativeMapObj)sob).GetArray(scope.Packet, true, alternate);
        Assert.Same(scope.Packet, result);
        Assert.Equal(SobWire(99, -1, -2, 21, 22, 31220, 10, 7, name), Bytes(result));
        Assert.False(sob.IsStatue); Assert.False(sob.IsTrap()); Assert.False(sob.Alive);
        sob.HitPoints = 1; Assert.True(sob.Alive); sob.HitPoints = 0; Assert.False(sob.Alive);
        Assert.Equal(18, sob.BitVector.bits.Length); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(17, true)] [InlineData(18, false)] [InlineData(19, false)]
    public Task SobVisibilityUsesStrictEighteenDistanceAndIgnoresAllowDynamicOverride(int distance, bool visible) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map();
        var sob = new LegacyNativeSobNpcRuntime { Map = 700, X = (ushort)(10 + distance), Y = 10, DynamicID = 0, AllowDynamic = true };
        Assert.Equal(visible, sob.CanSee(scope.Player));
        scope.Player.DynamicID = 9; Assert.False(sob.CanSee(scope.Player));
        Assert.Throws<NullReferenceException>(() => sob.CanSee(null!));
        sob.Send(scope.Packet); Assert.Empty(scope.Queue); // Genuine native method is empty.
        sob.SendView(scope.Packet, map); Assert.Empty(scope.Queue); // Dynamic mismatch also filters SendView.
    });

    [Fact]
    public Task SobFlagsUseActualStatusVectorAndWholeMapOnlyGamePollBroadcast() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map();
        var nearby = new Queue<byte[]>(); var otherMap = new Queue<byte[]>();
        var peer = scope.Peer(map, 99, 70, 70, nearby);
        var foreign = NativeSpellTestFixture.CreateOwner(otherMap); foreign.Player.UID = 100; foreign.Player.Map = 701;
        LegacyNativePoolClients.GamePoll![17] = scope.Client;
        LegacyNativePoolClients.GamePoll[99] = peer; LegacyNativePoolClients.GamePoll[100] = foreign;
        var sob = new LegacyNativeSobNpcRuntime { UID = 77, Map = 700, X = 10, Y = 10 };
        Assert.True(sob.AddFlag(Flag.Poisoned, 30, true, 7));
        Assert.True(sob.ContainFlag(Flag.Poisoned)); Assert.False(sob.AddFlag(Flag.Poisoned, 99, false));
        Assert.Single(scope.Queue); Assert.Single(nearby); Assert.Empty(otherMap);
        Assert.Equal((ushort)2176, Opcode(Assert.Single(scope.Queue)));
        var record = Assert.Single(sob.BitVector.ArrayFlags).Value;
        Assert.Equal(30, record.Secounds); Assert.Equal(7, record.InvokerSecouds); Assert.True(record.RemoveOnDead);
        Assert.True(sob.RemoveFlag(Flag.Poisoned)); Assert.False(sob.RemoveFlag(Flag.Poisoned));
        Assert.False(sob.ContainFlag(Flag.Poisoned)); Assert.Equal(2, nearby.Count); Assert.Equal(2, scope.Queue.Count);
    });

    [Fact]
    public Task WholeSobFileRetainsMapFilterTrainingHpAndRepeatedSecondNameRead() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); var map = scope.Map(1039, admitPlayer: false);
        scope.WriteDb("SobNpcs.txt", "70,Training,10,1137,1039,20,20,-7,-9,3,First,Second\n71,Ignored,10,1137,700,bad,bad\n");
        LegacyNativeNpcServer.LoadSobNpcs(1039);
        var sob = Assert.IsType<LegacyNativeSobNpcRuntime>(Assert.Single(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.SobNpc)));
        Assert.Equal(10000, sob.HitPoints); Assert.Equal(10000, sob.MaxHitPoints);
        Assert.Equal("Training", sob.Name); Assert.Equal("Second", sob.Name2); Assert.Equal((ushort)3, sob.Sort);
        Assert.Equal(Cell.Npc, map.cells[20, 20]); Assert.False(LegacyNativePoolMaps.ServerMaps!.Base.ContainsKey(700));
    });

    [Theory]
    [InlineData(0, null)] [InlineData(1, "Timed")]
    public Task WholeNpcTimeUsesNameVisibilityByteAndGateCollision(uint nameFlag, string? expected) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); var map = scope.Map(1039, admitPlayer: false);
        scope.WriteDb("NpcTime.txt", $"516076,Timed,10,1551,1039,20,20,1,2,3,{nameFlag}\n");
        LegacyNativeNpcServer.NpcTime();
        var sob = Assert.IsType<LegacyNativeSobNpcRuntime>(Assert.Single(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.SobNpc)));
        Assert.Equal(expected, sob.Name); Assert.Equal(10000, sob.HitPoints); Assert.Equal(10000, sob.MaxHitPoints);
        Assert.Equal(Cell.None, map.cells[20, 20]);
    });

    [Fact]
    public Task MissingRequiredSobFileFaultsDirectlyWithoutCreatingFakeActors() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); var map = scope.Map(admitPlayer: false);
        File.Delete(Path.Combine(scope.Db, "SobNpcs.txt"));
        Assert.Throws<FileNotFoundException>(() => LegacyNativeNpcServer.LoadSobNpcs(700));
        Assert.Empty(map.View!.GetAllMapRoles(LegacyNativeMapObjectType.SobNpc));
    });
}
