using System.Reflection;
using System.Text;
using WolfConquer.Core.Networking;
using Pipe = WolfConquer.Core.World.LegacyNativePipeClientRuntime;
using Socket = WolfConquer.Core.Networking.LegacyNativeSecuritySocket;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePipeTransportTests
{
    [Fact]
    public Task DeadPipeSendAndDisconnectReturnBeforeNullSocketOrPacketAccess() => NativePacketTestThread.Run(() =>
    {
        var pipe = new Pipe();
        pipe.Send(null!);
        pipe.Disconnect();
        Assert.False(pipe.Alive);
        Assert.Null(pipe.Socket);
        Assert.Null(pipe.Owner);
    });

    [Fact]
    public Task PipeSendQueuesOwnedPacketCopyThroughPassedSocketAndIgnoresOwnerSendState() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Owner.Fake = true;
        scope.Owner.FullLoading = false;
        scope.Owner.Player = null!;
        scope.Packet.InitWriter();
        scope.Packet.Write(0x01020304u);
        scope.Packet.Finalize(104);
        byte[] expected = Convert.FromHexString("0800680004030201");

        scope.Pipe.Send(scope.Packet);

        byte[] queued = Assert.Single(scope.Queue);
        Assert.Equal(expected, queued);
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
        scope.Packet.Seek(4);
        scope.Packet.Write(0u);
        Assert.Equal(expected, queued);
        Assert.True(scope.Pipe.Alive);
        Assert.True(scope.Socket.Alive);
        Assert.Null(scope.Socket.Connection);
    });

    [Fact]
    public Task AlivePipeWithNullSocketPropagatesFaultButRealSocketSwallowsNullPacket() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var realSocket = scope.Pipe.Socket!;
        scope.Pipe.Socket = null!;
        Assert.Throws<NullReferenceException>(() => scope.Pipe.Send(scope.Packet));
        Assert.True(scope.Pipe.Alive);
        Assert.Empty(scope.Queue);
        scope.Pipe.Socket = realSocket;
        scope.Pipe.Send(null!);
        Assert.Empty(scope.Queue);
        Assert.True(scope.Pipe.Alive);
        Assert.True(realSocket.Alive);
    });

    [Fact]
    public Task LivePipeDoesNotOverrideSocketDeadGateOrRepairMissingNativeQueue() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Socket.Alive = false;
        scope.Pipe.Send(null!);
        Assert.True(scope.Pipe.Alive);
        Assert.False(scope.Socket.Alive);
        Assert.Empty(scope.Queue);
        scope.Socket.Alive = true;
        QueueField.SetValue(scope.Socket, null);
        scope.Packet.InitWriter();
        scope.Packet.Write(7u);
        scope.Packet.Finalize(104);
        scope.Pipe.Send(scope.Packet);
        Assert.Null(QueueField.GetValue(scope.Socket));
        Assert.True(scope.Pipe.Alive);
        Assert.True(scope.Socket.Alive);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task PipeSendAdvancesRealSocketCipherAcrossOrderedEntriesWithoutReinitializingIt() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        byte[] firstBytes = Convert.FromHexString("0800680004030201");
        byte[] secondBytes = Convert.FromHexString("08006800DDCCBBAA");
        using var first = new LegacyNativePacket(firstBytes);
        using var second = new LegacyNativePacket(secondBytes);
        var actualCipher = Cipher();
        scope.Socket.Crypto = actualCipher;
        var control = new LegacyGameServerCipher(Encoding.ASCII.GetBytes("0123456789ABCDEF"));
        var expectedFirst = new byte[firstBytes.Length];
        var expectedSecond = new byte[secondBytes.Length];
        control.Encrypt(firstBytes, expectedFirst);
        control.Encrypt(secondBytes, expectedSecond);

        scope.Pipe.Send(first);
        scope.Pipe.Send(second);

        var queued = scope.Queue.ToArray();
        Assert.Equal(2, queued.Length);
        Assert.Equal(expectedFirst, queued[0]);
        Assert.Equal(expectedSecond, queued[1]);
        Assert.Same(actualCipher, scope.Socket.Crypto);
        Assert.Equal(firstBytes, NativeSpellTestFixture.Bytes(first));
        Assert.Equal(secondBytes, NativeSpellTestFixture.Bytes(second));
        Assert.Null(scope.Socket.Connection);
    });

    [Fact]
    public Task DisconnectClearsPipeAliveBeforeUncreatedSocketQueueFaultAndWillNotRetry() => NativePacketTestThread.Run(() =>
    {
        var socket = NewSocket();
        var pipe = new Pipe(socket, null!);
        Assert.Null(QueueField.GetValue(socket));
        Assert.Throws<NullReferenceException>(() => pipe.Disconnect());
        Assert.False(pipe.Alive);
        Assert.False(socket.Alive);
        Assert.Same(pipe, socket.Client);
        pipe.Socket = null!;
        pipe.Disconnect();
        Assert.False(pipe.Alive);
        Assert.Null(pipe.Socket);
    });

    [Fact]
    public Task DisconnectWithReplacedNullSocketKeepsEarlierPipeDeathAndDoesNotClearOwnerField() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Owner.PipeClient = scope.Pipe;
        scope.Pipe.Socket = null!;
        Assert.Throws<NullReferenceException>(() => scope.Pipe.Disconnect());
        Assert.False(scope.Pipe.Alive);
        Assert.Same(scope.Pipe, scope.Owner.PipeClient);
        Assert.True(scope.Owner.IsConnectedInterServer());
        Assert.True(scope.Socket.Alive);
        scope.Pipe.Disconnect();
        Assert.False(scope.Pipe.Alive);
    });

    [Fact]
    public Task DisconnectAlreadyDeadRealSocketDoesNotInventOwnerUnbindingOrDropQueuedBytes() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Owner.PipeClient = scope.Pipe;
        scope.Socket.Alive = false;
        byte[] previous = [7, 8, 9];
        scope.Queue.Enqueue(previous);

        scope.Pipe.Disconnect();

        Assert.False(scope.Pipe.Alive);
        Assert.False(scope.Socket.Alive);
        Assert.Same(previous, Assert.Single(scope.Queue));
        Assert.Same(scope.Pipe, scope.Owner.PipeClient);
        Assert.Same(scope.Pipe, scope.Socket.Client);
        Assert.True(scope.Owner.IsConnectedInterServer());
        Assert.Null(scope.Socket.Connection);
    });

    [Fact]
    public Task ActualSocketTimerFaultPreservesPipeAndSocketDeathBeforeOsCleanupOrCallbacks() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var timer = new FailingTimer();
        typeof(Socket).GetField("TimerSubscriptions", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(scope.Socket, new IDisposable?[] { timer });
        scope.Owner.PipeClient = scope.Pipe;

        var fault = Assert.Throws<InvalidOperationException>(() => scope.Pipe.Disconnect());

        Assert.Same(timer.Fault, fault);
        Assert.Equal(1, timer.Calls);
        Assert.False(scope.Pipe.Alive);
        Assert.False(scope.Socket.Alive);
        Assert.Same(scope.Pipe, scope.Socket.Client);
        Assert.Same(scope.Pipe, scope.Owner.PipeClient);
        Assert.True(scope.Owner.IsConnectedInterServer());
        Assert.Empty(scope.Queue);
        Assert.Null(scope.Socket.Connection); // Timer throws before any OS cleanup.
        scope.Pipe.Disconnect();
        Assert.Equal(1, timer.Calls);
    });
}
