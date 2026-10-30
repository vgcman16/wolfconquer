using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed unsafe class CorrectedOnlyNativePreparedCodecTests
{
    [Fact]
    public Task PreparedReadPublishesOnlyACompleteRealParse() => NativePacketTestThread.Run(() =>
    {
        byte[] input = Convert.FromHexString("34127856");
        using var packet = new LegacyNativePacket(input);
        Assert.Equal(((ushort)0x1234, (ushort)0x5678),
            packet.ReadPrepared(source => (source.ReadUInt16(), source.ReadUInt16())));
        Assert.Equal(4, packet.Position); Assert.Equal(4, packet.Size);
        Assert.Equal(input, NativeStringPacketTestScope.Bytes(packet));
    });

    [Fact]
    public Task OrdinaryFailureAfterRealReadRestoresTheInitialCursor() => NativePacketTestThread.Run(() =>
    {
        byte[] input = Convert.FromHexString("34127856");
        using var packet = new LegacyNativePacket(input);
        packet.Seek(2);
        var expected = new InvalidDataException("fixture failure after real native read");
        var actual = Assert.Throws<InvalidDataException>(() => packet.ReadPrepared<int>(source =>
        {
            Assert.Equal((ushort)0x5678, source.ReadUInt16());
            throw expected;
        }));
        Assert.Same(expected, actual); Assert.Equal(2, packet.Position); Assert.Equal(4, packet.Size);
        Assert.Equal(input, NativeStringPacketTestScope.Bytes(packet));
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task OrdinaryScratchFailureDoesNotTouchTheTarget(bool sealedFrame) => NativePacketTestThread.Run(() =>
    {
        using var sealScope = new NativeStringPacketTestScope(sealedFrame);
        using var packet = new LegacyNativePacket(23);
        Fill(packet); packet.Size = 17; packet.Seek(11);
        byte[] before = NativeStringPacketTestScope.Bytes(packet, packet.Capacity);
        var expected = new InvalidDataException("fixture failure after real scratch writes");
        var actual = Assert.Throws<InvalidDataException>(() => packet.PrepareFrame(2036, scratch =>
        {
            Assert.NotSame(packet, scratch); Assert.Equal(packet.Capacity, scratch.Capacity);
            scratch.Write(0x12345678u); scratch.Write(new[] { "fx" });
            throw expected;
        }));
        Assert.Same(expected, actual);
        Assert.Equal(before, NativeStringPacketTestScope.Bytes(packet, packet.Capacity));
        Assert.Equal(17, packet.Size); Assert.Equal(11, packet.Position);
    });

    [Theory]
    [InlineData(-1)] [InlineData(9)]
    public Task InvalidTargetSizeRefusesBeforeScratchCallback(int size) => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeStringPacketTestScope();
        using var packet = new LegacyNativePacket(8);
        Fill(packet); packet.Size = size; packet.Seek(2);
        byte[] before = NativeStringPacketTestScope.Bytes(packet, packet.Capacity);
        bool invoked = false;
        Assert.Throws<InvalidOperationException>(() => packet.PrepareFrame(2036, _ => invoked = true));
        Assert.False(invoked); Assert.Equal(size, packet.Size); Assert.Equal(2, packet.Position);
        Assert.Equal(before, NativeStringPacketTestScope.Bytes(packet, packet.Capacity));
    });

    [Theory]
    [InlineData(1)] [InlineData(3)]
    public Task MissingHeaderCapacityRefusesBeforeScratchCallback(int capacity) => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeStringPacketTestScope();
        using var packet = new LegacyNativePacket(capacity); Fill(packet);
        byte[] before = NativeStringPacketTestScope.Bytes(packet, capacity);
        bool invoked = false;
        Assert.Throws<InvalidOperationException>(() => packet.PrepareFrame(2036, _ => invoked = true));
        Assert.False(invoked); Assert.Equal(0, packet.Size); Assert.Equal(0, packet.Position);
        Assert.Equal(before, NativeStringPacketTestScope.Bytes(packet, capacity));
    });

    [Fact]
    public Task ChangedSealDuringPreparationRefusesBeforeTargetWrites() => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeStringPacketTestScope(true);
        using var packet = new LegacyNativePacket(16); Fill(packet);
        byte[] before = NativeStringPacketTestScope.Bytes(packet, packet.Capacity);
        Assert.Throws<InvalidOperationException>(() => packet.PrepareFrame(2036, scratch =>
        {
            scratch.Write(17u); LegacyNativePacket.SealString = "12345678";
        }));
        Assert.Equal(before, NativeStringPacketTestScope.Bytes(packet, packet.Capacity));
        Assert.Equal(0, packet.Position); Assert.Equal(0, packet.Size);
    });

    [Fact]
    public Task PreparedFramePreservesNativeEightByteUtf8SealPrefix() => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeStringPacketTestScope();
        LegacyNativePacket.SealString = "éééééééé";
        using var packet = new LegacyNativePacket(16);
        packet.PrepareFrame(0x1234, scratch => scratch.Write(-2));
        Assert.Equal(Convert.FromHexString("08003412FEFFFFFFC3A9C3A9C3A9C3A9"),
            NativeStringPacketTestScope.Bytes(packet));
        Assert.Equal(4, packet.Position); Assert.Equal(16, packet.Size);
    });

    [Fact]
    public Task NullDelegatesRefuseWithoutChangingAnExistingPacket() => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeStringPacketTestScope();
        using var packet = new LegacyNativePacket(16); Fill(packet); packet.Size = 16; packet.Seek(3);
        byte[] before = NativeStringPacketTestScope.Bytes(packet);
        Assert.Throws<ArgumentNullException>(() => packet.ReadPrepared<int>(null!));
        Assert.Throws<ArgumentNullException>(() => packet.PrepareFrame(2036, null!));
        Assert.Equal(before, NativeStringPacketTestScope.Bytes(packet));
        Assert.Equal(3, packet.Position); Assert.Equal(16, packet.Size);
    });

    [Fact]
    public Task UnrepresentableWholeFrameLengthRefusesAfterScratchPreparation() => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeStringPacketTestScope();
        using var packet = new LegacyNativePacket(65_544); Fill(packet);
        packet.Size = 17; packet.Seek(11);
        byte[] before = NativeStringPacketTestScope.Bytes(packet, packet.Capacity);
        Assert.Throws<InvalidOperationException>(() => packet.PrepareFrame(2036,
            scratch => scratch.ZeroFill(65_532))); // Header + body cannot fit UInt16.
        Assert.Equal(before, NativeStringPacketTestScope.Bytes(packet, packet.Capacity));
        Assert.Equal(17, packet.Size); Assert.Equal(11, packet.Position);
    });

    private static void Fill(LegacyNativePacket packet)
    {
        byte[] bytes = Enumerable.Repeat((byte)0xA5, packet.Capacity).ToArray();
        Marshal.Copy(bytes, 0, (nint)packet.Memory, bytes.Length);
    }
}
