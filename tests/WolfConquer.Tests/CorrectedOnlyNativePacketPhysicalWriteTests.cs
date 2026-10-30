using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using Packet = WolfConquer.Core.Networking.LegacyNativePacket;

namespace WolfConquer.Tests;

// Corrected source only. Tiny allocations and invalid cursors below must NEVER
// be executed against the old writer, which could access unmanaged storage OOB.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed unsafe partial class CorrectedOnlyNativePacketPhysicalWriteTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(4)] [InlineData(8)]
    public Task ScalarFitsExactPhysicalEndForTinyDefaultAndLargerAllocations(int width) => NativePacketTestThread.Run(() =>
    {
        foreach (var capacity in new[] { width, Packet.MAX_SIZE, Packet.MAX_SIZE + 16 })
        {
            using var packet = new Packet(capacity); Fill(packet);
            packet.Seek(capacity - width); WriteWidth(packet, width);
            Assert.Equal(capacity, packet.Position); Assert.Equal(73, packet.Size);
            Assert.All(Bytes(packet)[(capacity - width)..], value => Assert.Equal((byte)0x5a, value));
            Refuses<InvalidOperationException>(packet, () => packet.Write((byte)1));
        }
    });

    [Theory]
    [InlineData(2)] [InlineData(4)] [InlineData(8)]
    public Task WholeScalarWidthRefusesOnGenuinelySmallerStorage(int width) => NativePacketTestThread.Run(() =>
    {
        using var packet = new Packet(width - 1); Fill(packet);
        Refuses<InvalidOperationException>(packet, () => WriteWidth(packet, width));
    });

    [Fact]
    public Task SignedAndBooleanValuesRetainIndependentLittleEndianGolden() => NativePacketTestThread.Run(() =>
    {
        using var packet = new Packet(32);
        packet.Write(true); packet.Write(false); packet.Write((byte)0xa5); packet.Write((sbyte)-2);
        packet.Write((ushort)0x1234); packet.Write((short)-12345); packet.Write(0x89abcdefU);
        packet.Write(-123456789); packet.Write(0x0123456789abcdefUL); packet.Write(-2L);
        Assert.Equal(Convert.FromHexString("0100A5FE3412C7CFEFCDAB89EB32A4F8EFCDAB8967452301FEFFFFFFFFFFFFFF"), Bytes(packet));
        Assert.Equal(32, packet.Position); Assert.Equal(0, packet.Size);
    });

    [Fact]
    public Task NegativeBeyondEndNullAndFullWidthWrappedCursorsRefuseWithoutNativeAccess() => NativePacketTestThread.Run(() =>
    {
        using var packet = new Packet(8); Fill(packet);
        foreach (var cursor in new[] { (nuint)packet.Memory - 1, (nuint)packet.Memory + 9, (nuint)0 })
        {
            packet.stream = (byte*)cursor;
            Refuses<InvalidOperationException>(packet, () => packet.Write((byte)1));
            Refuses<InvalidOperationException>(packet, () => packet.ZeroFill(0));
        }
        if (IntPtr.Size == 8)
        {
            // Legacy Position narrows this distant address to zero. No dereference.
            var distance = 1UL << 32;
            packet.stream = (byte*)((nuint)packet.Memory + (nuint)distance);
            Assert.Equal(0, packet.Position);
            Refuses<InvalidOperationException>(packet, () => packet.Write(1UL));
            Refuses<InvalidOperationException>(packet, () => packet.Finalize(0x1234));
        }
        packet.Seek(0);
    });

    [Theory]
    [InlineData("éZ", 5, "C3A9000000")]
    [InlineData("Aé", 4, "41C30000")]
    [InlineData("\uD83D\uDE00X", 1, "F0")]
    [InlineData("A", 0, "")]
    public Task FixedStringsRetainCharacterCountTruncationPaddingAndSurrogatePrefix(string value, int length, string hex) => NativePacketTestThread.Run(() =>
    {
        using var packet = new Packet(Math.Max(1, length)); Fill(packet); packet.Seek(0);
        packet.Write(value, length);
        Assert.Equal(Convert.FromHexString(hex), Bytes(packet)[..length]);
        Assert.Equal(length, packet.Position); Assert.Equal(73, packet.Size);
    });

    [Fact]
    public Task CompositeStringsRejectBeforeAnyPrefixPaddingOrContentChange() => NativePacketTestThread.Run(() =>
    {
        using var packet = new Packet(12); Fill(packet); packet.Seek(1);
        Refuses<InvalidOperationException>(packet, () => packet.Write("éZ", 12));
        Refuses<ArgumentOutOfRangeException>(packet, () => packet.Write("A", -1));
        Refuses<ArgumentNullException>(packet, () => packet.Write((string)null!, 2));
        packet.Seek(0);
        Refuses<InvalidOperationException>(packet, () => packet.Write(new[] { "éZ", "", "中" }));
        Refuses<ArgumentNullException>(packet, () => packet.Write((string[])null!));
    });

    [Fact]
    public Task StringListRetainsUtf8LengthAndNullEntryGoldensAtExactCapacity() => NativePacketTestThread.Run(() =>
    {
        using var packet = new Packet(13);
        packet.Write(new[] { "éZ", "", "中" });
        Assert.Equal(Convert.FromHexString("030300C3A95A00000300E4B8AD"), Bytes(packet));
        Assert.Equal(13, packet.Position);
        using var absent = new Packet(5);
        absent.Write(new[] { null!, "" });
        Assert.Equal(Convert.FromHexString("0200000000"), Bytes(absent));
    });

    [Fact]
    public Task StringListUsesRealCountAndByteLengthRepresentationBounds() => NativePacketTestThread.Run(() =>
    {
        using var countPacket = new Packet(513); Fill(countPacket);
        Refuses<ArgumentOutOfRangeException>(countPacket, () => countPacket.Write(new string[256]));
        countPacket.Write(new string[255]); Assert.Equal(511, countPacket.Position);
        Assert.Equal((byte)255, Bytes(countPacket)[0]);
        using var lengthPacket = new Packet(65_539); Fill(lengthPacket);
        Refuses<ArgumentOutOfRangeException>(lengthPacket, () => lengthPacket.Write(new[] { new string('A', 65_536) }));
        lengthPacket.Write(new[] { new string('雪', 21_845) });
        Assert.Equal(65_538, lengthPacket.Position);
        Assert.Equal(Convert.FromHexString("01FFFFE99BAA"), Bytes(lengthPacket)[..6]);
    });

    [Fact]
    public Task UnsafeCopyAndZeroFillValidateWholeCountsAndPermitExactEndOrEmptySpan() => NativePacketTestThread.Run(() =>
    {
        using var packet = new Packet(4); Fill(packet);
        CopyFour(packet); Assert.Equal(Convert.FromHexString("41420044"), Bytes(packet));
        Assert.Equal(4, packet.Position);
        packet.WriteUnsafe(null, 0); packet.ZeroFill(0); Assert.Equal(4, packet.Position);
        Refuses<InvalidOperationException>(packet, () => CopyFour(packet));
        packet.Seek(1);
        Refuses<InvalidOperationException>(packet, () => packet.ZeroFill(4));
        Refuses<InvalidOperationException>(packet, () => CopyFour(packet));
        Refuses<ArgumentOutOfRangeException>(packet, () => packet.ZeroFill(-1));
        Refuses<ArgumentOutOfRangeException>(packet, () => packet.WriteUnsafe(null, -1));
        Refuses<ArgumentNullException>(packet, () => packet.WriteUnsafe(null, 1));
        Refuses<InvalidOperationException>(packet, () => packet.ZeroFill(int.MaxValue));
        packet.Seek(0); packet.ZeroFill(4); Assert.Equal(new byte[4], Bytes(packet));
    });

    [Fact]
    public Task ValidOwnedOverlappingCopyUsesSnapshotRatherThanCorruptingItsUnreadTail() => NativePacketTestThread.Run(() =>
    {
        using var packet = new Packet(8);
        Marshal.Copy(Convert.FromHexString("0102030405060708"), 0, (nint)packet.Memory, 8);
        packet.Seek(1); packet.WriteUnsafe(packet.Memory, 6);
        Assert.Equal(Convert.FromHexString("0101020304050608"), Bytes(packet)); Assert.Equal(7, packet.Position);
    });

    [Fact]
    public Task OpaqueProtocolHelperPreparesWholeValueAndRetainsItsTrailingTokenGolden() => NativePacketTestThread.Run(() =>
    {
        using var tooSmall = new Packet(5); Fill(tooSmall);
        Refuses<InvalidOperationException>(tooSmall, () => tooSmall.CreateProtocolBuffer(127, 128));
        Refuses<ArgumentNullException>(tooSmall, () => tooSmall.CreateProtocolBuffer(null!));
        using var exact = new Packet(6); exact.CreateProtocolBuffer(127, 128);
        Assert.Equal(Convert.FromHexString("087F10800118"), Bytes(exact)); Assert.Equal(6, exact.Position);
        using var empty = new Packet(1); empty.CreateProtocolBuffer();
        Assert.Equal(new byte[] { 8 }, Bytes(empty)); Assert.Equal(1, empty.Position);
        using var insufficient = new Packet(6); Fill(insufficient);
        Refuses<InvalidOperationException>(insufficient, () => insufficient.CreateProtocolBuffer(uint.MaxValue));
        using var maximum = new Packet(7); maximum.CreateProtocolBuffer(uint.MaxValue);
        Assert.Equal(Convert.FromHexString("08FFFFFFFF0F10"), Bytes(maximum)); Assert.Equal(7, maximum.Position);
    });

    [Fact]
    public Task DisposedPacketRejectsEveryWriterEvenWithZeroCounts() => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope("TQServer");
        using var packet = new Packet(16); packet.Dispose();
        foreach (var operation in new Action[]
        {
            () => packet.Write((byte)1), () => packet.Write(true), () => packet.Write(1UL),
            () => packet.Write("A", 0), () => packet.Write(Array.Empty<string>()),
            () => packet.WriteUnsafe(null, 0), () => packet.ZeroFill(0),
            () => packet.CreateProtocolBuffer(), () => packet.Finalize(0x1234), () => packet.WriteSeal(),
        }) Assert.Throws<ObjectDisposedException>(operation);
        Assert.Equal((nuint)0, (nuint)packet.Memory);
    });

    private static void CopyFour(Packet packet)
    {
        byte* source = stackalloc byte[4] { 65, 66, 0, 68 };
        packet.WriteUnsafe(source, 4);
    }

    private static void WriteWidth(Packet packet, int width)
    {
        switch (width)
        {
            case 1: packet.Write((byte)0x5a); break;
            case 2: packet.Write((ushort)0x5a5a); break;
            case 4: packet.Write(0x5a5a5a5aU); break;
            case 8: packet.Write(0x5a5a5a5a5a5a5a5aUL); break;
            default: throw new ArgumentOutOfRangeException(nameof(width));
        }
    }

    private static void Fill(Packet packet)
    {
        Marshal.Copy(Enumerable.Repeat((byte)0xa5, packet.Capacity).ToArray(), 0, (nint)packet.Memory, packet.Capacity);
        packet.Size = 73;
    }

    private static byte[] Bytes(Packet packet)
    {
        var result = new byte[packet.Capacity]; Marshal.Copy((nint)packet.Memory, result, 0, result.Length); return result;
    }

    private static void Refuses<T>(Packet packet, Action operation) where T : Exception
    {
        var bytes = Bytes(packet); var cursor = (nuint)packet.stream; var size = packet.Size;
        Assert.Throws<T>(operation);
        Assert.Equal(bytes, Bytes(packet)); Assert.Equal(cursor, (nuint)packet.stream); Assert.Equal(size, packet.Size);
    }

    private sealed class SealScope : IDisposable
    {
        private static readonly FieldInfo Field = typeof(Packet).GetField("seal", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? prior = Field.GetValue(null);
        internal SealScope(string? value) { if (value is null) Field.SetValue(null, null); else Packet.SealString = value; }
        public void Dispose() => Field.SetValue(null, prior);
    }
}
