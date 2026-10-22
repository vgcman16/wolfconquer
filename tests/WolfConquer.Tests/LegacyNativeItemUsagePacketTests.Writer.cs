using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.World;
using Packet = WolfConquer.Core.Networking.LegacyNativePacket;
using ActionID = WolfConquer.Core.World.LegacyNativeItemUsagePacket.ItemUsuageID;

namespace WolfConquer.Tests;

public sealed unsafe partial class LegacyNativeItemUsagePacketTests
{
    [Fact]
    public Task WriterResetsToFourNarrowsActionAndLeavesAll57ReservedBytesUntouched() => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope(null);
        byte[] original = Enumerable.Repeat((byte)0xD6, 256).ToArray();
        byte[] expected = original.ToArray();
        byte[] prefix = Convert.FromHexString("6300590844332211EFCDAB8967452301EFBE8877665507000000A4A3A2A1B4B3B2B1");
        prefix.CopyTo(expected, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(91), 0xDEADBEEF);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(95), 0x12345678);
        var args = new List<uint> { 0xDEADBEEF, 0x12345678 };
        using var packet = new Packet(original);
        packet.Seek(137);
        packet.Size = 233;

        Packet returned = packet.ItemUsageCreate((ActionID)0x1234BEEF, 0x11223344,
            0x0123456789ABCDEFul, 0x55667788, 7, 0xA1A2A3A4, 0xB1B2B3B4, args);

        Assert.Same(packet, returned);
        Assert.Equal(99, packet.Size);
        Assert.Equal(4, packet.Position);
        Assert.Equal(expected, Copy(packet, original.Length));
        Assert.Equal(Enumerable.Repeat((byte)0xD6, 57), Copy(packet, 91)[34..]);
        Assert.Equal(Enumerable.Repeat((byte)0xD6, 256), original);
        Assert.Equal(new uint[] { 0xDEADBEEF, 0x12345678 }, args);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NullAndEmptyArgsDoNotChangeCountOrReservedBytesAndReaderStopsEightBytesEarlier(bool empty) => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope("TQServer");
        byte[] original = Enumerable.Repeat((byte)0xCC, 256).ToArray();
        using var packet = new Packet(original);
        packet.ItemUsageCreate(ActionID.BuyItem, 0, 0, 123, 0, 0, 0, empty ? [] : null!);

        Assert.Equal(99, packet.Size);
        Assert.Equal(4, packet.Position);
        byte[] bytes = Copy(packet, 99);
        Assert.Equal((ushort)91, BinaryPrimitives.ReadUInt16LittleEndian(bytes));
        Assert.Equal((ushort)2137, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2)));
        Assert.Equal(Enumerable.Repeat((byte)0xCC, 57), bytes[34..91]);
        Assert.Equal(Encoding.ASCII.GetBytes("TQServer"), bytes[91..99]);

        packet.GetUsageItem(out _, out _, out _, out var timestamp, out var count, out _, out _, out var args, out var loader);
        Assert.Equal(123u, timestamp);
        Assert.Equal(0u, count);
        Assert.Empty(args);
        Assert.Equal((byte)0xCC, loader);
        Assert.Equal(83, packet.Position); // Parser skips only79 bytes from4 when no args.
        Assert.Equal(99, packet.Size);
        Assert.Equal(Enumerable.Repeat((byte)0xCC, 256), original);
    });

    [Fact]
    public Task WriterWritesAllProvidedArgsEvenBeyondReaderCountLimitWithoutRewritingSecondParameter() => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope(null);
        byte[] original = Enumerable.Repeat((byte)0xA6, 512).ToArray();
        var args = Enumerable.Range(0, 60).Select(i => (uint)i).ToList();
        using var packet = new Packet(original);

        packet.ItemUsageCreate(ActionID.DecomposeRelics, 17, 0, 1, uint.MaxValue, 0, 0, args);

        Assert.Equal(331, packet.Size);
        Assert.Equal(4, packet.Position);
        byte[] bytes = Copy(packet, original.Length);
        Assert.Equal(uint.MaxValue, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(22)));
        for (int i = 0; i < 60; i++)
            Assert.Equal((uint)i, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(91 + i * 4)));
        Assert.Equal(Enumerable.Repeat((byte)0xA6, 181), bytes[331..]);
        Assert.Equal(Enumerable.Range(0, 60).Select(i => (uint)i), args);
        Assert.Equal(Enumerable.Repeat((byte)0xA6, 512), original);
    });

    [Fact]
    public Task ZeroTimestampSamplesTheExistingNativeDateValueInsteadOfUsingZeroOrUnixTime() => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope(null);
        using var packet = new Packet(new byte[256]);
        uint before = unchecked((uint)DateTime.Now.Value());

        packet.ItemUsageCreate(ActionID.Ping, 17, 0, 0, 0, 0, 0);

        uint after = unchecked((uint)DateTime.Now.Value());
        uint observed = BinaryPrimitives.ReadUInt32LittleEndian(Copy(packet, 22).AsSpan(18));
        uint lower = unchecked(before - 10u);
        Assert.True(unchecked(observed - lower) <= unchecked(after - before + 20u));
        Assert.Equal(91, packet.Size);
        Assert.Equal(4, packet.Position);
    });

    [Fact]
    public Task WriterReaderRetainAsymmetricSignedActionAndCountDrivenArgsAndOldLoaderByte() => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope("TQServer");
        using var packet = new Packet(Enumerable.Repeat((byte)0xD6, 256).ToArray());
        packet.ItemUsageCreate((ActionID)0x1234BEEF, 17, 23, 31, 2, 41, 43, [47, 53]);

        packet.GetUsageItem(out var action, out var id, out var parameter, out var timestamp,
            out var parameter2, out var parameter3, out var parameter4, out var args, out var loader);

        Assert.Equal(0xFFFFBEEFu, (uint)action);
        Assert.Equal(17u, id);
        Assert.Equal(23ul, parameter);
        Assert.Equal(31u, timestamp);
        Assert.Equal(2u, parameter2);
        Assert.Equal(41u, parameter3);
        Assert.Equal(43u, parameter4);
        Assert.Equal(new uint[] { 47, 53 }, args);
        Assert.Equal((byte)0xD6, loader);
        Assert.Equal(99, packet.Position);
        Assert.Equal(107, packet.Size);
    });
}
