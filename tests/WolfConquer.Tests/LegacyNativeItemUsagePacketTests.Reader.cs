using WolfConquer.Core.World;
using Packet = WolfConquer.Core.Networking.LegacyNativePacket;

namespace WolfConquer.Tests;

public sealed unsafe partial class LegacyNativeItemUsagePacketTests
{
    [Theory]
    [InlineData(0x7FFF, 0x00007FFFu)]
    [InlineData(0x8000, 0xFFFF8000u)]
    [InlineData(0xFFFF, 0xFFFFFFFFu)]
    [InlineData(0xBEEF, 0xFFFFBEEFu)]
    public Task ReaderUsesCurrentCursorAndSignExtendsInt16IntoUIntEnum(int rawAction, uint expected) => NativePacketTestThread.Run(() =>
    {
        const int start = 7;
        byte[] original = ReaderInput(start, (ushort)rawAction, 0);
        using var packet = new Packet(original);
        packet.Size = 0; // Physically initialized512 bytes remain valid; native parser ignores Size.
        packet.Seek(start);

        packet.GetUsageItem(out var action, out var id, out var parameter, out var timestamp,
            out var parameter2, out var parameter3, out var parameter4, out var args, out var loader);

        Assert.Equal(expected, (uint)action);
        Assert.Equal(0x11223344u, id);
        Assert.Equal(0x0123456789ABCDEFul, parameter);
        Assert.Equal(0x55667788u, timestamp);
        Assert.Equal(0u, parameter2);
        Assert.Equal(0xA1A2A3A4u, parameter3);
        Assert.Equal(0xB1B2B3B4u, parameter4);
        Assert.Equal((byte)0x5A, loader);
        Assert.Empty(args);
        Assert.Equal(start + 79, packet.Position);
        Assert.Equal(0, packet.Size);
        Assert.Equal(original, Copy(packet, original.Length));
    });

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(49u)]
    [InlineData(50u)]
    [InlineData(uint.MaxValue)]
    public Task ReaderOnlySkipsExtraEightAndReadsArgsWhenSecondParameterIsOneThrough49(uint count) => NativePacketTestThread.Run(() =>
    {
        const int start = 13;
        byte[] original = ReaderInput(start, 67, count);
        using var packet = new Packet(original);
        packet.Seek(start);

        packet.GetUsageItem(out var action, out _, out _, out _, out var parameter2,
            out _, out _, out var args, out var loader);

        int expectedCount = count is > 0 and < 50 ? (int)count : 0;
        Assert.Equal(67u, (uint)action);
        Assert.Equal(count, parameter2);
        Assert.Equal((byte)0x5A, loader);
        Assert.Equal(Enumerable.Range(0, expectedCount).Select(i => 0xABC00000u + (uint)i), args);
        Assert.Equal(start + (expectedCount == 0 ? 79 : 87 + expectedCount * 4), packet.Position);
        Assert.Equal(original.Length, packet.Size);
        Assert.Equal(original, Copy(packet, original.Length));
    });

    [Fact]
    public Task ReaderAllocatesANewEmptyArgsOwnerForEachNoArgsCall() => NativePacketTestThread.Run(() =>
    {
        const int start = 7;
        using var packet = new Packet(ReaderInput(start, 1, 0));
        packet.Seek(start);
        packet.GetUsageItem(out _, out _, out _, out _, out _, out _, out _, out var first, out _);
        first.Add(99);
        packet.Seek(start);

        packet.GetUsageItem(out _, out _, out _, out _, out _, out _, out _, out var second, out _);

        Assert.NotSame(first, second);
        Assert.Equal(99u, Assert.Single(first));
        Assert.Empty(second);
        Assert.Equal(start + 79, packet.Position);
    });
}
