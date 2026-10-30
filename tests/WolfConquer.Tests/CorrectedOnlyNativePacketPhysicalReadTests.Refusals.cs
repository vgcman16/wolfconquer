using Packet = WolfConquer.Core.Networking.LegacyNativePacket;

namespace WolfConquer.Tests;

public sealed unsafe partial class CorrectedOnlyNativePacketPhysicalReadTests
{
    [Theory]
    [InlineData("bytes")] [InlineData("cstring")] [InlineData("unsafe")]
    public Task NegativeCountsRefuseBeforeAllocationCopyOrCursorChange(string operation) => NativePacketTestThread.Run(() =>
    {
        using var packet = Exact([1, 2, 3, 4]);
        packet.Seek(1);
        byte* destination = stackalloc byte[4] { 0xEE, 0xEE, 0xEE, 0xEE };
        Refuses<ArgumentOutOfRangeException>(packet, () => Read(packet, operation, -1, destination));
        Assert.Equal(new byte[] { 0xEE, 0xEE, 0xEE, 0xEE }, new ReadOnlySpan<byte>(destination, 4).ToArray());
    });

    [Theory]
    [InlineData("bytes")] [InlineData("cstring")] [InlineData("unsafe")]
    public Task OversizeCountRefusesBeforeAllocationOrForeignWrite(string operation) => NativePacketTestThread.Run(() =>
    {
        using var packet = Exact([1, 2, 3, 4]);
        byte* destination = stackalloc byte[4] { 0xEE, 0xEE, 0xEE, 0xEE };
        Refuses<EndOfStreamException>(packet, () => Read(packet, operation, int.MaxValue, destination));
        Assert.Equal(new byte[] { 0xEE, 0xEE, 0xEE, 0xEE }, new ReadOnlySpan<byte>(destination, 4).ToArray());
    });

    [Theory]
    [InlineData("bytes")] [InlineData("cstring")] [InlineData("unsafe")]
    public Task TruncatedRequestDoesNotConsumePrefixOrAcceptNulAsMissingTail(string operation) => NativePacketTestThread.Run(() =>
    {
        using var packet = Exact([0x41, 0, 0x42, 0x43]);
        packet.Size = 3; // A captured shorter input is deliberately chosen; byte4 is spare data.
        byte* destination = stackalloc byte[4] { 0xEE, 0xEE, 0xEE, 0xEE };
        Refuses<EndOfStreamException>(packet, () => Read(packet, operation, 4, destination));
        Assert.Equal(new byte[] { 0xEE, 0xEE, 0xEE, 0xEE }, new ReadOnlySpan<byte>(destination, 4).ToArray());
    });

    [Theory]
    [InlineData("")] [InlineData("01")] [InlineData("0101")]
    [InlineData("01020041")] [InlineData("02010041020042")]
    [InlineData("020000FFFF")] [InlineData("0103004100")]
    [InlineData("FF")]
    public Task MalformedStringListRetainsEntireOriginalCursorAndInput(string hex) => NativePacketTestThread.Run(() =>
    {
        byte[] input = Convert.FromHexString(hex);
        using var packet = Exact(input.Length == 0 ? [0xA5] : input);
        packet.Size = input.Length;
        Refuses<EndOfStreamException>(packet, () => packet.ReadStringList());
    });

    [Theory]
    [InlineData(-1)] [InlineData(5)]
    public Task InvalidSizeCannotAuthorizeAnyNativeRead(int size) => NativePacketTestThread.Run(() =>
    {
        using var packet = Exact([1, 2, 3, 4]);
        packet.Size = size;
        Refuses<InvalidOperationException>(packet, () => packet.ReadUInt8());
        Refuses<InvalidOperationException>(packet, () => packet.ReadBytes(0));
        Refuses<InvalidOperationException>(packet, () => packet.ReadCString(0));
        Refuses<InvalidOperationException>(packet, () => packet.ReadStringList());
        Refuses<InvalidOperationException>(packet, () => packet.ReadUnsafe(null, 0));
    });

    [Theory]
    [InlineData(-1)] [InlineData(5)]
    public Task InvalidPhysicalCursorRefusesBeforeAnyNativeRead(int position) => NativePacketTestThread.Run(() =>
    {
        using var packet = Exact([1, 2, 3, 4]);
        packet.Seek(position);
        Refuses<InvalidOperationException>(packet, () => packet.ReadUInt8());
        Refuses<InvalidOperationException>(packet, () => packet.ReadBytes(0));
        Refuses<InvalidOperationException>(packet, () => packet.ReadCString(0));
        Refuses<InvalidOperationException>(packet, () => packet.ReadStringList());
        Refuses<InvalidOperationException>(packet, () => packet.ReadUnsafe(null, 0));
    });

