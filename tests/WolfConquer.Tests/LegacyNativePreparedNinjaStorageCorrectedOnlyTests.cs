using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// CORRECTED SOURCE ONLY. Never run these tiny/disposed native-buffer inputs on
// the old unsafe writers, including as a baseline intended merely to fail.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed unsafe class LegacyNativePreparedNinjaStorageCorrectedOnlyTests
{
    [Theory]
    [InlineData(false, false, 24)]
    [InlineData(false, true, 32)]
    [InlineData(true, false, 51)]
    [InlineData(true, true, 59)]
    public Task InsufficientActualCapacityRefusesWithoutAnyNativeWrite(bool user, bool sealedFrame, int capacity) =>
        NativePacketTestThread.Run(() =>
        {
            using var fixture = new NativeSpellTestFixture();
            if (sealedFrame) LegacyNativePacket.SealString = "TQServer";
            using var packet = new LegacyNativePacket(capacity);
            new Span<byte>(packet.Memory, capacity).Fill(0xA5);
            packet.Size = 7; packet.Seek(1);
            var before = new ReadOnlySpan<byte>(packet.Memory, capacity).ToArray();
            if (user) Assert.Throws<InvalidOperationException>(() => packet.CreateNinjaUser(new()));
            else Assert.Throws<InvalidOperationException>(() => packet.SpellCreate(new()));
            Assert.Equal(before, new ReadOnlySpan<byte>(packet.Memory, capacity).ToArray());
            Assert.Equal(7, packet.Size);
            Assert.Equal(1, packet.Position);
        });

    [Theory]
    [InlineData(false, false, 25)]
    [InlineData(false, true, 33)]
    [InlineData(true, false, 52)]
    [InlineData(true, true, 60)]
    public Task ExactCapacityRetainsHeaderSizeAndCursor(bool user, bool sealedFrame, int capacity) => NativePacketTestThread.Run(() =>
    {
        using var fixture = new NativeSpellTestFixture();
        if (sealedFrame) LegacyNativePacket.SealString = "TQServer";
        using var packet = new LegacyNativePacket(capacity);
        if (user) Assert.Same(packet, packet.CreateNinjaUser(new()));
        else Assert.Same(packet, packet.SpellCreate(new()));
        Assert.Equal(capacity, packet.Size);
        Assert.Equal(4, packet.Position);
        Assert.Equal(capacity - (sealedFrame ? 8 : 0), (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
            new ReadOnlySpan<byte>(packet.Memory, capacity)));
    });

    [Fact]
    public Task DisposedStorageRefusesBeforeCursorOrSizePublication() => NativePacketTestThread.Run(() =>
    {
        using var fixture = new NativeSpellTestFixture();
        var packet = new LegacyNativePacket(64);
        packet.Size = 7; packet.Seek(1);
        var cursor = (nint)packet.stream;
        packet.Dispose();
        Assert.Throws<InvalidOperationException>(() => packet.SpellCreate(new()));
        Assert.Throws<InvalidOperationException>(() => packet.CreateNinjaUser(new()));
        Assert.Equal(7, packet.Size);
        Assert.Equal(cursor, (nint)packet.stream);
        Assert.Equal((nint)0, LegacyNativePreparedPacketStorage.Address(packet));
    });

    [Fact]
    public Task PreparedStorageFenceChecksAddressCapacityCursorSizeAndExactBytes() => NativePacketTestThread.Run(() =>
    {
        using var packet = new LegacyNativePacket(16);
        byte[] frame = [6, 0, 1, 0, 0xAA, 0xBB];
        var address = LegacyNativePreparedPacketStorage.Address(packet);
        LegacyNativePreparedPacketStorage.Write(packet, frame);
        Assert.True(LegacyNativePreparedPacketStorage.Matches(packet, frame, address));
        Assert.False(LegacyNativePreparedPacketStorage.Matches(packet, frame, 0));
        Assert.False(LegacyNativePreparedPacketStorage.Matches(packet, frame, address + 1));
        packet.Seek(5);
        Assert.False(LegacyNativePreparedPacketStorage.Matches(packet, frame, address));
        packet.Seek(4); packet.Size = 5;
        Assert.False(LegacyNativePreparedPacketStorage.Matches(packet, frame, address));
        packet.Size = 6; packet.Memory[5] = 0;
        Assert.False(LegacyNativePreparedPacketStorage.Matches(packet, frame, address));
        packet.Dispose();
        Assert.False(LegacyNativePreparedPacketStorage.Matches(packet, frame, address));
    });

    [Fact]
    public Task MissingRequiredStorageAndFrameArgumentsRefuseWithoutWrites() => NativePacketTestThread.Run(() =>
    {
        using var packet = new LegacyNativePacket(16);
        new Span<byte>(packet.Memory, 16).Fill(0xA5);
        packet.Size = 7; packet.Seek(1);
        Assert.Throws<InvalidOperationException>(() => LegacyNativePreparedPacketStorage.Validate(null!, 4));
        Assert.Throws<ArgumentNullException>(() => LegacyNativePreparedPacketStorage.Write(packet, null!));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 16), new ReadOnlySpan<byte>(packet.Memory, 16).ToArray());
        Assert.Equal(7, packet.Size);
        Assert.Equal(1, packet.Position);
    });
}
