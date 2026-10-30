using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeGameClientByteSendTests
{
    [Theory]
    [InlineData(11, false)]
    [InlineData(12, true)]
    public Task ByteHeaderAndTrailerBoundaryRefusesOverlapAndAcceptsTheFirstCompleteFrame(int length, bool accepted) => NativePacketTestThread.Run(() =>
    {
        using var logs = new OwnedLogScope(); var queue = new Queue<byte[]>(); var owner = Owner(queue);
        byte[] source = new byte[length]; owner.Send(source);
        if (accepted)
        {
            byte[] expected = Convert.FromHexString("040000005451536572766572");
            Assert.Equal(expected, source); Assert.Equal(expected, Assert.Single(queue)); logs.AssertNoErrors();
        }
        else
        {
            Assert.Equal(new byte[length], source); Assert.Empty(queue); logs.AssertOneError("LegacyNativeGameClientRuntime.Send");
        }
        Assert.Null(owner.Socket!.Connection);
    });

    [Theory]
    [InlineData(1)]
    [InlineData(11)]
    [InlineData(13)]
    [InlineData(65535)]
    public Task NonzeroByteHeaderMustMatchActualFrameBeforeCallerOrCipherChanges(int declared) => NativePacketTestThread.Run(() =>
    {
        using var logs = new OwnedLogScope(); var queue = new Queue<byte[]>(); var owner = Owner(queue);
        owner.Socket!.Crypto = Cipher(); byte[] source = Unsealed.ToArray();
        source[0] = (byte)declared; source[1] = (byte)(declared >> 8); byte[] before = source.ToArray();
        owner.Send(source);
        Assert.Equal(before, source); Assert.Empty(queue);
        byte[] next = Unsealed.ToArray(); owner.Send(next);
        byte[] expected = new byte[Sealed.Length]; new LegacyGameServerCipher(Key).Encrypt(Sealed, expected);
        Assert.Equal(expected, Assert.Single(queue)); Assert.Equal(Sealed, next);
        logs.AssertOneError("LegacyNativeGameClientRuntime.Send"); Assert.Null(owner.Socket.Connection);
    });

    // Corrected source only: never execute these ranges against an old fixed1024
    // allocation or unbounded native send implementation.
    [Theory]
    [InlineData(1025)]
    [InlineData(1032)]
    public unsafe Task CorrectedBoundaryCopiesUseTheActualAllocationAndRetainTheExistingSendFrontier(int length) => NativePacketTestThread.Run(() =>
    {
        using var logs = new OwnedLogScope(); var queue = new Queue<byte[]>(); var owner = Owner(queue);
        byte[] source = Enumerable.Repeat((byte)0xA5, length).ToArray(); source[0] = source[1] = 0;
        byte[] expected = (byte[])source.Clone();
        expected[0] = (byte)(length - 8); expected[1] = (byte)((length - 8) >> 8);
        Encoding.ASCII.GetBytes("TQServer").CopyTo(expected, length - 8);
        using var packet = new LegacyNativePacket(source);
        Assert.True(packet.Capacity >= source.Length); Assert.Equal(length, packet.Size);
        Assert.Equal(source, packet.CaptureSendBytes()); Assert.Equal(0, packet.Position);
        owner.Send(source);
        Assert.Equal(expected, source); Assert.Equal(expected, Assert.Single(queue));
        Assert.Null(owner.Socket!.Connection); logs.AssertNoErrors();
        packet.Dispose(); Assert.Equal((nuint)0, (nuint)packet.Memory);
        Assert.Throws<ObjectDisposedException>(() => packet.CaptureSendBytes());
    });

    [Fact]
    public Task ShortBytePreparationCannotMutateTheCallerOrConsumeAnyCipherState() => NativePacketTestThread.Run(() =>
    {
        using var logs = new OwnedLogScope(); var queue = new Queue<byte[]>(); var owner = Owner(queue);
        owner.Socket!.Crypto = Cipher(); byte[] shortInput = new byte[7];
        owner.Send(shortInput);
        Assert.Equal(new byte[7], shortInput); Assert.Empty(queue);
        byte[] valid = Unsealed.ToArray(); owner.Send(valid);
        byte[] expected = new byte[Sealed.Length]; new LegacyGameServerCipher(Key).Encrypt(Sealed, expected);
        Assert.Equal(Sealed, valid); Assert.Equal(expected, Assert.Single(queue));
        Assert.Null(owner.Socket.Connection); logs.AssertOneError("LegacyNativeGameClientRuntime.Send");
    });

    [Theory]
    [InlineData("negative")]
    [InlineData("oversize")]
    [InlineData("disposed")]
    public Task InvalidPacketStorageRefusesBeforeWarningCopyEncryptionOrQueueing(string fault) => NativePacketTestThread.Run(() =>
    {
        using var logs = new OwnedLogScope(); var queue = new Queue<byte[]>(); var owner = Owner(queue);
        owner.Socket!.Crypto = Cipher(); using var packet = new LegacyNativePacket(Unsealed);
        if (fault == "negative") packet.Size = -1;
        else if (fault == "oversize") packet.Size = packet.Capacity + 1;
        else packet.Dispose();
        owner.Send(packet);
        Assert.Empty(queue);
        byte[] valid = Unsealed.ToArray(); owner.Send(valid);
        byte[] expected = new byte[Sealed.Length]; new LegacyGameServerCipher(Key).Encrypt(Sealed, expected);
        Assert.Equal(expected, Assert.Single(queue)); Assert.Equal(Sealed, valid);
        Assert.Null(owner.Socket.Connection); logs.AssertOneError("LegacyNativeGameClientRuntime.Send");
    });

    [Fact]
    public Task ValidHugePacketIsFilteredWithoutMutatingItOrAdvancingTheCipher() => NativePacketTestThread.Run(() =>
    {
        using var logs = new OwnedLogScope(); var queue = new Queue<byte[]>(); var owner = Owner(queue);
        owner.Socket!.Crypto = Cipher(); byte[] bytes = Enumerable.Repeat((byte)0xA5, 1033).ToArray();
        using var packet = new LegacyNativePacket(bytes); packet.Seek(777);
        owner.Send(packet);
        Assert.Empty(queue); Assert.Equal(bytes, packet.CaptureSendBytes());
        Assert.Equal(777, packet.Position); Assert.Equal(bytes.Length, packet.Size);
        byte[] valid = Unsealed.ToArray(); owner.Send(valid);
        byte[] expected = new byte[Sealed.Length]; new LegacyGameServerCipher(Key).Encrypt(Sealed, expected);
        Assert.Equal(expected, Assert.Single(queue)); logs.AssertNoErrors(); Assert.Null(owner.Socket.Connection);
    });
}
