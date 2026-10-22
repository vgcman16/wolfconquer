using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using StringId = WolfConquer.Core.World.LegacyNativeMsgStringPacket.StringID;

namespace WolfConquer.Tests;

// Real native Player/RoleView/Packet/queued-send components with explicitly seeded queues.
// No host/socket Create, physical Player admission, map load, or live rendering proof.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativePlayerSendStringTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task BothOverloadsUseReceiverUidAndPhysicalOwnerOrForeignViewWithoutLoadingGate(
        bool explicitUid, bool screen) => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeStringPacketTestScope();
        var ownerQueue = new Queue<byte[]>();
        var viewQueue = new Queue<byte[]>();
        var owner = NativeStringPacketTestScope.Owner(ownerQueue);
        var viewOwner = NativeStringPacketTestScope.Owner(viewQueue);
        var currentOwnerPlayer = owner.Player;
        var currentViewPlayer = viewOwner.Player;
        currentOwnerPlayer.UID = 111;
        currentViewPlayer.UID = 222;
        var receiver = new LegacyNativePlayerRuntime(owner) { UID = 0x12345678, View = currentViewPlayer.View };
        var heldView = receiver.View;
        string[] arguments = ["fx", "é"];
        scope.Packet.Seek(700);

        Send(receiver, scope.Packet, explicitUid, screen, arguments);

        byte[] expected = Convert.FromHexString("1200F407" + (explicitUid ? "21436587" : "78563412") +
            "0A02020066780200C3A9");
        Assert.Equal(expected, Assert.Single(screen ? viewQueue : ownerQueue));
        Assert.Empty(screen ? ownerQueue : viewQueue);
        Assert.Equal(expected, NativeStringPacketTestScope.Bytes(scope.Packet));
        Assert.Equal(4, scope.Packet.Position);
        Assert.Equal(expected.Length, scope.Packet.Size);
        Assert.Same(owner, receiver.Owner);
        Assert.Same(heldView, receiver.View);
        Assert.Same(viewOwner, receiver.View.Owner);
        Assert.Same(currentOwnerPlayer, owner.Player);
        Assert.Same(currentViewPlayer, viewOwner.Player);
        Assert.NotSame(receiver, owner.Player);
        Assert.NotSame(receiver, viewOwner.Player);
        Assert.Equal(0x12345678u, receiver.UID);
        Assert.False(owner.FullLoading);
        Assert.False(viewOwner.FullLoading);
        Assert.Null(owner.Map);
        Assert.Null(viewOwner.Map); // Genuine RoleView owner route only, not a fake recipient/GetArray.
        Assert.Equal(new[] { "fx", "é" }, arguments);
        scope.Packet.Write(0u); // Actual caller stream is retained/usable; queue owns an independent copy.
        Assert.Equal(expected, Assert.Single(screen ? viewQueue : ownerQueue));
        Assert.Equal(8, scope.Packet.Position);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NullViewIsUnusedByOwnerRouteAndFaultsOnlyAfterScreenArgumentSerialization(bool explicitUid) => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeStringPacketTestScope();
        var queue = new Queue<byte[]>();
        var owner = NativeStringPacketTestScope.Owner(queue);
        var receiver = new LegacyNativePlayerRuntime(owner) { UID = 0x12345678, View = null! };
        string[] arguments = ["fx", "é"];
        byte[] expected = Convert.FromHexString("1200F407" + (explicitUid ? "21436587" : "78563412") +
            "0A02020066780200C3A9");

        Send(receiver, scope.Packet, explicitUid, false, arguments);
        Assert.Equal(expected, Assert.Single(queue));
        queue.Clear();
        scope.Packet.Size = 123;
        scope.Packet.Seek(700);
        Assert.Throws<NullReferenceException>(() => Send(receiver, scope.Packet, explicitUid, true, arguments));

        Assert.Equal(expected, NativeStringPacketTestScope.Bytes(scope.Packet));
        Assert.Equal(expected.Length, scope.Packet.Size);
        Assert.Equal(4, scope.Packet.Position);
        Assert.Empty(queue);
        Assert.Null(receiver.View);
        Assert.Same(owner, receiver.Owner);
    });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task NullParamsReachTheNativeThreeZeroEncodingThroughBothOverloadsAndRoutes(
        bool explicitUid, bool screen) => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeStringPacketTestScope();
        var ownerQueue = new Queue<byte[]>();
        var viewQueue = new Queue<byte[]>();
        var owner = NativeStringPacketTestScope.Owner(ownerQueue);
        var viewOwner = NativeStringPacketTestScope.Owner(viewQueue);
        var receiver = new LegacyNativePlayerRuntime(owner) { UID = 0x12345678, View = viewOwner.Player.View };

        Send(receiver, scope.Packet, explicitUid, screen, null!);

        Assert.Equal(Convert.FromHexString("0C00F407" + (explicitUid ? "21436587" : "78563412") + "0A000000"),
            Assert.Single(screen ? viewQueue : ownerQueue));
        Assert.Empty(screen ? ownerQueue : viewQueue);
        Assert.Equal(12, scope.Packet.Size);
        Assert.Equal(4, scope.Packet.Position);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task LocationEffectInSendStringUsesNewMessagesZeroCoordinatesNotEitherUid(bool explicitUid) => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeStringPacketTestScope();
        var queue = new Queue<byte[]>();
        var owner = NativeStringPacketTestScope.Owner(queue);
        owner.Player.UID = 17;
        if (explicitUid)
            owner.Player.SendString(scope.Packet, StringId.LocationEffect, 0x87654321, false, "p");
        else
            owner.Player.SendString(scope.Packet, StringId.LocationEffect, false, "p");
        Assert.Equal(Convert.FromHexString("0D00F407000000000901010070"), Assert.Single(queue));
        scope.Packet.GetStringPacket(out var parsed);
        Assert.Equal(0u, parsed.UID);
        Assert.Equal(StringId.LocationEffect, parsed.ID);
        Assert.Equal(new[] { "p" }, parsed.Strings);
        Assert.Equal(17u, owner.Player.UID);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task GenuineViewQueuesItsOwnerBeforeMissingMapViewFaultAndRetainsCallerPacket(bool explicitUid) => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeStringPacketTestScope();
        var ownerQueue = new Queue<byte[]>();
        var viewQueue = new Queue<byte[]>();
        var owner = NativeStringPacketTestScope.Owner(ownerQueue);
        var viewOwner = NativeStringPacketTestScope.Owner(viewQueue);
        viewOwner.Map = new LegacyNativeGameMap(72, 72, 1002);
        var receiver = new LegacyNativePlayerRuntime(owner) { UID = 0x12345678, View = viewOwner.Player.View };

        Assert.Throws<NullReferenceException>(() => Send(receiver, scope.Packet, explicitUid, true, ["fx", "é"]));

        byte[] expected = Convert.FromHexString("1200F407" + (explicitUid ? "21436587" : "78563412") +
            "0A02020066780200C3A9");
        Assert.Equal(expected, Assert.Single(viewQueue));
        Assert.Empty(ownerQueue);
        Assert.Equal(expected, NativeStringPacketTestScope.Bytes(scope.Packet));
        Assert.Equal(4, scope.Packet.Position);
        Assert.Null(viewOwner.Map.View); // Neither SendString nor RoleView repairs/publishes a map view.
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task OwnerFakeOrDeadGuardDoesNotSkipPriorStringSerialization(bool fake) => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeStringPacketTestScope();
        var queue = new Queue<byte[]>();
        var owner = NativeStringPacketTestScope.Owner(queue);
        owner.Player.UID = 0x12345678;
        owner.Fake = fake;
        owner.Socket!.Alive = fake;

        Send(owner.Player, scope.Packet, false, false, ["fx", "é"]);

        Assert.Empty(queue);
        Assert.Equal(Convert.FromHexString("1200F407785634120A02020066780200C3A9"),
            NativeStringPacketTestScope.Bytes(scope.Packet));
        Assert.Equal(4, scope.Packet.Position);
        Assert.Equal(fake, owner.Fake);
        Assert.Equal(fake, owner.Socket.Alive);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NullStreamFaultsBeforeEitherRealSendRouteCanQueue(bool screen) => NativePacketTestThread.Run(() =>
    {
        var ownerQueue = new Queue<byte[]>();
        var viewQueue = new Queue<byte[]>();
        var owner = NativeStringPacketTestScope.Owner(ownerQueue);
        var viewOwner = NativeStringPacketTestScope.Owner(viewQueue);
        var receiver = new LegacyNativePlayerRuntime(owner) { View = viewOwner.Player.View };

        Assert.Throws<NullReferenceException>(() => Send(receiver, null!, false, screen, ["fx"]));

        Assert.Empty(ownerQueue);
        Assert.Empty(viewQueue);
        Assert.Same(viewOwner, receiver.View.Owner);
        Assert.Same(owner, receiver.Owner);
    });

    private static void Send(LegacyNativePlayerRuntime receiver, LegacyNativePacket stream,
        bool explicitUid, bool screen, string[] arguments)
    {
        if (explicitUid)
            receiver.SendString(stream, StringId.Effect, 0x87654321, screen, arguments);
        else
            receiver.SendString(stream, StringId.Effect, screen, arguments);
    }
}
