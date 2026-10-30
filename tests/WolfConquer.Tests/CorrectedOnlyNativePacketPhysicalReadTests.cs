using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using Packet = WolfConquer.Core.Networking.LegacyNativePacket;

namespace WolfConquer.Tests;

// Corrected-only controls: never execute these malformed native cases as an
// old-code red baseline. Every inspected byte belongs to an initialized allocation.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed unsafe partial class CorrectedOnlyNativePacketPhysicalReadTests
{
    [Fact]
    public Task IndependentLittleEndianAndSignedGoldenConsumesExactPhysicalInput() => NativePacketTestThread.Run(() =>
    {
        byte[] input = Convert.FromHexString("0100A5FE3412C7CFEFCDAB89EB32A4F8EFCDAB8967452301FEFFFFFFFFFFFFFF");
        using var packet = Exact(input);
        Assert.Equal((byte)1, packet.ReadUInt8());
        Assert.Equal((byte)0, packet.ReadUInt8());
        Assert.Equal((byte)0xA5, packet.ReadUInt8());
        Assert.Equal((sbyte)-2, packet.ReadInt8());
        Assert.Equal((ushort)0x1234, packet.ReadUInt16());
        Assert.Equal((short)-12345, packet.ReadInt16());
        Assert.Equal(0x89ABCDEFu, packet.ReadUInt32());
        Assert.Equal(-123456789, packet.ReadInt32());
        Assert.Equal(0x0123456789ABCDEFul, packet.ReadUInt64());
        Assert.Equal(-2L, packet.ReadInt64());
        Assert.Equal(input.Length, packet.Position);
        Assert.Equal(input.Length, packet.Size);
        Assert.Equal(input, Raw(packet));
        Assert.Throws<EndOfStreamException>(() => packet.ReadUInt8());
        Assert.Equal(input.Length, packet.Position);
    });

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(4)] [InlineData(8)]
    public Task ScalarAtCustomPhysicalEndSucceedsAndOneByteShortRefuses(int width) => NativePacketTestThread.Run(() =>
    {
        byte[] input = Enumerable.Repeat((byte)0xA5, width + 3).ToArray();
        using var packet = Exact(input);
        packet.Seek(3);
        ulong expected = width == 8 ? 0xA5A5A5A5A5A5A5A5ul : 0xA5A5A5A5A5A5A5A5ul & ((1ul << (width * 8)) - 1);
        Assert.Equal(expected, Scalar(packet, width));
        Assert.Equal(input.Length, packet.Position);
        packet.Seek(3);
        packet.Size = input.Length - 1;
        Assert.Throws<EndOfStreamException>(() => Scalar(packet, width));
        Assert.Equal(3, packet.Position);
        Assert.Equal(input.Length - 1, packet.Size);
        Assert.Equal(input, Raw(packet));
    });

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(4)] [InlineData(8)]
    public Task ScalarAtDefaultAllocationEndUsesActualInputInsteadOfOldMaxSentinel(int width) => NativePacketTestThread.Run(() =>
    {
        byte[] input = Enumerable.Repeat((byte)0xA5, Packet.MAX_SIZE).ToArray();
        using var packet = Exact(input);
        packet.Seek(input.Length - width);
        ulong expected = width == 8 ? 0xA5A5A5A5A5A5A5A5ul : 0xA5A5A5A5A5A5A5A5ul & ((1ul << (width * 8)) - 1);
        Assert.Equal(expected, Scalar(packet, width));
        Assert.Equal(input.Length, packet.Position);
        Assert.Throws<EndOfStreamException>(() => Scalar(packet, width));
        Assert.Equal(input.Length, packet.Position);
        Assert.Equal(input, Raw(packet));
    });

    [Fact]
    public Task SpareAllocationNeverExtendsTheByteArrayConstructorsActualInput() => NativePacketTestThread.Run(() =>
    {
        byte[] input = [0x34, 0x12, 0xA5];
        using var packet = new Packet(input); // Capacity1024 is not input.
        Assert.Equal((ushort)0x1234, packet.ReadUInt16());
        Assert.Throws<EndOfStreamException>(() => packet.ReadUInt16());
        Assert.Equal(2, packet.Position);
        Assert.Equal((byte)0xA5, packet.ReadUInt8());
        Assert.Equal(3, packet.Position);
        Assert.Throws<EndOfStreamException>(() => packet.ReadUInt8());
        Assert.Equal(input, Raw(packet, input.Length));
    });

    [Fact]
    public Task ByteAndForeignDestinationReadsAllowExactEndAndZeroLengthAtEof() => NativePacketTestThread.Run(() =>
    {
        byte[] input = [0x41, 0x42, 0, 0x44];
        using var packet = Exact(input);
        byte* destination = stackalloc byte[4] { 0xEE, 0xEE, 0xEE, 0xEE };
        packet.ReadUnsafe(destination, 4);
        Assert.Equal(input, new ReadOnlySpan<byte>(destination, 4).ToArray());
        Assert.Equal(4, packet.Position);
        Assert.Empty(packet.ReadBytes(0));
        Assert.Equal("", packet.ReadCString(0));
        packet.ReadUnsafe(null, 0);
        Assert.Equal(4, packet.Position);
        packet.Seek(0);
        Assert.Equal(input, packet.ReadBytes(4));
        Assert.Equal(4, packet.Position);
        Assert.Equal(input, Raw(packet));
    });

    [Fact]
    public Task ForeignDestinationUsesSnapshotForValidOverlappingOwnedStorage() => NativePacketTestThread.Run(() =>
    {
        using var packet = Exact([1, 2, 3, 4, 5, 6]);
        packet.ReadUnsafe(packet.Memory + 1, 4);
        Assert.Equal(new byte[] { 1, 1, 2, 3, 4, 6 }, Raw(packet));
        Assert.Equal(4, packet.Position);
        Assert.Equal(6, packet.Size);
    });

    [Theory]
    [InlineData("", "")]
    [InlineData("00", "")]
    [InlineData("410042", "A")]
    [InlineData("E78BBC", "狼")]
    [InlineData("C328", "\uFFFD(")]
    [InlineData("41FF0042", "A\uFFFD")]
    public Task CStringPreservesUtf8ReplacementAndNulRulesAtExactInputEnd(string hex, string expected) => NativePacketTestThread.Run(() =>
    {
        byte[] input = Convert.FromHexString(hex);
        using var packet = Exact(input.Length == 0 ? [0xA5] : input);
        packet.Size = input.Length;
        Assert.Equal(expected, packet.ReadCString(input.Length));
        Assert.Equal(input.Length, packet.Position);
        Assert.Equal(input.Length, packet.Size);
    });

    [Fact]
    public Task StringListGoldenIncludesEmptyUtf8AndNulRowsWithoutConsumingTrailingBytes() => NativePacketTestThread.Run(() =>
    {
        byte[] input = Convert.FromHexString("040300C3A95A00000300E4B8AD0300410042FE");
        using var packet = Exact(input);
        Assert.Equal(new[] { "éZ", "", "中", "A" }, packet.ReadStringList());
        Assert.Equal(input.Length - 1, packet.Position);
        Assert.Equal((byte)0xFE, packet.ReadUInt8());
        Assert.Equal(input.Length, packet.Position);
        Assert.Equal(input, Raw(packet));
    });

    [Fact]
    public Task EmptyStringListConsumesOnlyTheCountByte() => NativePacketTestThread.Run(() =>
    {
        using var packet = Exact([0, 0xA5]);
        Assert.Empty(packet.ReadStringList());
        Assert.Equal(1, packet.Position);
        Assert.Equal((byte)0xA5, packet.ReadUInt8());
    });

    private static Packet Exact(byte[] input)
    {
        var packet = new Packet(input.Length);
        Marshal.Copy(input, 0, (nint)packet.Memory, input.Length);
        packet.Size = input.Length; // Exact managed source captured above, never Capacity-derived.
        return packet;
    }

    private static byte[] Raw(Packet packet, int? count = null)
    {
        byte[] result = new byte[count ?? packet.Capacity];
        Marshal.Copy((nint)packet.Memory, result, 0, result.Length);
        return result;
    }

    private static ulong Scalar(Packet packet, int width) => width switch
    {
        1 => packet.ReadUInt8(), 2 => packet.ReadUInt16(),
        4 => packet.ReadUInt32(), 8 => packet.ReadUInt64(),
        _ => throw new ArgumentOutOfRangeException(nameof(width)),
    };
}
