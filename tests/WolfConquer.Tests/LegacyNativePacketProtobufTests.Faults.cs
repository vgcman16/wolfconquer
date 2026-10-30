using System.Buffers.Binary;
using Packet = WolfConquer.Core.Networking.LegacyNativePacket;
using Dict = WolfConquer.Core.World.LegacyNativeMsgDictLottery;

namespace WolfConquer.Tests;

public sealed unsafe partial class LegacyNativePacketProtobufTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public Task DeclaredLengthBelowHeaderRefusesWithoutConsumingCursor(int declaredLength) => NativePacketTestThread.Run(() =>
    {
        byte[] original = new byte[] { 0, 0, 0xA1, 0x09 };
        BinaryPrimitives.WriteUInt16LittleEndian(original, (ushort)declaredLength);
        using var packet = new Packet(original); // Native constructor reserves1024; exact4-byte header copied.
        packet.Seek(3);
        var supplied = new Dict { Type = 7 };

        Assert.Throws<InvalidDataException>(() => packet.ProtoBufferDeserialize(supplied));

        Assert.Equal(3, packet.Position);
        Assert.Equal(4, packet.Size);
        Assert.Equal(7u, supplied.Type);
        Assert.Equal(original, Copy(packet, original.Length));
    });

    [Fact]
    public Task FramedTruncatedVarintThrowsWithoutConsumingCursor() => NativePacketTestThread.Run(() =>
    {
        byte[] original = Frame(5, 2465, 0x08); // Tag is backed; missing varint is managed stream EOF, not pointer EOF.
        using var packet = new Packet(original);
        packet.Seek(11);
        var supplied = new Dict { Type = 7 };

        Assert.Throws<EndOfStreamException>(() => packet.ProtoBufferDeserialize(supplied));

        Assert.Equal(11, packet.Position);
        Assert.Equal(original.Length, packet.Size);
        Assert.Equal(7u, supplied.Type);
        Assert.Equal(original, Copy(packet, original.Length));
    });

    [Fact]
    public Task RawTruncatedVarintThrowsWithoutTouchingPacketCursorOrBacking() => NativePacketTestThread.Run(() =>
    {
        byte[] original = Enumerable.Repeat((byte)0xB7, 32).ToArray();
        byte[] payload = [0x08];
        using var packet = new Packet(original);
        packet.Seek(11);
        var supplied = new Dict { Type = 7 };

        Assert.Throws<EndOfStreamException>(() => packet.ProtoBufferDeserialize(supplied, payload));

        Assert.Equal(11, packet.Position);
        Assert.Equal(original.Length, packet.Size);
        Assert.Equal(7u, supplied.Type);
        Assert.Equal(new byte[] { 0x08 }, payload);
        Assert.Equal(original, Copy(packet, original.Length));
    });

    [Fact]
    public Task RawNullBufferThrowsBeforeSerializerWithoutChangingPacketOrSuppliedInstance() => NativePacketTestThread.Run(() =>
    {
        byte[] original = Enumerable.Repeat((byte)0xC7, 32).ToArray();
        using var packet = new Packet(original);
        packet.Seek(11);
        var supplied = new Dict { Type = 7 };

        Assert.Throws<ArgumentNullException>(() => packet.ProtoBufferDeserialize(supplied, null!));

        Assert.Equal(11, packet.Position);
        Assert.Equal(original.Length, packet.Size);
        Assert.Equal(7u, supplied.Type);
        Assert.Equal(original, Copy(packet, original.Length));
    });
}
