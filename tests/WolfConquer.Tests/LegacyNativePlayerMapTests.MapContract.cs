using System.Reflection;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Player = WolfConquer.Core.World.LegacyNativePlayerRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerMapTests
{
    [Fact]
    public Task InterfaceAdoptionUsesWholeConcretePlayerOwnersAndExactMapMembers() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); ILegacyNativeMapObj obj = scope.Player;
        Assert.Equal(LegacyNativeMapObjectType.Player, obj.ObjType); Assert.False(obj.IsTrap()); Assert.False(obj.Alive);
        obj.IndexInScreen = 77; Assert.Equal(77u, scope.Player.IndexInScreen);
        obj.AllowDynamic = true; Assert.True(scope.Player.AllowDynamic);
        scope.Player.Map = 1002; scope.Player.DynamicID = 5; scope.Player.X = 10; scope.Player.Y = 11;
        Assert.Equal(1002u, obj.Map); Assert.Equal(5u, obj.DynamicID); Assert.Equal((ushort)10, obj.X); Assert.Equal((ushort)11, obj.Y);
        Assert.Same(scope.Packet, obj.GetArray(scope.Packet, true, false));
        var message = Read(scope.Packet); Assert.Equal(obj.UID, message.UID); Assert.True(message.WindowSpawn);
        byte[] frame = NativeSpellTestFixture.Bytes(scope.Packet);
        obj.Send(scope.Packet); Assert.Equal(frame, Assert.Single(scope.Native.Queue));
        scope.Native.Queue.Clear(); scope.Client.Socket!.Alive = false;
        obj.Send(scope.Packet); Assert.Empty(scope.Native.Queue);
    });

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(int.MaxValue, true)]
    public Task HpReadAndAliveUseSinglePhysicalBackingWithNoWritableFacade(int hitpoints, bool alive) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Set(scope.Player, "_minhitpoints", hitpoints);
        Assert.Equal(hitpoints, scope.Player.HitPoints); Assert.Equal(alive, scope.Player.Alive);
        Assert.False(typeof(Player).GetProperty(nameof(Player.HitPoints))!.CanWrite);
        Assert.Empty(scope.Native.Queue);
    });

    [Fact]
    public Task NativeVisibilityAcceptsActualPlayersWithoutFakeMapObjectsOrModernSpawnProjection() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var other = new Player(scope.Client) { UID = 99, Map = 1002, X = 10, Y = 10 };
        scope.Player.Map = 1002; scope.Player.X = 11; scope.Player.Y = 11;
        Assert.True(scope.Player.View.CanSee(other));
        other.DynamicID = 1; Assert.False(scope.Player.View.CanSee(other));
        other.AllowDynamic = true; Assert.True(scope.Player.View.CanSee(other));
        other.X = 100; Assert.False(scope.Player.View.CanSee(other));
        other.X = 10; other.Map = 10428; scope.Player.Map = 10428;
        Assert.False(scope.Player.View.CanSee(other));
    });
}
