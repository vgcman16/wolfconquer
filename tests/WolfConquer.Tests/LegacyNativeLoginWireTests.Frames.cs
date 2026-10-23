using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeLoginWireTests
{
    [Theory]
    [InlineData(0u, 0u, false)] [InlineData(0u, 0u, true)]
    [InlineData(1u, 1002u, false)] [InlineData(1u, 1002u, true)]
    [InlineData(uint.MaxValue, uint.MaxValue, false)] [InlineData(uint.MaxValue, uint.MaxValue, true)]
    [InlineData(1u, 0u, false)] [InlineData(1u, 0u, true)]
    public Task WholeLoginHandlerWriterPreservesHeaderZeroWordTypeMapAndSeal(uint type, uint map, bool sealedFrame)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Scope(sealedFrame);
        Assert.Same(f.Packet, f.Packet.LoginHandlerCreate(type, map));
        f.AssertFrame(Frame(1044, Words(0, type, map), sealedFrame));
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task WholeVersionAndBothServerConfigurationWritersKeepEveryLiteralAndWidth(bool sealedFrame)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Scope(sealedFrame);
        Assert.Same(f.Packet, f.Packet.ServerVerCreate());
        f.AssertFrame(Frame(2161, Words(0, 0), sealedFrame));
        f.Reset(); Assert.Same(f.Packet, f.Packet.ServerConfig());
        f.AssertFrame(Frame(2047, Words(0, 0, 15, 9999, 3).Concat(new byte[] { 1 }).ToArray(), sealedFrame));
        f.Reset(); Assert.Same(f.Packet, f.Packet.CreateBenefitsConfig());
        f.AssertFrame(Frame(2131, Words(4, 1, 20191203, 20200106, 3, 20190917, 20201220, 12, 20200509, 20200601), sealedFrame));
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task WholeWritersCanReuseTheSameRealPacketWithoutErasingItsOldTail(bool sealedFrame)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Scope(sealedFrame);
        f.Packet.CreateBenefitsConfig(); byte[] previous = f.Bytes(0, 80);
        f.Packet.ServerVerCreate();
        byte[] expected = Frame(2161, Words(0, 0), sealedFrame); expected.CopyTo(previous, 0);
        Assert.Equal(expected.Length, f.Packet.Size); Assert.Equal(4, f.Packet.Position);
        Assert.Equal(previous, f.Bytes(0, 80));
        previous = f.Bytes(0, 80); f.Packet.LoginHandlerCreate(1, 1002);
        expected = Frame(1044, Words(0, 1, 1002), sealedFrame); expected.CopyTo(previous, 0);
        Assert.Equal(expected.Length, f.Packet.Size); Assert.Equal(4, f.Packet.Position);
        Assert.Equal(previous, f.Bytes(0, 80));
    });

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void WholeWriterNullStreamFaultIsNotReplacedByAFallbackPacket(int writer)
    {
        LegacyNativePacket packet = null!;
        Action call = writer switch
        {
            0 => () => packet.LoginHandlerCreate(1, 1002),
            1 => () => packet.ServerVerCreate(),
            2 => () => packet.ServerTimerCreate(),
            3 => () => packet.ServerConfig(),
            _ => () => packet.CreateBenefitsConfig()
        };
        Assert.Throws<NullReferenceException>(call);
    }
}