    [Fact]
    public Task FullWidthCursorCannotWrapIntoAValidNarrowPosition() => NativePacketTestThread.Run(() =>
    {
        using var packet = Exact([1, 2, 3, 4]);
        packet.stream = (byte*)((nuint)packet.Memory ^ ((nuint)1 << (IntPtr.Size * 8 - 1)));
        Refuses<InvalidOperationException>(packet, () => packet.ReadUInt32());
        Refuses<InvalidOperationException>(packet, () => packet.ReadBytes(0));
        Refuses<InvalidOperationException>(packet, () => packet.ReadCString(0));
        Refuses<InvalidOperationException>(packet, () => packet.ReadStringList());
        Refuses<InvalidOperationException>(packet, () => packet.ReadUnsafe(null, 0));
    });

    [Fact]
    public Task CursorInsideCapacityButPastSuppliedInputRefusesEvenZeroLengthReads() => NativePacketTestThread.Run(() =>
    {
        using var packet = Exact([1, 2, 3, 4]);
        packet.Size = 2;
        packet.Seek(3);
        Refuses<EndOfStreamException>(packet, () => packet.ReadUInt8());
        Refuses<EndOfStreamException>(packet, () => packet.ReadBytes(0));
        Refuses<EndOfStreamException>(packet, () => packet.ReadCString(0));
        Refuses<EndOfStreamException>(packet, () => packet.ReadStringList());
        Refuses<EndOfStreamException>(packet, () => packet.ReadUnsafe(null, 0));
    });

    [Fact]
    public Task DisposedReadsRefuseWithoutChangingRetainedCursorSizeOrForeignDestination() => NativePacketTestThread.Run(() =>
    {
        using var packet = Exact([1, 2, 3, 4]);
        packet.Seek(1);
        nuint cursor = (nuint)packet.stream;
        packet.Dispose();
        byte* destination = stackalloc byte[4] { 0xEE, 0xEE, 0xEE, 0xEE };
        Assert.Throws<ObjectDisposedException>(() => packet.ReadUInt8());
        Assert.Throws<ObjectDisposedException>(() => packet.ReadBytes(0));
        Assert.Throws<ObjectDisposedException>(() => packet.ReadCString(0));
        Assert.Throws<ObjectDisposedException>(() => packet.ReadStringList());
        Assert.Throws<ObjectDisposedException>(() => packet.ReadUnsafe(destination, 1));
        Assert.Equal(cursor, (nuint)packet.stream);
        Assert.Equal(4, packet.Size);
        Assert.Equal((nuint)0, (nuint)packet.Memory);
        Assert.Equal(new byte[] { 0xEE, 0xEE, 0xEE, 0xEE }, new ReadOnlySpan<byte>(destination, 4).ToArray());
    });

    [Fact]
    public Task NullForeignDestinationRefusesACompletePositiveCopyWithoutConsumingInput() => NativePacketTestThread.Run(() =>
    {
        using var packet = Exact([1, 2, 3, 4]);
        Refuses<ArgumentNullException>(packet, () => packet.ReadUnsafe(null, 4));
    });

    [Fact]
    public Task FreshWriterWithoutPublishedInputSizeRemainsUnreadable() => NativePacketTestThread.Run(() =>
    {
        using var packet = Exact([0xCC, 0xCC, 0xCC, 0xCC]);
        packet.Size = 0;
        packet.Write((byte)0xA5);
        packet.Seek(0);
        Refuses<EndOfStreamException>(packet, () => packet.ReadUInt8());
        Assert.Equal(0, packet.Size);
    });

    private static void Refuses<T>(Packet packet, Action read) where T : Exception
    {
        byte[] before = Raw(packet);
        nuint cursor = (nuint)packet.stream;
        int size = packet.Size;
        Assert.Throws<T>(read);
        Assert.Equal(cursor, (nuint)packet.stream);
        Assert.Equal(size, packet.Size);
        Assert.Equal(before, Raw(packet));
    }

    private static void Read(Packet packet, string operation, int count, byte* destination)
    {
        switch (operation)
        {
            case "bytes": packet.ReadBytes(count); break;
            case "cstring": packet.ReadCString(count); break;
            case "unsafe": packet.ReadUnsafe(destination, count); break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }
}
