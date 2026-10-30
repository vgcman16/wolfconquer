using WolfConquer.Core.World;
using Packet = WolfConquer.Core.Networking.LegacyNativePacket;
using Dict = WolfConquer.Core.World.LegacyNativeMsgDictLottery;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;

namespace WolfConquer.Tests;

public sealed unsafe partial class LegacyNativePacketProtobufTests
{
    [Fact]
    public Task RawDeserializeIgnoresSuppliedInstanceAndPacketCursorSizeAndMemory() => NativePacketTestThread.Run(() =>
    {
        byte[] original = Enumerable.Repeat((byte)0xA7, 64).ToArray();
        byte[] payload = Convert.FromHexString("08011011");
        byte[] payloadBefore = payload.ToArray();
        var item = new Dict.Lottery { Type = 99, Exp = true };
        var retained = new List<Dict.Lottery> { item };
        var supplied = new Dict { Type = 9, Done = 23, Items2 = retained };
        using var packet = new Packet(original);
        packet.Seek(11);
        packet.Size = 37;

        Dict actual = packet.ProtoBufferDeserialize(supplied, payload);

        Assert.NotSame(supplied, actual);
        Assert.Equal(1u, actual.Type);
        Assert.Equal(17u, actual.Done);
        Assert.Empty(actual.Items2);
        Assert.Equal(9u, supplied.Type);
        Assert.Equal(23u, supplied.Done);
        Assert.Same(retained, supplied.Items2);
        Assert.Same(item, Assert.Single(supplied.Items2));
        Assert.Equal(99u, item.Type);
        Assert.True(item.Exp);
        Assert.Equal(11, packet.Position);
        Assert.Equal(37, packet.Size);
        Assert.Equal(original, Copy(packet, original.Length));
        Assert.Equal(payloadBefore, payload);
    });

    [Theory]
    [InlineData(null)]
    [InlineData("TQServer")]
    public Task FramedDeserializeStartsAtZeroWithActualSizeAndIgnoresOpcodeSealAndSuppliedObject(string? seal) => NativePacketTestThread.Run(() =>
    {
        using var savedSeal = new SealScope(seal);
        byte[] original = Frame(8, ushort.MaxValue, 0x08, 0x01, 0x10, 0x11);
        var supplied = new Dict { Type = 9, Done = 23 };
        var retained = supplied.Items2;
        using var packet = new Packet(original);
        packet.Size = 8 + Packet.SealSize; // Actual input owns the declared frame; spare allocation does not.
        packet.Seek(12);

        Dict actual = packet.ProtoBufferDeserialize(supplied);

        Assert.NotSame(supplied, actual);
        Assert.Equal(1u, actual.Type);
        Assert.Equal(17u, actual.Done);
        Assert.Empty(actual.Items2);
        Assert.Equal(9u, supplied.Type);
        Assert.Equal(23u, supplied.Done);
        Assert.Same(retained, supplied.Items2);
        Assert.Equal(8, packet.Position);
        Assert.Equal(8 + Packet.SealSize, packet.Size);
        Assert.Equal(original, Copy(packet, original.Length));
        Assert.Equal(seal is null ? 0 : 8, Packet.SealSize);
    });

    [Fact]
    public Task FramedDeserializeHonorsDeclaredPayloadLengthRatherThanFollowingTrailingFields() => NativePacketTestThread.Run(() =>
    {
        byte[] original = Frame(6, 2465, 0x08, 0x01, 0x10, 0x11);
        using var packet = new Packet(original);

        Dict actual = packet.ProtoBufferDeserialize(new Dict { Done = 99 });

        Assert.Equal(1u, actual.Type);
        Assert.Equal(0u, actual.Done); // Physically backed Done17 lies beyond declared payload.
        Assert.Equal(6, packet.Position);
        Assert.Equal(original.Length, packet.Size);
        Assert.Equal(original, Copy(packet, original.Length));
    });

    [Fact]
    public Task EmptyRawPayloadReturnsNewDefaultWithoutMergingSuppliedInstance() => NativePacketTestThread.Run(() =>
    {
        using var packet = new Packet(new byte[32]);
        packet.Seek(9);
        var supplied = new Dict { Type = 7, Done = 8 };

        Dict actual = packet.ProtoBufferDeserialize(supplied, []);

        Assert.NotSame(supplied, actual);
        Assert.NotNull(actual);
        Assert.Equal(0u, actual.Type);
        Assert.Equal(0u, actual.Done);
        Assert.Empty(actual.Items2);
        Assert.Equal(7u, supplied.Type);
        Assert.Equal(8u, supplied.Done);
        Assert.Equal(9, packet.Position);
        Assert.Equal(32, packet.Size);
    });

    [Fact]
    public Task EmptyFramedPayloadReturnsNewDefaultAndLeavesCursorAfterHeader() => NativePacketTestThread.Run(() =>
    {
        byte[] original = Frame(4, 0x1234);
        using var packet = new Packet(original);
        packet.Seek(19);
        var supplied = new Dict { Type = 7, Done = 8 };

        Dict actual = packet.ProtoBufferDeserialize(supplied);

        Assert.NotSame(supplied, actual);
        Assert.NotNull(actual);
        Assert.Equal(0u, actual.Type);
        Assert.Equal(0u, actual.Done);
        Assert.Empty(actual.Items2);
        Assert.Equal(7u, supplied.Type);
        Assert.Equal(8u, supplied.Done);
        Assert.Equal(4, packet.Position);
        Assert.Equal(original, Copy(packet, original.Length));
    });

    [Fact]
    public Task BothDeserializeOverloadsHaveNoCombatGearGate() => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeCombatGearQueryScope(false);
        byte[] original = Frame(8, 0, 0x08, 0x01, 0x10, 0x11);
        using var packet = new Packet(original);
        packet.Seek(13);
        Dict raw = packet.ProtoBufferDeserialize(new Dict(), Convert.FromHexString("08011011"));
        Assert.Equal(13, packet.Position);
        Dict framed = packet.ProtoBufferDeserialize(new Dict());
        Assert.Equal(8, packet.Position);
        Assert.Equal(1u, raw.Type);
        Assert.Equal(17u, raw.Done);
        Assert.Equal(raw.Type, framed.Type);
        Assert.Equal(raw.Done, framed.Done);
        Assert.False(Table.m_Load);
        Assert.Equal(original.Length, packet.Size);
        Assert.Equal(original, Copy(packet, original.Length));
    });
}
