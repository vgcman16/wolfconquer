using System.Reflection;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using NativeSocket = WolfConquer.Core.Networking.LegacyNativeSecuritySocket;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeSecuritySocketQueueTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(1025)]
    public Task InvalidPhysicalSizeCannotPublishBytesOrAdvanceCipher(int size) => NativePacketTestThread.Run(() =>
    {
        var socket = Socket(); var queue = SeedQueue(socket); socket.Alive = true; socket.Crypto = Cipher();
        byte[] existing = [7, 8, 9]; queue.Enqueue(existing);
        using var invalid = new LegacyNativePacket(FirstFrame); invalid.Size = size;
        Assert.Equal(NativeSocket.SendResult.Refused, socket.TrySend(invalid));
        Assert.Same(existing, Assert.Single(queue));
        using var valid = new LegacyNativePacket(SecondFrame);
        Assert.Equal(NativeSocket.SendResult.Queued, socket.TrySend(valid));
        byte[] expected = new byte[SecondFrame.Length]; new LegacyGameServerCipher(Key).Encrypt(SecondFrame, expected);
        Assert.Same(existing, queue.Dequeue()); Assert.Equal(expected, Assert.Single(queue)); Assert.Null(socket.Connection);
    });

    [Fact]
    public Task DisposedStorageRefusesWithoutReadingItsRetainedPointerOrConsumingCipher() => NativePacketTestThread.Run(() =>
    {
        var socket = Socket(); var queue = SeedQueue(socket); socket.Alive = true; socket.Crypto = Cipher();
        using var packet = new LegacyNativePacket(FirstFrame); packet.Dispose();
        Assert.Equal(NativeSocket.SendResult.Refused, socket.TrySend(packet)); Assert.Empty(queue);
        using var next = new LegacyNativePacket(SecondFrame); socket.Send(next);
        byte[] expected = new byte[SecondFrame.Length]; new LegacyGameServerCipher(Key).Encrypt(SecondFrame, expected);
        Assert.Equal(expected, Assert.Single(queue)); Assert.Null(socket.Connection);
    });

    [Fact]
    public Task RawSocketKeepsZeroLengthAndLargerOwnedBuffersWithoutInventingTheGameClientLimit() => NativePacketTestThread.Run(() =>
    {
        var socket = Socket(); var queue = SeedQueue(socket); socket.Alive = true;
        using var empty = new LegacyNativePacket(Array.Empty<byte>());
        Assert.Equal(NativeSocket.SendResult.Queued, socket.TrySend(empty));
        Assert.Empty(Assert.Single(queue)); queue.Clear();
        byte[] input = Enumerable.Repeat((byte)0xA5, 1033).ToArray(); using var large = new LegacyNativePacket(input);
        Assert.Equal(NativeSocket.SendResult.Queued, socket.TrySend(large)); Assert.Equal(input, Assert.Single(queue));
        Assert.Equal(0, large.Position); Assert.Equal(input.Length, large.Size); Assert.Null(socket.Connection);
    });

    [Fact]
    public Task SelectedSocketBoundIsCheckedBeforeCipherOrQueuePublication() => NativePacketTestThread.Run(() =>
    {
        var socket = Socket(); var queue = SeedQueue(socket); socket.Alive = true; socket.Crypto = Cipher();
        using var large = new LegacyNativePacket(new byte[1033]);
        Assert.Equal(NativeSocket.SendResult.Refused, socket.TrySend(large, 1032)); Assert.Empty(queue);
        using var next = new LegacyNativePacket(SecondFrame); socket.Send(next);
        byte[] expected = new byte[SecondFrame.Length]; new LegacyGameServerCipher(Key).Encrypt(SecondFrame, expected);
        Assert.Equal(expected, Assert.Single(queue)); Assert.Null(socket.Connection);
    });

    [Fact]
    public Task CheckedManagedCipherFailureFencesTheSocketWithoutPretendingToRollback() => NativePacketTestThread.Run(() =>
    {
        var socket = Socket(); var queue = SeedQueue(socket); socket.Alive = true;
        var cipher = Cipher(); socket.Crypto = cipher;
        var ivField = typeof(LegacyNativeTQCast5).GetField("EncIvec", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var countField = typeof(LegacyNativeTQCast5).GetField("EncNum", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var iv = new byte[7]; var count = (int[])countField.GetValue(cipher)!; count[0] = 6;
        ivField.SetValue(cipher, iv);
        byte[] existing = [7, 8, 9]; queue.Enqueue(existing);
        using var packet = new LegacyNativePacket(FirstFrame);
        // Real managed-array prestate: first byte uses IV[6], then the checked
        // IV[7] access fails. The owned pinned plaintext pointer is always valid.
        Assert.Equal(NativeSocket.SendResult.CipherStateIndeterminate, socket.TrySend(packet));
        Assert.Equal(7, count[0]); Assert.Equal(FirstFrame[0], iv[6]); Assert.Same(existing, Assert.Single(queue));
        Assert.Equal(FirstFrame, packet.CaptureSendBytes());
        ivField.SetValue(cipher, new byte[16]);
        Assert.Equal(NativeSocket.SendResult.CipherStateIndeterminate, socket.TrySend(packet));
        socket.Crypto = Cipher();
        Assert.Equal(NativeSocket.SendResult.CipherStateIndeterminate, socket.TrySend(packet));
        var owner = new LegacyNativeGameClientRuntime { Socket = socket };
        byte[] caller = Convert.FromHexString("000080080102030405060708CCCCCCCCCCCCCCCC"); byte[] before = caller.ToArray();
        owner.Send(caller);
        Assert.Equal(before, caller); Assert.Same(existing, Assert.Single(queue));
        Assert.Equal(7, count[0]); Assert.True(socket.Alive); Assert.Null(socket.Connection);
    });

    [Fact]
    public async Task SendCaptureAndDisposeShareTheActualPacketLifetimeGate()
    {
        LegacyNativePacket? created = null;
        byte[] input = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        await NativePacketTestThread.Run(() => created = new LegacyNativePacket(input));
        using var packet = created ?? throw new InvalidOperationException("Owned packet was not created.");
        using var start = new Barrier(2);
        void Rendezvous()
        {
            if (!start.SignalAndWait(TimeSpan.FromSeconds(10))) throw new TimeoutException("send capture workers did not rendezvous");
        }
        Task<byte[]?> capture = Task.Factory.StartNew(() =>
        {
            Rendezvous();
            try { return packet.CaptureSendBytes(); }
            catch (ObjectDisposedException) { return null; }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task dispose = Task.Factory.StartNew(() => { Rendezvous(); packet.Dispose(); },
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        await Task.WhenAll(capture, dispose);
        byte[]? snapshot = await capture;
        if (snapshot is not null) Assert.Equal(input, snapshot);
        Assert.Throws<ObjectDisposedException>(() => packet.CaptureSendBytes());
    }
}
