using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed unsafe class LegacyNativePacketAllocationSafetyTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(64)]
    [InlineData(1024)]
    [InlineData(16384)]
    public Task IntegerAllocationReportsExactImmutableCapacity(int capacity)
        => NativePacketTestThread.Run(() =>
    {
        using var packet = new LegacyNativePacket(capacity);
        Assert.Equal(capacity, packet.Capacity);
        Assert.NotEqual(IntPtr.Zero, (IntPtr)packet.Memory);
        Assert.Equal(0, packet.Position);
        Assert.Equal(0, packet.Size);
        packet.Memory[capacity - 1] = 0xA5;
        Assert.Equal((byte)0xA5, packet.Memory[capacity - 1]);
        packet.Size = int.MaxValue;
        Assert.Equal(capacity, packet.Capacity); // Mutable wire Size is not allocation authority.
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(1024)]
    [InlineData(1025)]
    [InlineData(4096)]
    [InlineData(16384)]
    public Task ByteBufferAllocatesEnoughAndPreservesEveryInputByte(int length)
        => NativePacketTestThread.Run(() =>
    {
        var source = Enumerable.Range(0, length).Select(index => (byte)(index * 37)).ToArray();
        using var packet = new LegacyNativePacket(source);
        Assert.Equal(Math.Max(1024, length), packet.Capacity);
        Assert.Equal(length, packet.Size);
        Assert.Equal(0, packet.Position);
        var actual = new byte[length];
        Marshal.Copy((IntPtr)packet.Memory, actual, 0, length);
        Assert.Equal(source, actual);
        if (length > 0)
        {
            source[length - 1] ^= 0xFF;
            Assert.Equal(actual[length - 1], packet.Memory[length - 1]);
        }
    });

    [Fact]
    public Task NullBufferIsRejectedBeforeConstruction()
        => NativePacketTestThread.Run(() =>
    {
        Assert.Throws<ArgumentNullException>(() => new LegacyNativePacket((byte[])null!));
    });

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public Task NonpositiveAllocationIsRejectedBeforeConstruction(int capacity)
        => NativePacketTestThread.Run(() =>
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LegacyNativePacket(capacity));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ExistingDeepStackGuardStillRejectsBothConstructors(bool fromBuffer)
        => NativePacketTestThread.Run(() =>
    {
        var error = Assert.Throws<Exception>(() => ConstructAtDepth(80, fromBuffer));
        Assert.Contains("frame count excceeded 70", error.Message);
    });

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static LegacyNativePacket ConstructAtDepth(int remaining, bool fromBuffer)
    {
        if (remaining == 0)
            return fromBuffer ? new LegacyNativePacket(new byte[16]) : new LegacyNativePacket(16);
        var packet = ConstructAtDepth(remaining - 1, fromBuffer);
        GC.KeepAlive(packet); // Prevent tail-call removal of the actual constructor stack.
        return packet;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DisposalClearsStorageWithoutChangingAllocationMetadata(bool fromBuffer)
        => NativePacketTestThread.Run(() =>
    {
        using var packet = fromBuffer ? new LegacyNativePacket(new byte[16]) : new LegacyNativePacket(16);
        int capacity = packet.Capacity;
        packet.Dispose();
        packet.Dispose();
        Assert.Equal(IntPtr.Zero, (IntPtr)packet.Memory);
        Assert.Equal(capacity, packet.Capacity);
    });
}
