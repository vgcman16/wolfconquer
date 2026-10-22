using System.Reflection;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeRoleViewTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SendViewUsesSamePacketForOrderedPlayerRecipientsAndOptionalOwnerFirst(bool me) => NativePacketTestThread.Run(() =>
    {
        var client = Client();
        var mapView = PublishModelView(client);
        var queue = SeedModelSocket(client);
        byte[] bytes = [4, 0, 0xAB, 0x12];
        using var packet = new LegacyNativePacket(bytes);
        var order = new List<uint>();
        var first = Model(2);
        var second = Model(3);
        second.Alive = false; // SendView has no implicit alive-only filter.
        foreach (var obj in new[] { first, second })
        {
            obj.OnSend = received =>
            {
                Assert.Same(packet, received);
                Assert.Equal(me ? 1 : 0, queue.Count);
                if (me)
                    Assert.Equal(bytes, Assert.Single(queue)); // Owner queued before first callback.
                order.Add(obj.UID);
            };
        }
        Assert.True(mapView.EnterMap(first));
        Assert.True(mapView.EnterMap(second));
        Assert.True(mapView.EnterMap(first)); // Native duplicate references are not deduplicated.
        Assert.True(mapView.EnterMap(Model(client.Player.UID)));
        Assert.True(mapView.EnterMap(Model(4, 37)));
        Assert.True(mapView.EnterMap(Model(5, type: LegacyNativeMapObjectType.Monster)));

        client.Player.View.SendView(packet, me);

        Assert.Equal(new uint[] { 2, 3, 2 }, order);
        Assert.Equal(me ? 1 : 0, queue.Count);
        Assert.Equal(bytes.Length, packet.Size);
        Assert.Equal(0, packet.Position);
        Assert.Null(client.Socket!.Connection);
        Assert.Null(client.Socket.Crypto); // Plain component queue only; no OS socket/send pump.
    });

    [Fact]
    public Task SendViewOwnerPrecedesMissingMapViewFaultWithoutPublishingOrRepairingIt() => NativePacketTestThread.Run(() =>
    {
        var client = Client();
        client.Map = new LegacyNativeGameMap(72, 72, 1002);
        var queue = SeedModelSocket(client);
        byte[] bytes = [4, 0, 0xCD, 0x34];
        using var packet = new LegacyNativePacket(bytes);

        Assert.Throws<NullReferenceException>(() => client.Player.View.SendView(packet, me: true));

        Assert.Equal(bytes, Assert.Single(queue));
        Assert.Null(client.Map.View);
        Assert.Null(client.Socket!.Connection);
    });

    [Fact]
    public Task SendViewDoesNotCatchModelRecipientFaultOrSendToLaterRecipients() => NativePacketTestThread.Run(() =>
    {
        var client = Client();
        var mapView = PublishModelView(client);
        var queue = SeedModelSocket(client);
        using var packet = new LegacyNativePacket(new byte[] { 4, 0, 1, 2 });
        var fault = new InvalidOperationException("MODEL recipient failure.");
        var attempts = new List<uint>();
        var first = Model(2);
        first.OnSend = received =>
        {
            Assert.Same(packet, received);
            attempts.Add(2);
            throw fault;
        };
        var second = Model(3);
        second.OnSend = _ => attempts.Add(3);
        Assert.True(mapView.EnterMap(first));
        Assert.True(mapView.EnterMap(second));

        Assert.Same(fault, Assert.Throws<InvalidOperationException>(() =>
            client.Player.View.SendView(packet, me: false)));

        Assert.Equal(new uint[] { 2 }, attempts);
        Assert.Empty(queue);
        Assert.Null(client.Socket!.Connection);
    });

    private static Queue<byte[]> SeedModelSocket(LegacyNativeGameClientRuntime client)
    {
        // MODEL prerequisite only: real SecuritySocket.Create/pump/host binding are absent.
        var socket = new LegacyNativeSecuritySocket(null!, null!);
        var queue = new Queue<byte[]>();
        typeof(LegacyNativeSecuritySocket).GetField("SendQueue", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(socket, queue);
        socket.Alive = true;
        client.Socket = socket;
        return queue;
    }
}
