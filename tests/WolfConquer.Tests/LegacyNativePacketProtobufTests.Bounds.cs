using System.Runtime.InteropServices;
using WolfConquer.Core.World;
using Packet = WolfConquer.Core.Networking.LegacyNativePacket;
using Dict = WolfConquer.Core.World.LegacyNativeMsgDictLottery;

namespace WolfConquer.Tests;

// Run these refusal cases only against the corrected API. The old unchecked
// native implementation is never a safe executable red-baseline target.
public sealed unsafe partial class LegacyNativePacketProtobufTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("TQServer")]
    public Task FreshWriterAllowsExactPayloadAndCurrentSealCapacityWithIndependentWireGolden(string? seal) => NativePacketTestThread.Run(() =>
    {
        using var savedSeal = new SealScope(seal);
        byte[] expected = Convert.FromHexString(seal is null ? "0800A10908011011" : "0800A109080110115451536572766572");
        byte[] original = Enumerable.Repeat((byte)0xA5, expected.Length).ToArray();
        using var packet = new Packet(original.Length);
        Marshal.Copy(original, 0, (nint)packet.Memory, original.Length);
        packet.InitWriter();
        Assert.Equal(0, packet.Size);

        packet.ProtoBufferSerialize(new Dict { Type = 1, Done = 17 });

        Assert.Equal(8, packet.Position);
        Assert.Equal(0, packet.Size);
        Assert.Equal(original[..4], Copy(packet, 4));
        Assert.Equal(Convert.FromHexString("08011011"), Copy(packet, 8)[4..]);
        if (seal is not null) Assert.Equal(original[8..], Copy(packet, original.Length)[8..]);
        packet.Finalize(2465);
        Assert.Equal(4, packet.Position);
        Assert.Equal(expected.Length, packet.Size);
        Assert.Equal(expected, Copy(packet, expected.Length));
    });

    [Theory]
    [InlineData(null)]
    [InlineData("TQServer")]
    public Task OneBytePayloadOrSealOverflowRefusesBeforeAnyNativeWrite(string? seal) => NativePacketTestThread.Run(() =>
    {
        using var savedSeal = new SealScope(seal);
        int capacity = 7 + Packet.SealSize;
        byte[] original = Enumerable.Repeat((byte)0xD6, capacity).ToArray();
        using var packet = new Packet(capacity);
        Marshal.Copy(original, 0, (nint)packet.Memory, original.Length);
        packet.InitWriter();
        var message = new Dict { Type = 1, Done = 17 };

        Assert.Throws<InvalidOperationException>(() => packet.ProtoBufferSerialize(message));

        Assert.Equal(4, packet.Position);
        Assert.Equal(0, packet.Size);
        Assert.Equal(original, Copy(packet, capacity));
        Assert.Equal(1u, message.Type);
        Assert.Equal(17u, message.Done);
    });

    [Fact]
    public Task EmptyAppendAndValidDecodeAllowAnIdleOnePastEndCursor() => NativePacketTestThread.Run(() =>
    {
        using var savedSeal = new SealScope(null);
        byte[] original = Convert.FromHexString("0800A10908011011");
        using var packet = new Packet(original.Length);
        Marshal.Copy(original, 0, (nint)packet.Memory, original.Length);
        packet.Size = original.Length;
        packet.Seek(original.Length);

        packet.ProtoBufferSerialize(null!);
        Assert.Equal(original.Length, packet.Position);
        Assert.Equal(original, Copy(packet, original.Length));
        Dict actual = packet.ProtoBufferDeserialize(new Dict());

        Assert.Equal(1u, actual.Type);
        Assert.Equal(17u, actual.Done);
        Assert.Equal(original.Length, packet.Position);
        Assert.Equal(original.Length, packet.Size);
        Assert.Equal(original, Copy(packet, original.Length));
    });

    [Fact]
    public Task LargeRealMessagePayloadRefusesWithoutDirtyingNativeStorageOrSourceRows() => NativePacketTestThread.Run(() =>
    {
        using var savedSeal = new SealScope(null);
        byte[] original = Enumerable.Repeat((byte)0xB4, 64).ToArray();
        using var packet = new Packet(original.Length);
        Marshal.Copy(original, 0, (nint)packet.Memory, original.Length);
        packet.Size = 31;
        packet.Seek(7);
        string text = new('x', Packet.MAX_SIZE + 1);
        string[] content = [text];
        var message = new LegacyNativeMsgMessage.MsgTalkProto { Color = 17, Content = content };

        Assert.Throws<InvalidOperationException>(() => packet.ProtoBufferSerialize(message));

        Assert.Equal(7, packet.Position);
        Assert.Equal(31, packet.Size);
        Assert.Equal(original, Copy(packet, original.Length));
        Assert.Same(content, message.Content);
        Assert.Same(text, Assert.Single(message.Content));
        Assert.Equal(17u, message.Color);
    });

    [Theory]
    [InlineData(-1)]
    [InlineData(65)]
    public Task InvalidMutableSizeRefusesBothNativeApisWithoutChanges(int size) => NativePacketTestThread.Run(() =>
    {
        byte[] original = Frame(8, 2465, 0x08, 0x01, 0x10, 0x11);
        using var packet = new Packet(64);
        Marshal.Copy(original, 0, (nint)packet.Memory, original.Length);
        packet.Size = size;
        packet.Seek(11);
        var supplied = new Dict { Type = 7 };

        Assert.Throws<InvalidOperationException>(() => packet.ProtoBufferSerialize(supplied));
        Assert.Throws<InvalidOperationException>(() => packet.ProtoBufferDeserialize(supplied));

        Assert.Equal(11, packet.Position);
        Assert.Equal(size, packet.Size);
        Assert.Equal(original, Copy(packet, original.Length));
        Assert.Equal(7u, supplied.Type);
    });

    [Theory]
    [InlineData(-1)]
    [InlineData(65)]
    public Task CursorOutsideAllocationRefusesBothNativeApisWithoutChanges(int offset) => NativePacketTestThread.Run(() =>
    {
        byte[] original = Frame(8, 2465, 0x08, 0x01, 0x10, 0x11);
        using var packet = new Packet(64);
        Marshal.Copy(original, 0, (nint)packet.Memory, original.Length);
        packet.Size = 64;
        packet.Seek(offset);
        nuint cursor = (nuint)packet.stream;

        Assert.Throws<InvalidOperationException>(() => packet.ProtoBufferSerialize(new Dict()));
        Assert.Throws<InvalidOperationException>(() => packet.ProtoBufferDeserialize(new Dict()));

        Assert.Equal(cursor, (nuint)packet.stream);
        Assert.Equal(64, packet.Size);
        Assert.Equal(original, Copy(packet, original.Length));
    });

    [Fact]
    public Task FullWidthInvalidCursorCannotWrapIntoApparentlyValidIntPosition() => NativePacketTestThread.Run(() =>
    {
        using var packet = new Packet(new byte[64]);
        nuint cursor = (nuint)packet.Memory ^ ((nuint)1 << (IntPtr.Size * 8 - 1));
        packet.stream = (byte*)cursor;

        Assert.Throws<InvalidOperationException>(() => packet.ProtoBufferSerialize(new Dict()));
        Assert.Throws<InvalidOperationException>(() => packet.ProtoBufferDeserialize(new Dict()));

        Assert.Equal(cursor, (nuint)packet.stream);
        Assert.Equal(64, packet.Size);
        Assert.Equal(new byte[64], Copy(packet, 64));
    });

    [Fact]
    public Task DisposedStorageRefusesBothNativeApisWithoutChangingRetainedCursorOrSize() => NativePacketTestThread.Run(() =>
    {
        using var packet = new Packet(new byte[64]);
        packet.Seek(13);
        nuint cursor = (nuint)packet.stream;
        packet.Dispose();

        Assert.Throws<ObjectDisposedException>(() => packet.ProtoBufferSerialize(new Dict()));
        Assert.Throws<ObjectDisposedException>(() => packet.ProtoBufferDeserialize(new Dict()));

        Assert.Equal((nuint)0, (nuint)packet.Memory);
        Assert.Equal(cursor, (nuint)packet.stream);
        Assert.Equal(64, packet.Size);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    public Task ActualInputSizeRefusesTruncatedHeaderOrPayloadDespiteBackedSpareBytes(int size) => NativePacketTestThread.Run(() =>
    {
        byte[] original = Frame(8, 2465, 0x08, 0x01, 0x10, 0x11);
        using var packet = new Packet(original);
        packet.Size = size;
        packet.Seek(11);
        var supplied = new Dict { Type = 7, Done = 23 };

        Assert.Throws<InvalidDataException>(() => packet.ProtoBufferDeserialize(supplied));

        Assert.Equal(11, packet.Position);
        Assert.Equal(size, packet.Size);
        Assert.Equal(original, Copy(packet, original.Length));
        Assert.Equal(7u, supplied.Type);
        Assert.Equal(23u, supplied.Done);
    });

    [Fact]
    public Task DeclaredLengthBeyondPhysicalAllocationRefusesBeforePayloadRead() => NativePacketTestThread.Run(() =>
    {
        byte[] original = [0xFF, 0xFF, 0xA1, 0x09];
        using var packet = new Packet(4);
        Marshal.Copy(original, 0, (nint)packet.Memory, original.Length);
        packet.Size = 4;
        packet.Seek(4); // One-past-end is a valid idle cursor, never a readable payload owner.

        Assert.Throws<InvalidDataException>(() => packet.ProtoBufferDeserialize(new Dict()));

        Assert.Equal(4, packet.Position);
        Assert.Equal(4, packet.Size);
        Assert.Equal(original, Copy(packet, original.Length));
    });

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task PhysicallyShortAllocationRefusesBeforeReadingAnyHeader(int length) => NativePacketTestThread.Run(() =>
    {
        byte[] original = Enumerable.Repeat((byte)0xFF, length).ToArray();
        using var packet = new Packet(length);
        Marshal.Copy(original, 0, (nint)packet.Memory, length);
        packet.Size = length;
        packet.Seek(length);

        Assert.Throws<InvalidDataException>(() => packet.ProtoBufferDeserialize(new Dict()));

        Assert.Equal(length, packet.Position);
        Assert.Equal(length, packet.Size);
        Assert.Equal(original, Copy(packet, length));
    });

    [Fact]
    public Task RawManagedDecodeRemainsIndependentOfDisposedAndInvalidNativePacketState() => NativePacketTestThread.Run(() =>
    {
        byte[] payload = Convert.FromHexString("08011011");
        using var packet = new Packet(new byte[32]);
        packet.Seek(-1);
        packet.Size = int.MaxValue;
        nuint cursor = (nuint)packet.stream;
        packet.Dispose();
        var supplied = new Dict { Type = 7, Done = 23 };

        Dict actual = packet.ProtoBufferDeserialize(supplied, payload);

        Assert.NotSame(supplied, actual);
        Assert.Equal(1u, actual.Type);
        Assert.Equal(17u, actual.Done);
        Assert.Equal(7u, supplied.Type);
        Assert.Equal(23u, supplied.Done);
        Assert.Equal(cursor, (nuint)packet.stream);
        Assert.Equal(int.MaxValue, packet.Size);
        Assert.Equal((nuint)0, (nuint)packet.Memory);
        Assert.Equal(Convert.FromHexString("08011011"), payload);
    });
}
