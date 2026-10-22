using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Msg = WolfConquer.Core.World.LegacyNativeMsgMessage;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMsgMessageBehaviorTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task SystemMessageUsesAllFourNativeBranchesAndTheActualForeignViewOwner(bool screen, bool self) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(recycle: true);
        var clientQueue = new Queue<byte[]>(); var viewQueue = new Queue<byte[]>();
        var client = Client(clientQueue); var viewOwner = Client(viewQueue);
        client.Player.Name = "receiver"; viewOwner.Player.Name = "foreign";
        var heldPlayer = client.Player;
        client.Player.View = viewOwner.Player.View;
        var heldView = client.Player.View;
        client.SendSysMesage("status", Msg.ChatMode.Whisper, Msg.MsgColor.cyan, screen, self);
        var queued = Assert.Single(screen ? viewQueue : clientQueue);
        Assert.Empty(screen ? clientQueue : viewQueue);
        var parsed = Decode(queued);
        Assert.Equal(65535u, parsed.Color); Assert.Equal(2001u, parsed.ChatType);
        Assert.Equal(new[] { "SYSTEM", self ? "receiver" : "ALL", "", "status", "", "", "" }, parsed.Content);
        Assert.Equal(1u, parsed.Count); Assert.Equal(0u, parsed.ServerID); Assert.Equal(0u, parsed.UnionType);
        Assert.Same(heldPlayer, client.Player); Assert.Same(heldView, client.Player.View); Assert.Same(viewOwner, heldView.Owner);
        Assert.False(client.FullLoading); Assert.False(viewOwner.FullLoading);
        Assert.Null(client.Map); Assert.Null(viewOwner.Map);
        Assert.Null(client.Socket!.Connection); Assert.Null(viewOwner.Socket!.Connection);
        Assert.Equal(0, scope.Packet.Position); // Actual using-dispose calls native Reuse.
        Assert.Equal(0, LegacyNativePacketRecycle.Count);
        Assert.Equal(queued, Bytes(scope.Packet));
        scope.Packet.Seek(4); scope.Packet.Write(uint.MaxValue);
        Assert.Equal(queued, Assert.Single(screen ? viewQueue : clientQueue)); // Queue copied native bytes.
    });

    [Fact]
    public Task DefaultDirectMessageDoesNotDereferencePlayerAndKeepsNativeRedTopLeftDefaults() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(recycle: true);
        var queue = new Queue<byte[]>();
        var client = Client(queue);
        client.Player = null!;
        client.SendSysMesage("default");
        var parsed = Decode(Assert.Single(queue));
        Assert.Equal(16711680u, parsed.Color); Assert.Equal(2005u, parsed.ChatType);
        Assert.Equal(new[] { "SYSTEM", "ALL", "", "default", "", "", "" }, parsed.Content);
        Assert.Null(client.Player); Assert.Equal(0, scope.Packet.Position);
        Assert.Equal(0, LegacyNativePacketRecycle.Count);
    });

    [Fact]
    public Task NullViewFaultsAfterSerializationInBothScreenBranchesAndUsingStillReusesTheRealPacket() => NativePacketTestThread.Run(() =>
    {
        foreach (bool self in new[] { false, true })
        {
            using var scope = new Scope(recycle: true);
            var queue = new Queue<byte[]>();
            var client = Client(queue);
            client.Player.Name = "receiver"; client.Player.View = null!;
            Assert.Throws<NullReferenceException>(() => client.SendSysMesage("screen", SendScren: true, self: self));
            Assert.Empty(queue); Assert.Null(client.Player.View);
            var parsed = Decode(Bytes(scope.Packet));
            Assert.Equal(new[] { "SYSTEM", self ? "receiver" : "ALL", "", "screen", "", "", "" }, parsed.Content);
            Assert.Equal(0, scope.Packet.Position); Assert.Equal(0, LegacyNativePacketRecycle.Count);
        }
    });

    [Fact]
    public Task ScreenViewQueuesItsActualOwnerBeforeMissingMapViewFaultWithoutRepairingIt() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(recycle: true);
        var clientQueue = new Queue<byte[]>(); var viewQueue = new Queue<byte[]>();
        var client = Client(clientQueue); var viewOwner = Client(viewQueue);
        client.Player.Name = "receiver";
        viewOwner.Map = new LegacyNativeGameMap(72, 72, 1002);
        client.Player.View = viewOwner.Player.View;
        Assert.Throws<NullReferenceException>(() => client.SendSysMesage("partial", SendScren: true, self: true));
        var parsed = Decode(Assert.Single(viewQueue));
        Assert.Equal("receiver", parsed.Content[1]); Assert.Equal("partial", parsed.Content[3]);
        Assert.Empty(clientQueue); Assert.Null(viewOwner.Map.View);
        Assert.Equal(0, scope.Packet.Position); Assert.Equal(0, LegacyNativePacketRecycle.Count);
        Assert.Null(viewOwner.Socket!.Connection);
    });

    [Fact]
    public Task FakeSendGuardRunsAfterSystemMessageSerializationAndDoesNotSkipRecycleDisposal() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(recycle: true);
        var queue = new Queue<byte[]>();
        var client = Client(queue);
        client.Fake = true;
        client.SendSysMesage("fake");
        Assert.Empty(queue);
        var parsed = Decode(Bytes(scope.Packet));
        Assert.Equal("fake", parsed.Content[3]); Assert.Equal(2005u, parsed.ChatType); Assert.Equal(16711680u, parsed.Color);
        Assert.Equal(0, scope.Packet.Position); Assert.Equal(0, LegacyNativePacketRecycle.Count);
        Assert.True(client.Fake); Assert.Null(client.Socket!.Connection);
    });
}
