using System.Reflection;
using System.Text;
using WolfConquer.Core.Networking;
using NativeSocket = WolfConquer.Core.Networking.LegacyNativeSecuritySocket;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeSecuritySocketQueueTests
{
    private static readonly byte[] FirstFrame = Convert.FromHexString("0D00800808011205087910AC02");
    private static readonly byte[] SecondFrame = Convert.FromHexString("0A008008080012020879");
    private static readonly byte[] Key = Encoding.ASCII.GetBytes("0123456789ABCDEF");
    private static readonly FieldInfo QueueField = typeof(NativeSocket).GetField(
        "SendQueue", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo TimersField = typeof(NativeSocket).GetField(
        "TimerSubscriptions", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Fact]
    public Task ConstructorLeavesQueueNullAndOwnsDistinctActual1024ByteHandshakeBuffers() => NativePacketTestThread.Run(() =>
    {
        var first = Socket();
        var second = Socket();

        Assert.False(first.Alive);
        Assert.False(second.Alive);
        Assert.Null(Queue(first));
        Assert.Null(Queue(second));
        Assert.Null(TimersField.GetValue(first));
        Assert.Null(first.Connection);
        Assert.Null(first.Crypto);
        Assert.NotSame(first.DHKeyBuffer, first.EncryptedDHKeyBuffer);
        Assert.NotSame(first.DHKeyBuffer, second.DHKeyBuffer);
        Assert.NotSame(first.EncryptedDHKeyBuffer, second.EncryptedDHKeyBuffer);
        Assert.NotSame(first.DHKeyBuffer.buffer, first.EncryptedDHKeyBuffer.buffer);
        Assert.NotSame(first.DHKeyBuffer.buffer, second.DHKeyBuffer.buffer);
        Assert.All(new[] { first.DHKeyBuffer, first.EncryptedDHKeyBuffer, second.DHKeyBuffer, second.EncryptedDHKeyBuffer }, buffer =>
        {
            Assert.IsType<LegacyNativeReceiveBuffer>(buffer);
            Assert.Equal(1024, buffer.buffer.Length);
            Assert.Equal(1024, buffer.MaxLength());
            Assert.Equal(0, buffer.Length());
        });
    });

    [Fact]
    public Task DeadSendReturnsBeforeNullPacketOrUncreatedQueueAccess() => NativePacketTestThread.Run(() =>
    {
        var socket = Socket();

        socket.Send(null!);

        Assert.False(socket.Alive);
        Assert.Null(Queue(socket));
        Assert.Null(socket.Connection);
    });

    [Fact]
    public Task LivePlaintextSendQueuesOwnedCopyNotAliasedToInputOrPacketMemory() => NativePacketTestThread.Run(() =>
    {
        var socket = Socket();
        var queue = SeedQueue(socket);
        socket.Alive = true;
        var source = FirstFrame.ToArray();
        using var packet = new LegacyNativePacket(source);

        socket.Send(packet);

        var queued = Assert.Single(queue);
        Assert.Equal(FirstFrame, queued);
        Assert.NotSame(source, queued);
        Assert.Equal(0, packet.Position);
        Assert.Equal(FirstFrame.Length, packet.Size);
        source[0] ^= 0xFF;
        packet.Seek(0);
        packet.Write((byte)0x55);
        Assert.Equal(FirstFrame, queued);
        Assert.Same(queue, Queue(socket));
        Assert.Null(socket.Connection); // Component queue proof only, no socket/pump/Create.
    });

    [Fact]
    public Task TwoEncryptedPacketsAdvanceTheActualNativeCipherAcrossQueueEntries() => NativePacketTestThread.Run(() =>
    {
        var socket = Socket();
        var queue = SeedQueue(socket);
        socket.Alive = true;
        socket.Crypto = Cipher();
        using var first = new LegacyNativePacket(FirstFrame);
        using var second = new LegacyNativePacket(SecondFrame);
        var control = new LegacyGameServerCipher(Key);
        var expectedFirst = new byte[FirstFrame.Length];
        var expectedSecond = new byte[SecondFrame.Length];
        control.Encrypt(FirstFrame, expectedFirst);
        control.Encrypt(SecondFrame, expectedSecond);

        socket.Send(first);
        socket.Send(second);

        var entries = queue.ToArray();
        Assert.Equal(2, entries.Length);
        Assert.Equal(expectedFirst, entries[0]);
        Assert.Equal(expectedSecond, entries[1]);
        Assert.NotSame(entries[0], entries[1]);
        Assert.Equal(0, first.Position);
        Assert.Equal(0, second.Position);
        Assert.Null(socket.Connection);
    });

    [Fact]
    public Task MissingQueueFaultIsSwallowedOnlyAfterNativeCipherStateHasAdvanced() => NativePacketTestThread.Run(() =>
    {
        var socket = Socket();
        socket.Alive = true;
        socket.Crypto = Cipher();
        using var first = new LegacyNativePacket(FirstFrame);
        using var second = new LegacyNativePacket(SecondFrame);
        var control = new LegacyGameServerCipher(Key);
        control.Encrypt(FirstFrame, new byte[FirstFrame.Length]);
        var expectedContinuation = new byte[SecondFrame.Length];
        control.Encrypt(SecondFrame, expectedContinuation);
        var resetOutput = new byte[SecondFrame.Length];
        new LegacyGameServerCipher(Key).Encrypt(SecondFrame, resetOutput);

        socket.Send(first); // MODEL uncreated SendQueue remains null; no native Create claim.

        Assert.True(socket.Alive);
        Assert.Null(Queue(socket));
        var queue = SeedQueue(socket);
        socket.Send(second);
        var queued = Assert.Single(queue);
        Assert.Equal(expectedContinuation, queued);
        Assert.NotEqual(Convert.ToHexString(resetOutput), Convert.ToHexString(queued));
        Assert.Null(socket.Connection);
    });

    [Fact]
    public Task LiveNullPacketFaultIsSwallowedWithoutChangingQueuedEntries() => NativePacketTestThread.Run(() =>
    {
        var socket = Socket();
        var queue = SeedQueue(socket);
        byte[] existing = [7, 8, 9];
        queue.Enqueue(existing);
        socket.Alive = true;
        socket.Crypto = Cipher();

        socket.Send(null!);

        Assert.Same(existing, Assert.Single(queue));
        Assert.Same(queue, Queue(socket));
        Assert.True(socket.Alive);
        Assert.Null(socket.Connection);
    });

    [Fact]
    public Task DisconnectUncreatedQueueFaultsBeforeTheAlreadyDeadGuard() => NativePacketTestThread.Run(() =>
    {
        var callbacks = 0;
        var socket = new NativeSocket(_ => callbacks++, null!);
        var timer = new ControlledTimer(throws: true);
        TimersField.SetValue(socket, new IDisposable?[] { timer });
        Assert.False(socket.Alive);
        Assert.Null(Queue(socket));

        Assert.Throws<NullReferenceException>(() => socket.Disconnect());

        Assert.False(socket.Alive);
        Assert.Equal(0, timer.DisposeCalls);
        Assert.Equal(0, callbacks);
        Assert.Null(socket.Connection);
    });

    [Fact]
    public Task DisconnectAlreadyDeadWithModelQueueReturnsBeforeTimersOrCallback() => NativePacketTestThread.Run(() =>
    {
        var callbacks = 0;
        var socket = new NativeSocket(_ => callbacks++, null!);
        var queue = SeedQueue(socket);
        byte[] existing = [1];
        queue.Enqueue(existing);
        var timer = new ControlledTimer(throws: true);
        TimersField.SetValue(socket, new IDisposable?[] { timer });

        socket.Disconnect();

        Assert.False(socket.Alive);
        Assert.Same(existing, Assert.Single(queue));
        Assert.Equal(0, timer.DisposeCalls);
        Assert.Equal(0, callbacks);
        Assert.Null(socket.Connection);
    });

    [Fact]
    public Task DisconnectTimerFaultPublishesDeadStateBeforeCleanupTryAndSuppressesLaterTimersAndCallback() => NativePacketTestThread.Run(() =>
    {
        var callbacks = 0;
        var socket = new NativeSocket(_ => callbacks++, null!);
        var queue = SeedQueue(socket);
        var failing = new ControlledTimer(throws: true);
        var later = new ControlledTimer(throws: false);
        IDisposable?[] timers = [null, failing, later];
        TimersField.SetValue(socket, timers);
        socket.Alive = true;
        Assert.Null(socket.Connection);

        var fault = Assert.Throws<InvalidOperationException>(() => socket.Disconnect());

        Assert.Same(failing.Fault, fault);
        Assert.False(socket.Alive);
        Assert.Empty(queue);
        Assert.Same(timers, TimersField.GetValue(socket));
        Assert.Equal(1, failing.DisposeCalls);
        Assert.Equal(0, later.DisposeCalls);
        Assert.Equal(0, callbacks);
        Assert.Null(socket.Connection); // Timer throws before the OS cleanup try is reached.
    });

    private static NativeSocket Socket() => new NativeSocket(null!, null!);

    private static LegacyNativeTQCast5 Cipher()
    {
        var cipher = new LegacyNativeTQCast5();
        cipher.GenerateKey(Key);
        return cipher;
    }

    private static Queue<byte[]>? Queue(NativeSocket socket) => (Queue<byte[]>?)QueueField.GetValue(socket);

    private static Queue<byte[]> SeedQueue(NativeSocket socket)
    {
        // Controlled MODEL state only: constructor does not own this native Create assignment.
        var queue = new Queue<byte[]>();
        QueueField.SetValue(socket, queue);
        return queue;
    }

    private sealed class ControlledTimer(bool throws) : IDisposable
    {
        public readonly InvalidOperationException Fault = new("Owned timer-disposal fault.");
        public int DisposeCalls;

        public void Dispose()
        {
            DisposeCalls++;
            if (throws) throw Fault;
        }
    }
}
